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
/// - Day phase starts (must wake for daytime regardless of energy), OR
/// - A night job trait requests waking (via WakeRequested flag)
/// Can start during Dusk or Night phases. Can continue sleeping through Dawn
/// if energy hasn't reached 100% yet.
/// </summary>
public class SleepActivity : Activity
{
    // Energy restored per tick while sleeping
    // At 0.025/tick, sleeping for ~4000 ticks fully restores from 0 to 100
    private const float ENERGYRESTORERATE = 0.025f;

    private Need? _energyNeed;

    /// <summary>
    /// Gets or sets a value indicating whether something has requested this entity wake up.
    /// Set by traits (e.g., NecromancyStudyJobTrait) that need the entity to wake for work.
    /// </summary>
    public bool WakeRequested { get; set; }

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

        // 2. Day phase starts - must wake regardless of energy
        if (currentPhase == DayPhaseType.Day)
        {
            Log.Print($"{_owner.Name}: Waking up - day started (energy: {currentEnergy:F1})");
            Complete();
            return null;
        }

        // 3. A trait requested waking (e.g., night job starting)
        if (WakeRequested)
        {
            WakeRequested = false;
            Log.Print($"{_owner.Name}: Waking up - wake requested (energy: {currentEnergy:F1})");
            Complete();
            return null;
        }

        // Continue sleeping during Dusk, Night, or Dawn (until Day or full energy)
        return new IdleAction(_owner, this, Priority);
    }
}
