using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for fetching resources from one building (source) and bringing them to another (destination).
/// Used by bakers to fetch water from the well, or any entity that needs to transport items between buildings.
///
/// Phases:
/// 1. Navigate to source building
/// 2. Take items from source storage into inventory
/// 3. Navigate to destination building
/// 4. Deposit items from inventory to destination storage.
/// </summary>
public class FetchResourceActivity : Activity
{
    private enum FetchPhase
    {
        GoingToSource,
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

    public override string DisplayName => _currentPhase switch
    {
        FetchPhase.GoingToSource => $"Going to get {_itemId}",
        FetchPhase.TakingResource => $"Taking {_itemId}",
        FetchPhase.GoingToDestination => $"Bringing {_itemId}",
        FetchPhase.DepositingResource => $"Storing {_itemId}",
        _ => $"Fetching {_itemId}"
    };

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
        DebugLog("FETCH", $"Started FetchResourceActivity: {_desiredQuantity}x {_itemId} from {_sourceBuilding.BuildingName} to {_destinationBuilding.BuildingName}", 0);
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
        _currentPhase = FetchPhase.TakingResource;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingResource()
    {
        if (_owner == null)
        {
            return null;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Log.Warn($"{_owner.Name}: No inventory to carry {_itemId}");
            Fail();
            return null;
        }

        // Check how much is available using wrapper (auto-observes)
        int available = _owner.GetStorageItemCount(_sourceBuilding, _itemId);
        if (available == 0)
        {
            DebugLog("FETCH", $"No {_itemId} available at {_sourceBuilding.BuildingName}", 0);
            Fail();
            return null;
        }

        // Take up to desired quantity, or all available if less
        int actualAmount = System.Math.Min(_desiredQuantity, available);

        // Use wrapper method - auto-observes storage contents after taking
        var item = _owner.TakeFromStorage(_sourceBuilding, _itemId, actualAmount);
        if (item != null)
        {
            // Record how much was actually taken from source
            _actualQuantityTaken = item.Quantity;

            if (inventory.AddItem(item))
            {
                Log.Print($"{_owner.Name}: Took {_actualQuantityTaken}x {_itemId} from {_sourceBuilding.BuildingName}");
                DebugLog("FETCH", $"Took {_actualQuantityTaken}x {_itemId}, transitioning to GoingToDestination", 0);
            }
            else
            {
                // Inventory full, put it back using wrapper
                _owner.PutInStorage(_sourceBuilding, item);
                Log.Warn($"{_owner.Name}: Inventory full, leaving {_itemId} at {_sourceBuilding.BuildingName}");
                Fail();
                return null;
            }
        }
        else
        {
            DebugLog("FETCH", $"Failed to take {_itemId} from storage", 0);
            Fail();
            return null;
        }

        _currentPhase = FetchPhase.GoingToDestination;
        return new IdleAction(_owner, this, Priority);
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

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Complete();
            return null;
        }

        // Transfer the actual amount we took from source to destination storage
        // Use _actualQuantityTaken instead of what's currently in inventory
        // in case some was consumed or dropped elsewhere
        int itemCount = inventory.GetItemCount(_itemId);
        if (itemCount == 0)
        {
            DebugLog("FETCH", $"No {_itemId} in inventory to deposit", 0);
            Complete();
            return null;
        }

        // Remove from inventory (take up to what we actually took from source)
        var item = inventory.RemoveItem(_itemId, System.Math.Min(itemCount, _actualQuantityTaken));
        if (item != null)
        {
            // Use wrapper method - auto-observes storage contents
            if (_owner.PutInStorage(_destinationBuilding, item))
            {
                Log.Print($"{_owner.Name}: Stored {item.Quantity}x {_itemId} at {_destinationBuilding.BuildingName}");
                DebugLog("FETCH", $"Deposited {item.Quantity}x {_itemId}, activity complete", 0);
            }
            else
            {
                // Destination storage full, keep in inventory
                inventory.AddItem(item);
                Log.Warn($"{_owner.Name}: {_destinationBuilding.BuildingName} storage full, keeping {_itemId} in inventory");
            }
        }

        Complete();
        return null;
    }
}
