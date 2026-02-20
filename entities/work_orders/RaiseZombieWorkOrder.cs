using System;
using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.WorkOrders;

/// <summary>
/// Work order for raising a zombie from a corpse on the necromancy altar.
/// Takes ~5 in-game hours (3,413 ticks at ~681.74 ticks/hour).
/// Grants necromancy and arcane_theory XP.
/// On completion: spawns a zombie at the altar position.
/// </summary>
public class RaiseZombieWorkOrder : WorkOrder
{
    // ~5 in-game hours at 681.74 ticks/hour ~ 3,409 ticks
    // Using 3,413 for a round-ish number
    private const int REQUIREDTICKS = 3413;

    private static readonly IReadOnlyList<(string SkillId, float XpPerTick)> XPREWARDS = new List<(string, float)>
    {
        ("necromancy", 0.025f),
        ("arcane_theory", 0.008f)
    };

    private const float ENERGYDRAIN = 0.015f;

    /// <summary>
    /// Gets or sets the facility where the zombie will spawn on completion.
    /// </summary>
    public Facility? SpawnFacility { get; set; }

    public RaiseZombieWorkOrder()
        : base(
            id: $"raise_zombie_{Guid.NewGuid():N}",
            type: "raise_zombie",
            requiredTicks: REQUIREDTICKS,
            xpRewards: XPREWARDS,
            energyDrainPerTick: ENERGYDRAIN)
    {
    }

    protected override void OnComplete(Being worker)
    {
        Log.Print($"{worker.Name}: Zombie raising complete!");

        // The actual zombie spawning will be handled by the activity/command
        // that processes the work order completion, since it requires main thread
        // access (scene tree manipulation). We just mark it complete here.
        // The WorkOnOrderActivity checks IsComplete and handles spawning.
    }
}
