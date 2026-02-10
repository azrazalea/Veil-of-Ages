using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities;

public interface IEntity<TraitType> : ISensable
    where TraitType : Trait
{
    Area? GridArea { get; }
    SortedSet<TraitType> Traits { get; }

    void AddTrait<T>(int priority)
        where T : TraitType, new()
    {
        var trait = new T
        {
            Priority = priority
        };

        Traits.Add(trait);

        // Trait will be properly initialized by Being._Ready() when it processes all traits
    }

    void AddTrait(Trait trait, int priority)
    {
        trait.Priority = priority;
        Traits.Add((TraitType)trait);

        // Trait will be properly initialized by Being._Ready() when it processes all traits
    }

    ///
    /// <returns></returns><summary>
    /// Add a trait to the entity and enqueue it for initialization if a queue is provided.
    /// This is a simplified way to add traits during initialization without needing separate creation,
    /// addition, and initialization steps.
    ///
    /// Example usage:
    /// // Simple trait addition (replaces 3+ lines of code)
    /// _owner?.selfAsEntity().AddTraitToQueue.<LivingTrait>(0, initQueue);
    /// </summary>
    /// <typeparam name="T">The type of trait to add</typeparam>
    /// <param name="priority">The priority of the trait</param>
    /// <param name="initQueue">Optional initialization queue</param>
    /// <returns>The created trait instance</returns>
    T AddTraitToQueue<T>(int priority, Queue<TraitType>? initQueue = null)
        where T : TraitType, new()
    {
        var trait = new T
        {
            Priority = priority
        };

        Traits.Add(trait);

        // Add to initialization queue if provided, otherwise _Ready() will handle it
        initQueue?.Enqueue(trait);

        return trait;
    }

    /// <summary>
    /// Add a trait to the entity and enqueue it for initialization if a queue is provided.
    /// This is a simplified way to add pre-constructed traits during initialization without
    /// needing separate addition and initialization steps.
    ///
    /// Example usage:
    /// // For a trait that needs custom construction
    /// var consumptionTrait = new ConsumptionBehaviorTrait("hunger", new FarmSourceIdentifier(), ...);
    /// _owner?.selfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);.
    /// </summary>
    /// <param name="trait">The trait instance to add.</param>
    /// <param name="priority">The priority of the trait.</param>
    /// <param name="initQueue">Optional initialization queue.</param>
    /// <returns>The added trait instance.</returns>
    TraitType AddTraitToQueue(TraitType trait, int priority, Queue<TraitType>? initQueue = null)
    {
        trait.Priority = priority;
        Traits.Add(trait);

        // Add to initialization queue if provided, otherwise _Ready() will handle it
        initQueue?.Enqueue(trait);

        return trait;
    }

    bool RemoveTrait(TraitType trait)
    {
        return Traits.Remove(trait);
    }

    T? GetTrait<T>()
        where T : Trait
    {
        foreach (var trait in Traits)
        {
            if (trait is T typedTrait)
            {
                return typedTrait;
            }
        }

        return default;
    }

    bool HasTrait<T>()
        where T : Trait
    {
        foreach (var trait in Traits)
        {
            if (trait is T)
            {
                return true;
            }
        }

        return false;
    }

    // Event system for traits
    void OnTraitEvent(string eventName, params object[] args)
    {
        foreach (var trait in Traits)
        {
            trait.OnEvent(eventName, args);
        }
    }
}
