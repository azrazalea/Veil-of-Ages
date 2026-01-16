using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;

namespace VeilOfAges.Entities.Traits;

public class LivingTrait : BeingTrait
{
    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Initialize needs in the needs system
        var needsSystem = owner.NeedsSystem;
        if (needsSystem != null)
        {
            // Hunger: decays at 0.02/tick, critical at 15, low at 40
            var hunger = new Need("hunger", "Hunger", 75f, 0.02f, 15f, 40f, 90f);
            needsSystem.AddNeed(hunger);

            // Energy: decays slower (0.008/tick), restored by sleep
            // At 8 ticks/sec, this is ~1562 ticks (195 sec) from 100 to 0
            // With 10-hour days, energy will drop significantly by nightfall
            var energy = new Need("energy", "Energy", 100f, 0.008f, 20f, 40f, 80f);
            needsSystem.AddNeed(energy);
        }

        Log.Print($"{owner.Name}: Living trait initialized with hunger and energy needs");
    }
}
