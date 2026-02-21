using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Marker trait indicating this entity is a living being.
/// Living entities have biological needs (hunger, energy) and can die from starvation or exhaustion.
///
/// NOTE: Needs are now defined at the entity level in BeingDefinition.Needs, not in this trait.
/// This trait serves as a marker for "is alive" checks and can be extended for living-specific behaviors.
/// </summary>
public class LivingTrait : BeingTrait
{
    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Needs are now initialized from BeingDefinition.Needs in GenericBeing._Ready()
        // This trait is now just a marker for "this entity is alive"
        Log.Print($"{owner.Name}: LivingTrait initialized (marker trait - needs defined at entity level)");
    }
}
