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
/// Uses ConsumeFromInventoryAction for inventory food and
/// ConsumeFromStorageByTagAction for home storage food.
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
    private bool _consumeActionIssued;
    private bool _itemConsumed;
    private bool _isFromInventory;
    private string? _foodItemId; // Item ID for inventory consumption (found during validation)

    public override string DisplayName => _isConsuming
        ? "Eating"
        : _home != null ? $"Going home to eat" : "Looking for food";
    public override Building? TargetBuilding => _home;

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
                    _isFromInventory = true;
                    _foodItemId = foodItem.Definition.Id;
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
                _isFromInventory = false;
                _isConsuming = true;
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
            // On first tick of consuming, issue the consume action
            if (!_consumeActionIssued)
            {
                _consumeActionIssued = true;

                if (_isFromInventory && _foodItemId != null)
                {
                    // Consuming from inventory - use ConsumeFromInventoryAction
                    return new ConsumeFromInventoryAction(
                        _owner,
                        this,
                        _foodItemId,
                        1,
                        Priority);
                }
                else if (_home != null)
                {
                    // Consuming from home storage - use ConsumeFromStorageByTagAction
                    return new ConsumeFromStorageByTagAction(
                        _owner,
                        this,
                        _home,
                        _foodTag,
                        1,
                        Priority);
                }
                else
                {
                    // No valid source - should not happen but handle gracefully
                    Log.Warn($"{_owner.Name}: No valid food source for consumption");
                    Fail();
                    return null;
                }
            }

            // After action has been issued and presumably executed, mark item as consumed
            if (!_itemConsumed)
            {
                _itemConsumed = true;
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
