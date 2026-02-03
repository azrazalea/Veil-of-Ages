using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Marker trait indicating this entity is an undead with hunger needs (like zombies).
/// Zombies have different needs than living beings (brain hunger instead of regular hunger).
///
/// NOTE: Needs are now defined at the entity level in BeingDefinition.Needs, not in this trait.
/// This trait serves as a marker for "undead with hunger" checks and zombie-specific behaviors.
/// </summary>
public class ZombieLivingTrait : BeingTrait
{
    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Needs are now initialized from BeingDefinition.Needs in GenericBeing._Ready()
        // This trait is now just a marker for "this entity is an undead with hunger needs"
        Log.Print($"{owner.Name}: ZombieLivingTrait initialized (marker trait - needs defined at entity level)");
    }
}
