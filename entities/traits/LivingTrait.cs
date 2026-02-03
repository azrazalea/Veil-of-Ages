using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Configuration for a single need, used to deserialize from JSON.
/// DEPRECATED: Needs should now be defined at entity level in BeingDefinition.Needs.
/// This record is kept for backwards compatibility but LivingTrait no longer creates needs.
/// </summary>
public record NeedConfiguration(
    string Id,
    string Name,
    float Initial = 100f,
    float DecayRate = 0.01f,
    float Critical = 10f,
    float Low = 30f,
    float High = 90f);

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
