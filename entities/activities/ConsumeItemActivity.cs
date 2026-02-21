using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for consuming food items from inventory or home storage.
/// Checks inventory first, then uses TakeFromStorageActivity to navigate to
/// storage (cross-area if needed) and fetch food into inventory.
/// Always consumes from inventory via ConsumeFromInventoryAction.
///
/// Phases:
/// 1. Check inventory for food — if found, skip to consuming
/// 2. Hidden entities — use ConsumeFromStorageByTagAction directly (no navigation)
/// 3. Fetch from storage — TakeFromStorageActivity(home, foodTag, 1) handles
///    cross-area navigation + local navigation + taking into inventory
/// 4. Consume — ConsumeFromInventoryAction + timer + need restoration.
/// </summary>
public class ConsumeItemActivity : Activity
{
    private readonly string _foodTag;
    private readonly Need _need;
    private readonly Facility? _homeStorage;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    private TakeFromStorageActivity? _fetchActivity;
    private uint _consumptionTimer;
    private bool _isConsuming;
    private bool _consumeActionIssued;
    private bool _itemConsumed;
    private string? _foodItemId;

    // Hidden entity special case: use ConsumeFromStorageByTagAction directly
    private bool _hiddenStorageConsumeIssued;

    public override string DisplayName => _isConsuming
        ? L.Tr("activity.EATING")
        : _homeStorage != null ? L.Tr("activity.GOING_HOME_TO_EAT") : L.Tr("activity.LOOKING_FOR_FOOD");
    public override Room? TargetRoom => _homeStorage?.ContainingRoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumeItemActivity"/> class.
    /// Create an activity to consume a food item.
    /// </summary>
    /// <param name="foodTag">Tag to identify food items (e.g., "food", "zombie_food").</param>
    /// <param name="need">The need to restore.</param>
    /// <param name="homeStorage">Home storage facility (can be null if only using inventory).</param>
    /// <param name="restoreAmount">Amount to restore the need.</param>
    /// <param name="consumptionDuration">Ticks to consume.</param>
    /// <param name="priority">Action priority.</param>
    public ConsumeItemActivity(
        string foodTag,
        Need need,
        Facility? homeStorage,
        float restoreAmount,
        uint consumptionDuration,
        int priority = 0)
    {
        _foodTag = foodTag;
        _need = need;
        _homeStorage = homeStorage;
        _restoreAmount = restoreAmount;
        _consumptionDuration = consumptionDuration;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();

        // If we were fetching, null the sub-activity to force re-navigation
        if (_fetchActivity != null && !_isConsuming)
        {
            _fetchActivity = null;
        }
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Phase 0: Try inventory first (immediate consumption, no travel)
        if (!_isConsuming && !_itemConsumed)
        {
            var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
            if (inventory != null)
            {
                var foodItem = inventory.FindItemByTag(_foodTag);
                if (foodItem != null)
                {
                    _foodItemId = foodItem.Definition.Id;
                    _isConsuming = true;
                    DebugLog("EATING", $"Found food in inventory ({foodItem.Definition.Name}), starting to eat", 0);
                }
            }
        }

        // Phase 0.5: Hidden entities skip navigation — they're already inside the building
        if (_owner.IsHidden && !_isConsuming && !_itemConsumed && _homeStorage != null)
        {
            if (!GodotObject.IsInstanceValid(_homeStorage))
            {
                DebugLog("EATING", "Hidden: Home building no longer valid", 0);
                Fail();
                return null;
            }

            if (_owner.StorageHasItemByTag(_homeStorage, _foodTag))
            {
                // Issue ConsumeFromStorageByTagAction directly — entity is inside the building
                if (!_hiddenStorageConsumeIssued)
                {
                    _hiddenStorageConsumeIssued = true;
                    _isConsuming = true;
                    var storageName = _homeStorage.ContainingRoom?.Name ?? "home";
                    DebugLog("EATING", $"Hidden: Found food at {storageName}, consuming directly", 0);
                    var storage = _homeStorage;

                    return new ConsumeFromStorageByTagAction(
                        _owner, this, storage, _foodTag, 1, Priority);
                }

                // After action executed, food should be in inventory now
                // (ConsumeFromStorageByTagAction adds to inventory if entity has one)
                var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
                var foodItem = inventory?.FindItemByTag(_foodTag);
                if (foodItem != null)
                {
                    _foodItemId = foodItem.Definition.Id;
                }
            }
            else
            {
                Log.Warn($"{_owner.Name}: Hidden but no food at {_homeStorage.ContainingRoom?.Name ?? "home"}");
                Fail();
                return null;
            }
        }

        // Phase 1: Fetch food from storage using TakeFromStorageActivity
        // Handles cross-area navigation + local navigation + taking into inventory
        if (!_isConsuming && !_itemConsumed && _homeStorage != null)
        {
            if (!GodotObject.IsInstanceValid(_homeStorage))
            {
                Log.Warn($"{_owner.Name}: Home destroyed while looking for food");
                Fail();
                return null;
            }

            if (_fetchActivity == null)
            {
                var storage = _homeStorage;
                _fetchActivity = new TakeFromStorageActivity(storage, _foodTag, 1, Priority);
                _fetchActivity.Initialize(_owner);
                DebugLog("EATING", $"Starting fetch from {_homeStorage.ContainingRoom?.Name ?? "home"}", 0);
            }

            var (result, action) = RunSubActivity(_fetchActivity, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    DebugLog("EATING", $"Failed to fetch food from {_homeStorage.ContainingRoom?.Name ?? "home"}", 0);
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    break;
            }

            // TakeFromStorageActivity completed — food should now be in inventory
            var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
            var foodItem = inventory?.FindItemByTag(_foodTag);
            if (foodItem != null)
            {
                _foodItemId = foodItem.Definition.Id;
                _isConsuming = true;
                DebugLog("EATING", $"Fetched {foodItem.Definition.Name} from {_homeStorage.ContainingRoom?.Name ?? "home"}, starting to eat", 0);
            }
            else
            {
                Log.Warn($"{_owner.Name}: Fetch completed but no food in inventory");
                Fail();
                return null;
            }
        }

        // If we still haven't found a source, fail
        if (!_isConsuming && _homeStorage == null)
        {
            Log.Warn($"{_owner.Name}: No food in inventory and no home to go to");
            Fail();
            return null;
        }

        // Phase 2: Consume the item from inventory
        if (_isConsuming)
        {
            // On first tick of consuming, issue the consume action
            if (!_consumeActionIssued && _foodItemId != null)
            {
                _consumeActionIssued = true;
                return new ConsumeFromInventoryAction(
                    _owner,
                    this,
                    _foodItemId,
                    1,
                    Priority);
            }

            // Mark item as consumed after action has been issued
            if (!_itemConsumed)
            {
                _itemConsumed = true;
            }

            _consumptionTimer++;

            if (_consumptionTimer >= _consumptionDuration)
            {
                _need.Restore(_restoreAmount);
                DebugLog("EATING", $"Finished eating (hunger restored by {_restoreAmount})", 0);
                Complete();
                return null;
            }

            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }
}
