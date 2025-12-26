using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for sleeping at home during the night.
/// Completes automatically when daytime arrives.
/// Future: Will restore an energy/rest need.
/// </summary>
public class SleepActivity : Activity
{
    public override string DisplayName => "Sleeping";

    public SleepActivity(int priority = 0)
    {
        Priority = priority;

        // Sleeping reduces hunger decay to 1/4 normal rate
        NeedDecayMultipliers["hunger"] = 0.25f;

        // Future: could add other needs like "energy" restoration here
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if it's time to wake up
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        bool shouldSleep = gameTime.CurrentDayPhase is DayPhaseType.Night or
                           DayPhaseType.Dusk;

        if (!shouldSleep)
        {
            Complete();
            return null;
        }

        return new IdleAction(_owner, this, Priority);
    }
}
