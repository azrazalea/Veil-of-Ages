using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for eating at a building (farm, tavern, etc.).
/// Handles navigation to the building, consuming for a duration, then restoring a need.
///
/// Future variants for other consumption types:
/// - FeedOnBeingActivity: Vampire/predator feeding on living beings
/// - ConsumeItemActivity: Eating items from ground or inventory.
/// </summary>
public class EatActivity : Activity
{
    private readonly Building _target;
    private readonly Need _need;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    private GoToBuildingActivity? _goToPhase;
    private uint _consumptionTimer;
    private bool _isConsuming;

    public override string DisplayName => _isConsuming
        ? $"Eating at {_target.BuildingType}"
        : $"Going to {_target.BuildingType} to eat";

    /// <summary>
    /// Initializes a new instance of the <see cref="EatActivity"/> class.
    /// Create an activity to eat at a building.
    /// </summary>
    /// <param name="target">The building to eat at (farm, tavern, etc.)</param>
    /// <param name="need">The need to restore (hunger).</param>
    /// <param name="restoreAmount">How much to restore the need (e.g., 60).</param>
    /// <param name="consumptionDuration">How many ticks to consume (e.g., 244).</param>
    /// <param name="priority">Action priority (default 0).</param>
    public EatActivity(Building target, Need need, float restoreAmount, uint consumptionDuration, int priority = 0)
    {
        _target = target;
        _need = need;
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

        // Check if building still exists
        if (!GodotObject.IsInstanceValid(_target))
        {
            Fail();
            return null;
        }

        // Phase 1: Get to the building
        if (!_isConsuming)
        {
            // Initialize go-to phase if needed
            if (_goToPhase == null)
            {
                _goToPhase = new GoToBuildingActivity(_target, Priority);
                _goToPhase.Initialize(_owner);
            }

            // Check if navigation failed
            if (_goToPhase.State == ActivityState.Failed)
            {
                Fail();
                return null;
            }

            // Check if we've arrived
            if (_goToPhase.State == ActivityState.Completed)
            {
                _isConsuming = true;
                Log.Print($"{_owner.Name}: Started eating at {_target.BuildingType}");
            }
            else
            {
                // Still navigating
                return _goToPhase.GetNextAction(position, perception);
            }
        }

        // Phase 2: Consume
        if (_isConsuming)
        {
            _consumptionTimer++;

            if (_consumptionTimer >= _consumptionDuration)
            {
                // Apply effect
                _need.Restore(_restoreAmount);
                Log.Print($"{_owner.Name}: Finished eating, restored {_restoreAmount} {_need.DisplayName}");
                Complete();
                return null;
            }

            // Still consuming, idle
            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }
}
