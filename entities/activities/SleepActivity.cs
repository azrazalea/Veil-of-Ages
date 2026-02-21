using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for sleeping. Restores energy and reduces hunger decay while sleeping.
/// Sleep targets 100% energy. Wakes when:
/// - Energy reaches 100%, OR
/// - Day phase starts (for normal sleep only, not emergency sleep), OR
/// - Energy reaches low threshold (for emergency sleep triggered by critical energy)
/// Can start during Dusk or Night phases. Can continue sleeping through Dawn
/// if energy hasn't reached 100% yet.
///
/// Critical non-energy needs (e.g., hunger) do NOT cause self-termination.
/// The priority system handles this: if food exists, the consumption trait
/// produces a higher-priority action that replaces sleep. If no food exists,
/// sleeping is the best option and waking would cause oscillation.
/// </summary>
public class SleepActivity : Activity
{
    // Energy restored per tick while sleeping
    // At 0.025/tick, sleeping for ~4000 ticks fully restores from 0 to 100
    private const float ENERGYRESTORERATE = 0.025f;

    // Energy threshold at which emergency sleep (priority < 0) wakes up.
    // Matches the energy need's "low" threshold so the entity won't immediately
    // re-trigger emergency sleep after waking.
    private const float LOWENERGYTHRESHOLD = 40f;

    private Need? _energyNeed;

    public override string DisplayName => L.Tr("activity.SLEEPING");

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

    protected override void OnInterrupted(InterruptionReason reason)
    {
        // Sleep can't be paused - cancel it entirely
        DebugLog("SLEEP", $"Sleep interrupted by {reason}, cancelling", 0);
        Fail();
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

        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        var currentPhase = gameTime.CurrentDayPhase;
        float currentEnergy = _energyNeed?.Value ?? 100f;

        // Wake conditions:
        // 1. Energy fully restored (target 100%)
        if (currentEnergy >= 100f)
        {
            Log.Print($"{_owner.Name}: Waking up - fully rested (energy: {currentEnergy:F1})");
            Complete();
            return null;
        }

        // 2. Day phase starts - wake unless this is emergency sleep (priority < 0)
        //    Emergency sleep must continue during the day until energy is restored,
        //    otherwise the entity enters a sleep-wake oscillation loop.
        if (currentPhase == DayPhaseType.Day && Priority >= 0)
        {
            Log.Print($"{_owner.Name}: Waking up - day started (energy: {currentEnergy:F1})");
            Complete();
            return null;
        }

        // 3. Emergency sleep (priority < 0) - wake when energy reaches low threshold.
        //    This ensures enough energy to not immediately re-trigger emergency sleep.
        if (Priority < 0 && currentEnergy >= LOWENERGYTHRESHOLD)
        {
            Log.Print($"{_owner.Name}: Waking up - energy restored above low threshold (energy: {currentEnergy:F1})");
            Complete();
            return null;
        }

        // Note: We do NOT self-terminate for critical non-energy needs (e.g., hunger).
        // The priority system handles this correctly:
        // - If the responsible trait CAN address the need (e.g., food exists),
        //   it produces a higher-priority action that wins the queue and replaces sleep.
        // - If it CANNOT (e.g., no food available), sleeping is the best option
        //   and waking would just cause an oscillation loop.

        // Continue sleeping during Dusk, Night, or Dawn (until Day or full energy)
        return new IdleAction(_owner, this, Priority);
    }
}
