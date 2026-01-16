using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for working at a field/farm during daytime.
/// Handles navigation to the workplace and idling there (working).
/// Completes when work duration expires or day phase ends.
/// Drains energy while working (restored by sleeping).
/// </summary>
public class WorkFieldActivity : Activity
{
    // Energy cost per tick while actively working
    // At 0.05/tick, working for 400 ticks costs 20 energy
    private const float ENERGYCOSTPERTICK = 0.05f;

    private readonly Building _workplace;
    private readonly uint _workDuration;

    private GoToBuildingActivity? _goToPhase;
    private uint _workTimer;
    private bool _isWorking;
    private Need? _energyNeed;

    public override string DisplayName => _isWorking
        ? $"Working at {_workplace.BuildingType}"
        : $"Going to work";

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkFieldActivity"/> class.
    /// Create an activity to work at a building.
    /// </summary>
    /// <param name="workplace">The building to work at (farm, etc.)</param>
    /// <param name="workDuration">How many ticks to work before taking a break.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public WorkFieldActivity(Building workplace, uint workDuration, int priority = 0)
    {
        _workplace = workplace;
        _workDuration = workDuration;
        Priority = priority;

        // Working makes you hungry faster
        NeedDecayMultipliers["hunger"] = 1.2f;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get energy need - work directly costs energy (not via decay multiplier)
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if workplace still exists
        if (!GodotObject.IsInstanceValid(_workplace))
        {
            Fail();
            return null;
        }

        // Check if work time has ended (day phase changed to dusk/night)
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            Log.Print($"{_owner.Name}: Work time ended, heading home");
            Complete();
            return null;
        }

        // Phase 1: Get to the workplace
        if (!_isWorking)
        {
            // Initialize go-to phase if needed
            if (_goToPhase == null)
            {
                _goToPhase = new GoToBuildingActivity(_workplace, Priority);
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
                _isWorking = true;
                Log.Print($"{_owner.Name}: Started working at {_workplace.BuildingType}");
            }
            else
            {
                // Still navigating
                return _goToPhase.GetNextAction(position, perception);
            }
        }

        // Phase 2: Work (idle at location)
        if (_isWorking)
        {
            _workTimer++;

            // Directly spend energy while working
            _energyNeed?.Restore(-ENERGYCOSTPERTICK);

            if (_workTimer >= _workDuration)
            {
                Log.Print($"{_owner.Name}: Completed work shift at {_workplace.BuildingType}");
                Complete();
                return null;
            }

            // Still working, idle
            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }
}
