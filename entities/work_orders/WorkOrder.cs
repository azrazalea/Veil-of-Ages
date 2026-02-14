using System.Collections.Generic;

namespace VeilOfAges.Entities.WorkOrders;

/// <summary>
/// Base class for persistent work orders that live on facilities.
/// Progress persists across interruptions — the work order stays on the facility,
/// not on the entity. Any entity with the right skills can continue the work.
/// </summary>
public abstract class WorkOrder
{
    /// <summary>
    /// Gets unique identifier for this work order instance.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the type identifier (e.g., "raise_zombie").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets or sets current progress in ticks.
    /// </summary>
    public int ProgressTicks { get; set; }

    /// <summary>
    /// Gets total ticks required to complete.
    /// </summary>
    public int RequiredTicks { get; }

    /// <summary>
    /// Gets a value indicating whether gets whether this work order is complete.
    /// </summary>
    public bool IsComplete => ProgressTicks >= RequiredTicks;

    /// <summary>
    /// Gets XP rewards granted per tick of work. Multiple skills can be rewarded.
    /// </summary>
    public IReadOnlyList<(string SkillId, float XpPerTick)> XpRewards { get; }

    /// <summary>
    /// Gets energy drain per tick of work.
    /// </summary>
    public float EnergyDrainPerTick { get; }

    /// <summary>
    /// Gets arbitrary parameters for the work order.
    /// </summary>
    public Dictionary<string, object> Parameters { get; } = new ();

    protected WorkOrder(string id, string type, int requiredTicks, IReadOnlyList<(string SkillId, float XpPerTick)> xpRewards, float energyDrainPerTick)
    {
        Id = id;
        Type = type;
        RequiredTicks = requiredTicks;
        XpRewards = xpRewards;
        EnergyDrainPerTick = energyDrainPerTick;
    }

    /// <summary>
    /// Advance the work order by one tick.
    /// Increments progress, grants XP to the worker, and drains energy.
    /// </summary>
    public void Advance(Being worker)
    {
        if (IsComplete)
        {
            return;
        }

        ProgressTicks++;

        // Grant XP for each skill
        foreach (var (skillId, xpPerTick) in XpRewards)
        {
            worker.SkillSystem?.GainXp(skillId, xpPerTick);
        }

        // Drain energy
        var energyNeed = worker.NeedsSystem?.GetNeed("energy");
        energyNeed?.Restore(-EnergyDrainPerTick);

        if (IsComplete)
        {
            OnComplete(worker);
        }
    }

    /// <summary>
    /// Called when the work order completes. Subclass implements completion logic
    /// (e.g., spawning a zombie, producing items).
    /// </summary>
    protected abstract void OnComplete(Being worker);

    /// <summary>
    /// Get a human-readable progress string.
    /// </summary>
    public string GetProgressString()
    {
        float percent = RequiredTicks > 0 ? (float)ProgressTicks / RequiredTicks * 100f : 100f;
        return $"{percent:F0}%";
    }
}
