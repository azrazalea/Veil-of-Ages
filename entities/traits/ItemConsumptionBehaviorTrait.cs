using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that handles need satisfaction by consuming items.
/// Checks inventory first, then home storage.
/// </summary>
public class ItemConsumptionBehaviorTrait : BeingTrait
{
    private readonly string _needId;
    private readonly string _foodTag;
    private readonly Func<Building?> _getHome;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    private Need? _need;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemConsumptionBehaviorTrait"/> class.
    /// Create a trait for item-based consumption.
    /// </summary>
    /// <param name="needId">The need to satisfy (e.g., "hunger").</param>
    /// <param name="foodTag">Tag to identify food items (e.g., "food", "zombie_food").</param>
    /// <param name="getHome">Function to get home building (may return null).</param>
    /// <param name="restoreAmount">Amount to restore when eating.</param>
    /// <param name="consumptionDuration">Ticks to spend eating.</param>
    public ItemConsumptionBehaviorTrait(
        string needId,
        string foodTag,
        Func<Building?> getHome,
        float restoreAmount = 60f,
        uint consumptionDuration = 244)
    {
        _needId = needId;
        _foodTag = foodTag;
        _getHome = getHome;
        _restoreAmount = restoreAmount;
        _consumptionDuration = consumptionDuration;
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        _need = _owner?.NeedsSystem?.GetNeed(_needId);

        if (_need == null)
        {
            Log.Error($"{_owner?.Name}: ItemConsumptionBehaviorTrait could not find need '{_needId}'");
        }
        else
        {
            Log.Print($"{_owner?.Name}: ItemConsumptionBehaviorTrait initialized for need '{_needId}' with food tag '{_foodTag}'");
        }
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (!IsInitialized || _owner == null || _need == null)
        {
            return null;
        }

        // If already moving, don't interrupt
        if (_owner.IsMoving())
        {
            return null;
        }

        // If we already have a consume activity running, let it handle things
        if (_owner.GetCurrentActivity() is ConsumeItemActivity)
        {
            return null;
        }

        // Check if need is low (hungry)
        if (!_need.IsLow())
        {
            return null;
        }

        // Check if we have food available
        if (!HasFoodAvailable())
        {
            // Only log occasionally to avoid spam
            if (GameController.CurrentTick % 200 == 0)
            {
                Log.Print($"{_owner.Name}: No {_foodTag} food available for '{_needId}'");
            }

            // TODO: Critical state handling
            return null;
        }

        // Determine priority based on hunger severity
        // Critical hunger: priority -1 (interrupts sleep)
        // Low hunger: priority 1 (doesn't interrupt sleep)
        int actionPriority = _need.IsCritical() ? -1 : 1;

        // Start consume activity
        var home = _getHome();
        var consumeActivity = new ConsumeItemActivity(
            _foodTag,
            _need,
            home,
            _restoreAmount,
            _consumptionDuration,
            priority: actionPriority);

        return new StartActivityAction(_owner, this, consumeActivity, priority: actionPriority);
    }

    /// <summary>
    /// Check if food is available in inventory or home storage.
    /// </summary>
    private bool HasFoodAvailable()
    {
        if (_owner == null)
        {
            return false;
        }

        // Check inventory
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory?.FindItemByTag(_foodTag) != null)
        {
            return true;
        }

        // Check home storage
        var home = _getHome();
        if (home != null && GodotObject.IsInstanceValid(home))
        {
            var homeStorage = home.GetStorage();
            if (homeStorage?.FindItemByTag(_foodTag) != null)
            {
                return true;
            }
        }

        return false;
    }
}
