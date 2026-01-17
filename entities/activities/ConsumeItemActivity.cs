using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for consuming food items from inventory or home storage.
/// Checks inventory first, then travels to home if needed.
/// </summary>
public class ConsumeItemActivity : Activity
{
    private readonly string _foodTag;
    private readonly Need _need;
    private readonly Building? _home;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    private GoToBuildingActivity? _goToPhase;
    private uint _consumptionTimer;
    private bool _isConsuming;
    private bool _itemConsumed;
    private IStorageContainer? _sourceStorage;

    public override string DisplayName => _isConsuming
        ? "Eating"
        : _home != null ? $"Going home to eat" : "Looking for food";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumeItemActivity"/> class.
    /// Create an activity to consume a food item.
    /// </summary>
    /// <param name="foodTag">Tag to identify food items (e.g., "food", "zombie_food").</param>
    /// <param name="need">The need to restore.</param>
    /// <param name="home">Home building with storage (can be null if only using inventory).</param>
    /// <param name="restoreAmount">Amount to restore the need.</param>
    /// <param name="consumptionDuration">Ticks to consume.</param>
    /// <param name="priority">Action priority.</param>
    public ConsumeItemActivity(
        string foodTag,
        Need need,
        Building? home,
        float restoreAmount,
        uint consumptionDuration,
        int priority = 0)
    {
        _foodTag = foodTag;
        _need = need;
        _home = home;
        _restoreAmount = restoreAmount;
        _consumptionDuration = consumptionDuration;
        Priority = priority;
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
                    _sourceStorage = inventory;
                    _isConsuming = true;
                }
            }
        }

        // Phase 1: Go to home if we haven't found food in inventory
        if (!_isConsuming && !_itemConsumed && _home != null)
        {
            // Check home still exists
            if (!GodotObject.IsInstanceValid(_home))
            {
                Log.Warn($"{_owner.Name}: Home destroyed while looking for food");
                Fail();
                return null;
            }

            // Initialize go-to phase if needed (targeting storage position)
            if (_goToPhase == null)
            {
                _goToPhase = new GoToBuildingActivity(_home, Priority, targetStorage: true);
                _goToPhase.Initialize(_owner);
            }

            // Run the navigation sub-activity
            var (result, action) = RunSubActivity(_goToPhase, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    // Fall through to check storage
                    break;
            }

            // We've arrived - check home storage for food using wrapper (auto-observes)
            if (_owner.StorageHasItemByTag(_home, _foodTag))
            {
                _isConsuming = true;

                // Note: _sourceStorage stays null for home storage case
                // We'll use the wrapper method TakeFromStorageByTag when consuming
            }
            else
            {
                // Memory was wrong or food was taken - memory is now updated
                Log.Warn($"{_owner.Name}: No food at home (memory updated)");
                Fail();
                return null;
            }
        }

        // If we still haven't found a source, fail
        if (!_isConsuming && _home == null)
        {
            Log.Warn($"{_owner.Name}: No food in inventory and no home to go to");
            Fail();
            return null;
        }

        // Phase 2: Consume the item
        if (_isConsuming)
        {
            // On first consumption tick, remove the item
            if (!_itemConsumed)
            {
                Item? consumed = null;

                // Check if food is from inventory (_sourceStorage set) or home storage
                if (_sourceStorage != null)
                {
                    // Consuming from inventory (already validated)
                    var foodItem = _sourceStorage.FindItemByTag(_foodTag);
                    if (foodItem?.Definition.Id != null)
                    {
                        consumed = _sourceStorage.RemoveItem(foodItem.Definition.Id, 1);
                    }
                }
                else if (_home != null)
                {
                    // Consuming from home storage - use wrapper method (auto-observes)
                    consumed = _owner.TakeFromStorageByTag(_home, _foodTag, 1);
                }

                if (consumed != null)
                {
                    _itemConsumed = true;
                }
                else
                {
                    // Food disappeared - if from home, memory is now updated
                    Log.Warn($"{_owner.Name}: Food disappeared before consumption");
                    Fail();
                    return null;
                }
            }

            _consumptionTimer++;

            if (_consumptionTimer >= _consumptionDuration)
            {
                // Apply restoration
                _need.Restore(_restoreAmount);
                Complete();
                return null;
            }

            // Still consuming, idle
            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }
}
