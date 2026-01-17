using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for sleeping at home during the night.
/// Completes automatically when daytime arrives.
/// Restores energy and reduces hunger decay while sleeping.
/// </summary>
public class SleepActivity : Activity
{
    // Energy restored per tick while sleeping
    // At 0.15/tick, sleeping for ~670 ticks fully restores from 0 to 100
    // This is roughly 84 seconds at 8 ticks/sec
    private const float ENERGYRESTORERATE = 0.15f;

    private Need? _energyNeed;

    public override string DisplayName => "Sleeping";

    public SleepActivity(int priority = -1)
    {
        Priority = priority;

        // Sleeping reduces hunger decay to 1/4 normal rate
        NeedDecayMultipliers["hunger"] = 0.25f;

        // Energy doesn't decay while sleeping (we're restoring it instead)
        NeedDecayMultipliers["energy"] = 0f;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get the energy need for restoration
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Restore energy each tick
        _energyNeed?.Restore(ENERGYRESTORERATE);

        // Check if it's time to wake up
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        bool shouldSleep = gameTime.CurrentDayPhase is DayPhaseType.Night or
                           DayPhaseType.Dusk;

        if (!shouldSleep)
        {
            Log.Print($"{_owner.Name}: Waking up (energy: {_energyNeed?.Value:F1})");
            Complete();
            return null;
        }

        return new IdleAction(_owner, this, Priority);
    }
}
