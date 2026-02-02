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
/// Activity for fetching resources from one building (source) and bringing them to another (destination).
/// Used by bakers to fetch water from the well, or any entity that needs to transport items between buildings.
///
/// Phases:
/// 1. Navigate to source building
/// 2. Work at source (if FetchDuration > 0, e.g., drawing water from a well)
/// 3. Take items from source storage into inventory
/// 4. Navigate to destination building
/// 5. Deposit items from inventory to destination storage.
/// </summary>
public class FetchResourceActivity : Activity
{
    private enum FetchPhase
    {
        GoingToSource,
        WorkingAtSource,
        TakingResource,
        GoingToDestination,
        DepositingResource,
        Done
    }

    private readonly Building _sourceBuilding;
    private readonly Building _destinationBuilding;
    private readonly string _itemId;
    private readonly int _desiredQuantity;

    private GoToBuildingActivity? _goToSourcePhase;
    private GoToBuildingActivity? _goToDestinationPhase;
    private FetchPhase _currentPhase = FetchPhase.GoingToSource;
    private int _actualQuantityTaken;
    private TakeFromStorageAction? _takeAction;
    private DepositToStorageAction? _depositAction;

    // Work phase tracking
    private uint _workTimer;
    private uint _fetchDuration;
    private Need? _energyNeed;

    // Energy cost per tick while working (same as DrawWaterActivity)
    private const float ENERGYCOSTPERTICK = 0.02f;

    public override string DisplayName => _currentPhase switch
    {
        FetchPhase.GoingToSource => $"Going to get {_itemId}",
        FetchPhase.WorkingAtSource => $"Fetching {_itemId}",
        FetchPhase.TakingResource => $"Taking {_itemId}",
        FetchPhase.GoingToDestination => $"Bringing {_itemId}",
        FetchPhase.DepositingResource => $"Storing {_itemId}",
        _ => $"Fetching {_itemId}"
    };
    public override Building? TargetBuilding => _currentPhase <= FetchPhase.TakingResource ? _sourceBuilding : _destinationBuilding;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        // When working at source, delegate to the navigation sub-activity for goal positions
        if (_currentPhase == FetchPhase.WorkingAtSource && _goToSourcePhase != null)
        {
            return _goToSourcePhase.GetAlternativeGoalPositions(entity);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchResourceActivity"/> class.
    /// Create an activity to fetch resources from one building to another.
    /// </summary>
    /// <param name="sourceBuilding">The building to take resources from (e.g., Well).</param>
    /// <param name="destinationBuilding">The building to deposit resources to (e.g., Bakery).</param>
    /// <param name="itemId">The item ID to fetch (e.g., "water").</param>
    /// <param name="desiredQuantity">How many items to fetch (will take up to this amount if available).</param>
    /// <param name="priority">Action priority.</param>
    public FetchResourceActivity(
        Building sourceBuilding,
        Building destinationBuilding,
        string itemId,
        int desiredQuantity,
        int priority = 0)
    {
        _sourceBuilding = sourceBuilding;
        _destinationBuilding = destinationBuilding;
        _itemId = itemId;
        _desiredQuantity = desiredQuantity;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get fetch duration from source building's storage configuration
        _fetchDuration = _sourceBuilding.GetStorageFetchDuration();

        // Get energy need for direct energy cost while working
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");

        DebugLog("FETCH", $"Started FetchResourceActivity: {_desiredQuantity}x {_itemId} from {_sourceBuilding.BuildingName} to {_destinationBuilding.BuildingName}, fetch duration: {_fetchDuration} ticks", 0);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if buildings still exist
        if (!GodotObject.IsInstanceValid(_sourceBuilding))
        {
            DebugLog("FETCH", "Source building no longer valid", 0);
            Fail();
            return null;
        }

        if (!GodotObject.IsInstanceValid(_destinationBuilding))
        {
            DebugLog("FETCH", "Destination building no longer valid", 0);
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            FetchPhase.GoingToSource => ProcessGoingToSource(position, perception),
            FetchPhase.WorkingAtSource => ProcessWorkingAtSource(),
            FetchPhase.TakingResource => ProcessTakingResource(),
            FetchPhase.GoingToDestination => ProcessGoingToDestination(position, perception),
            FetchPhase.DepositingResource => ProcessDepositingResource(),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToSource(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to phase if needed (targeting storage position)
        if (_goToSourcePhase == null)
        {
            _goToSourcePhase = new GoToBuildingActivity(_sourceBuilding, Priority, targetStorage: true);
            _goToSourcePhase.Initialize(_owner);
            DebugLog("FETCH", $"Starting navigation to source: {_sourceBuilding.BuildingName}", 0);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToSourcePhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("FETCH", "Failed to reach source building", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at source
        DebugLog("FETCH", $"Arrived at source: {_sourceBuilding.BuildingName}", 0);

        // If fetch duration > 0, go to working phase; otherwise skip straight to taking
        if (_fetchDuration > 0)
        {
            DebugLog("FETCH", $"Starting work phase (duration: {_fetchDuration} ticks)", 0);
            _currentPhase = FetchPhase.WorkingAtSource;
            _workTimer = 0;
        }
        else
        {
            _currentPhase = FetchPhase.TakingResource;
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

        // Done working - now take the resource
        DebugLog("FETCH", "Finished working, taking resource", 0);
        _currentPhase = FetchPhase.TakingResource;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingResource()
    {
        if (_owner == null)
        {
            return null;
        }

        // If we already have a take action in progress, check if it completed
        if (_takeAction != null)
        {
            // Action was executed - check result via the callback-set value
            if (_actualQuantityTaken > 0)
            {
                // Success - move to next phase
                Log.Print($"{_owner.Name}: Took {_actualQuantityTaken}x {_itemId} from {_sourceBuilding.BuildingName}");
                DebugLog("FETCH", $"Took {_actualQuantityTaken}x {_itemId}, transitioning to GoingToDestination", 0);
                _takeAction = null;
                _currentPhase = FetchPhase.GoingToDestination;
                return new IdleAction(_owner, this, Priority);
            }
            else
            {
                // Action failed
                DebugLog("FETCH", $"Failed to take {_itemId} from storage", 0);
                _takeAction = null;
                Fail();
                return null;
            }
        }

        // Check how much is available using memory (auto-observes)
        int available = _owner.GetStorageItemCount(_sourceBuilding, _itemId);
        if (available == 0)
        {
            DebugLog("FETCH", $"No {_itemId} available at {_sourceBuilding.BuildingName}", 0);
            Fail();
            return null;
        }

        // Take up to desired quantity, or all available if less
        int actualAmount = System.Math.Min(_desiredQuantity, available);

        // Create TakeFromStorageAction with callback to track result
        _takeAction = new TakeFromStorageAction(
            _owner,
            this,
            _sourceBuilding,
            _itemId,
            actualAmount,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var takeAction = (TakeFromStorageAction)action;
                _actualQuantityTaken = takeAction.ActualQuantity;
            }
        };

        return _takeAction;
    }

    private EntityAction? ProcessGoingToDestination(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to phase if needed (targeting storage position)
        if (_goToDestinationPhase == null)
        {
            _goToDestinationPhase = new GoToBuildingActivity(_destinationBuilding, Priority, targetStorage: true);
            _goToDestinationPhase.Initialize(_owner);
            DebugLog("FETCH", $"Starting navigation to destination: {_destinationBuilding.BuildingName}", 0);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToDestinationPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Failed to reach destination - keep item in inventory
                Log.Warn($"{_owner.Name}: Couldn't reach {_destinationBuilding.BuildingName}, {_itemId} stays in inventory");
                Complete(); // Still consider it a success since we got the item
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at destination
        DebugLog("FETCH", $"Arrived at destination: {_destinationBuilding.BuildingName}", 0);
        _currentPhase = FetchPhase.DepositingResource;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingResource()
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // If we already have a deposit action in progress, check if it completed
        if (_depositAction != null)
        {
            // Action was executed - check result
            int deposited = _depositAction.ActualDeposited;
            if (deposited > 0)
            {
                Log.Print($"{_owner.Name}: Stored {deposited}x {_itemId} at {_destinationBuilding.BuildingName}");
                DebugLog("FETCH", $"Deposited {deposited}x {_itemId}, activity complete", 0);
            }
            else
            {
                // Deposit failed - items stay in inventory
                Log.Warn($"{_owner.Name}: {_destinationBuilding.BuildingName} storage full, keeping {_itemId} in inventory");
            }

            _depositAction = null;
            Complete();
            return null;
        }

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

        // Create DepositToStorageAction to transfer items
        // Take up to what we actually took from source
        int amountToDeposit = System.Math.Min(itemCount, _actualQuantityTaken);

        _depositAction = new DepositToStorageAction(
            _owner,
            this,
            _destinationBuilding,
            _itemId,
            amountToDeposit,
            Priority);

        return _depositAction;
    }
}
