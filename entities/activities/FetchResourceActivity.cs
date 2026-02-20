using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for fetching resources from one facility (source) and bringing them to another (destination).
/// Used by bakers to fetch water from the well, or any entity that needs to transport items between facilities.
///
/// Uses a Stateless state machine to manage phase transitions and interruption/resumption.
///
/// States:
/// GoingToSource -> WorkingAtSource -> TakingResource -> GoingToDestination -> DepositingResource
///
/// Interruption behavior (two-zone regression):
/// - Source zone (GoingToSource, WorkingAtSource, TakingResource) regresses to GoingToSource
/// - Destination zone (GoingToDestination, DepositingResource) regresses to GoingToDestination.
/// </summary>
public class FetchResourceActivity : StatefulActivity<FetchResourceActivity.FetchState, FetchResourceActivity.FetchTrigger>
{
    /// <summary>
    /// States representing the phases of fetching a resource.
    /// </summary>
    public enum FetchState
    {
        GoingToSource,
        WorkingAtSource,
        TakingResource,
        GoingToDestination,
        DepositingResource,
    }

    /// <summary>
    /// Triggers that cause state transitions.
    /// </summary>
    public enum FetchTrigger
    {
        ArrivedAtSource,
        WorkComplete,
        ResourceTaken,
        ArrivedAtDestination,
        ResourceDeposited,
        Interrupted,
        Resumed,
    }

    private readonly Facility _sourceFacility;
    private readonly Facility _destinationFacility;
    private readonly string _itemId;
    private readonly int _desiredQuantity;

    private int _actualQuantityTaken;

    // Work phase tracking (progress preserved across interruptions)
    private uint _workTimer;
    private uint _fetchDuration;
    private Need? _energyNeed;

    // Energy cost per tick while working (same as DrawWaterActivity)
    private const float ENERGYCOSTPERTICK = 0.02f;

    // Display name helpers — prefer building name when available, fall back to facility ID
    private string SourceName => _sourceFacility.Owner?.BuildingName ?? _sourceFacility.Id;
    private string DestinationName => _destinationFacility.Owner?.BuildingName ?? _destinationFacility.Id;

    protected override FetchTrigger InterruptedTrigger => FetchTrigger.Interrupted;

    protected override FetchTrigger ResumedTrigger => FetchTrigger.Resumed;

    public override string DisplayName => _machine.State switch
    {
        FetchState.GoingToSource => L.TrFmt("activity.GOING_TO_GET", _itemId),
        FetchState.WorkingAtSource => L.TrFmt("activity.FETCHING", _itemId),
        FetchState.TakingResource => L.TrFmt("activity.TAKING", _itemId),
        FetchState.GoingToDestination => L.TrFmt("activity.BRINGING", _itemId),
        FetchState.DepositingResource => L.TrFmt("activity.STORING", _itemId),
        _ => L.TrFmt("activity.FETCHING", _itemId)
    };

    public override Building? TargetBuilding => _machine.State is FetchState.GoingToSource
        or FetchState.WorkingAtSource
        or FetchState.TakingResource
            ? _sourceFacility.Owner
            : _destinationFacility.Owner;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        // When working at source, delegate to the navigation sub-activity for goal positions
        if (_machine.State == FetchState.WorkingAtSource && _currentSubActivity != null)
        {
            return _currentSubActivity.GetAlternativeGoalPositions(entity);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchResourceActivity"/> class.
    /// Create an activity to fetch resources from one facility (source) to another (destination).
    /// </summary>
    /// <param name="sourceFacility">The facility to take resources from (e.g., Well storage).</param>
    /// <param name="destinationFacility">The facility to deposit resources to (e.g., Bakery storage).</param>
    /// <param name="itemId">The item ID to fetch (e.g., "water").</param>
    /// <param name="desiredQuantity">How many items to fetch (will take up to this amount if available).</param>
    /// <param name="priority">Action priority.</param>
    public FetchResourceActivity(
        Facility sourceFacility,
        Facility destinationFacility,
        string itemId,
        int desiredQuantity,
        int priority = 0)
        : base(FetchState.GoingToSource)
    {
        _sourceFacility = sourceFacility;
        _destinationFacility = destinationFacility;
        _itemId = itemId;
        _desiredQuantity = desiredQuantity;
        Priority = priority;

        ConfigureStateMachine();
    }

    /// <summary>
    /// Configures the state machine transitions, including interruption/resumption behavior.
    ///
    /// Source zone states (GoingToSource, WorkingAtSource, TakingResource) regress to GoingToSource on interruption.
    /// Destination zone states (GoingToDestination, DepositingResource) regress to GoingToDestination on interruption.
    /// Navigation states use PermitReentry for Resumed to force fresh pathfinder creation.
    /// Sub-activity references are automatically nulled by the base class OnTransitioned callback.
    /// </summary>
    private void ConfigureStateMachine()
    {
        // GoingToSource: navigation state for source zone
        _machine.Configure(FetchState.GoingToSource)
            .Permit(FetchTrigger.ArrivedAtSource, FetchState.WorkingAtSource)
            .Permit(FetchTrigger.ResourceTaken, FetchState.GoingToDestination) // Skip work if no fetch duration
            .PermitReentry(FetchTrigger.Interrupted) // Source zone regression
            .PermitReentry(FetchTrigger.Resumed);    // Re-enter to force fresh pathfinder

        // WorkingAtSource: idle work timer phase
        _machine.Configure(FetchState.WorkingAtSource)
            .Permit(FetchTrigger.WorkComplete, FetchState.TakingResource)
            .Permit(FetchTrigger.Interrupted, FetchState.GoingToSource); // Source zone regression

        // TakingResource: take items from source storage
        _machine.Configure(FetchState.TakingResource)
            .Permit(FetchTrigger.ResourceTaken, FetchState.GoingToDestination)
            .Permit(FetchTrigger.Interrupted, FetchState.GoingToSource); // Source zone regression

        // GoingToDestination: navigation state for destination zone
        _machine.Configure(FetchState.GoingToDestination)
            .Permit(FetchTrigger.ArrivedAtDestination, FetchState.DepositingResource)
            .PermitReentry(FetchTrigger.Interrupted) // Destination zone regression
            .PermitReentry(FetchTrigger.Resumed);    // Re-enter to force fresh pathfinder

        // DepositingResource: deposit items to destination storage
        _machine.Configure(FetchState.DepositingResource)
            .Permit(FetchTrigger.Interrupted, FetchState.GoingToDestination); // Destination zone regression

        // ResourceDeposited is handled by Complete() directly, no state transition needed
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get fetch duration from source facility's storage configuration
        _fetchDuration = _sourceFacility.SelfAsEntity().GetTrait<StorageTrait>()?.FetchDuration ?? 0;

        // Get energy need for direct energy cost while working
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");

        DebugLog("FETCH", $"Started FetchResourceActivity: {_desiredQuantity}x {_itemId} from {SourceName} to {DestinationName}, fetch duration: {_fetchDuration} ticks", 0);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if facilities still exist
        if (!GodotObject.IsInstanceValid(_sourceFacility))
        {
            DebugLog("FETCH", "Source facility no longer valid", 0);
            Fail();
            return null;
        }

        if (!GodotObject.IsInstanceValid(_destinationFacility))
        {
            DebugLog("FETCH", "Destination facility no longer valid", 0);
            Fail();
            return null;
        }

        return _machine.State switch
        {
            FetchState.GoingToSource => ProcessGoingToSource(position, perception),
            FetchState.WorkingAtSource => ProcessWorkingAtSource(),
            FetchState.TakingResource => ProcessTakingResource(position, perception),
            FetchState.GoingToDestination => ProcessGoingToDestination(position, perception),
            FetchState.DepositingResource => ProcessDepositingResource(position, perception),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToSource(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Run the navigation sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () => CreateNavigationActivity(_sourceFacility, "source"),
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("FETCH", "Failed to reach source facility", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at source
        DebugLog("FETCH", $"Arrived at source: {SourceName}", 0);

        // If fetch duration > 0, go to working phase; otherwise skip straight to taking
        if (_fetchDuration > 0)
        {
            DebugLog("FETCH", $"Starting work phase (duration: {_fetchDuration} ticks)", 0);
            _machine.Fire(FetchTrigger.ArrivedAtSource);
            _workTimer = 0;
        }
        else
        {
            // Skip WorkingAtSource, go to ArrivedAtSource -> WorkingAtSource -> WorkComplete -> TakingResource
            // Actually, we need to go through ArrivedAtSource first, then immediately fire WorkComplete
            _machine.Fire(FetchTrigger.ArrivedAtSource);
            _machine.Fire(FetchTrigger.WorkComplete);
        }

        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessWorkingAtSource()
    {
        if (_owner == null)
        {
            return null;
        }

        _workTimer++;

        // Spend energy while working (physical labor)
        _energyNeed?.Restore(-ENERGYCOSTPERTICK);

        if (_workTimer < _fetchDuration)
        {
            // Still working, idle
            DebugLog("FETCH", $"Working... {_workTimer}/{_fetchDuration} ticks");
            return new IdleAction(_owner, this, Priority);
        }

        // Done working - fire transition to TakingResource
        DebugLog("FETCH", "Finished working, taking resource", 0);
        _machine.Fire(FetchTrigger.WorkComplete);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingResource(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check availability before creating sub-activity (only when _currentSubActivity is null)
        if (_currentSubActivity == null)
        {
            // Check how much is available using memory (auto-observes)
            int available = _owner.GetStorageItemCount(_sourceFacility, _itemId);
            if (available == 0)
            {
                DebugLog("FETCH", $"No {_itemId} available at {SourceName}", 0);
                Fail();
                return null;
            }
        }

        // Run the take sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                int available = _owner!.GetStorageItemCount(_sourceFacility, _itemId);
                int actualAmount = System.Math.Min(_desiredQuantity, available);
                var itemsToTake = new List<(string itemId, int quantity)> { (_itemId, actualAmount) };
                DebugLog("FETCH", $"Taking {actualAmount}x {_itemId} from {SourceName}", 0);
                return new TakeFromStorageActivity(_sourceFacility, itemsToTake, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("FETCH", $"Failed to take {_itemId} from storage", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to check result
                break;
        }

        // Check inventory to see how much we actually got
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        _actualQuantityTaken = inventory?.GetItemCount(_itemId) ?? 0;

        if (_actualQuantityTaken > 0)
        {
            Log.Print($"{_owner.Name}: Took {_actualQuantityTaken}x {_itemId} from {SourceName}");
            DebugLog("FETCH", $"Took {_actualQuantityTaken}x {_itemId}, transitioning to GoingToDestination", 0);
            _machine.Fire(FetchTrigger.ResourceTaken);
            return new IdleAction(_owner, this, Priority);
        }
        else
        {
            // Sub-activity completed but we have nothing - fail
            DebugLog("FETCH", $"Take completed but no {_itemId} in inventory", 0);
            Fail();
            return null;
        }
    }

    private EntityAction? ProcessGoingToDestination(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Run the navigation sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () => CreateNavigationActivity(_destinationFacility, "destination"),
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Failed to reach destination - keep item in inventory
                Log.Warn($"{_owner.Name}: Couldn't reach {DestinationName}, {_itemId} stays in inventory");
                Complete(); // Still consider it a success since we got the item
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at destination
        DebugLog("FETCH", $"Arrived at destination: {DestinationName}", 0);
        _machine.Fire(FetchTrigger.ArrivedAtDestination);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingResource(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // Check inventory before creating sub-activity (only when _currentSubActivity is null)
        if (_currentSubActivity == null)
        {
            var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
            if (inventory == null)
            {
                Complete();
                return null;
            }

            // Check how much we have in inventory
            int itemCount = inventory.GetItemCount(_itemId);
            if (itemCount == 0)
            {
                DebugLog("FETCH", $"No {_itemId} in inventory to deposit", 0);
                Complete();
                return null;
            }
        }

        // Run the deposit sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                var inventory = _owner!.SelfAsEntity().GetTrait<InventoryTrait>();
                int itemCount = inventory?.GetItemCount(_itemId) ?? 0;
                int amountToDeposit = System.Math.Min(itemCount, _actualQuantityTaken);
                var itemsToDeposit = new List<(string itemId, int quantity)> { (_itemId, amountToDeposit) };
                DebugLog("FETCH", $"Depositing {amountToDeposit}x {_itemId} to {DestinationName}", 0);
                return new DepositToStorageActivity(_destinationFacility, itemsToDeposit, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Deposit failed - items stay in inventory
                Log.Warn($"{_owner.Name}: {DestinationName} storage full, keeping {_itemId} in inventory");
                Complete();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to complete
                break;
        }

        // Check how much we still have in inventory (less means successful deposit)
        var inventory2 = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        int remaining = inventory2?.GetItemCount(_itemId) ?? 0;
        int deposited = _actualQuantityTaken - remaining;

        if (deposited > 0)
        {
            Log.Print($"{_owner.Name}: Stored {deposited}x {_itemId} at {DestinationName}");
            DebugLog("FETCH", $"Deposited {deposited}x {_itemId}, activity complete", 0);
        }

        Complete();
        return null;
    }

    private Activity CreateNavigationActivity(Facility targetFacility, string label)
    {
        DebugLog("FETCH", $"Starting navigation to {label}: {targetFacility.Owner?.BuildingName ?? targetFacility.Id}", 0);
        return new GoToFacilityActivity(targetFacility, Priority);
    }
}
