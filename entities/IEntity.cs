using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities
{
    public interface IEntity<TraitType> : ISensable where TraitType : Trait
    {
        public Area? GridArea { get; }
        public SortedSet<TraitType> _traits { get; }

        public void AddTrait<T>(int priority) where T : TraitType, new()
        {
            var trait = new T
            {
                Priority = priority
            };

            _traits.Add(trait);

            // Note: Direct initialization no longer needed as the queue-based system will handle it
            // Traits will be properly initialized when added during trait initialization
            // Only initialize directly if we're outside of the initialization process
            if (GridArea != null && !trait.IsInitialized)
            {
                trait.Initialize();
            }
        }

        public void AddTrait(Trait trait, int priority)
        {
            trait.Priority = priority;
            _traits.Add((TraitType)trait);

            // Note: Direct initialization no longer needed as the queue-based system will handle it
            // Traits will be properly initialized when added during trait initialization
            // Only initialize directly if we're outside of the initialization process
            if (GridArea != null && trait is TraitType typedTrait && !typedTrait.IsInitialized)
            {
                trait.Initialize();
            }
        }

        /// <summary>
        /// Add a trait to the entity and enqueue it for initialization if a queue is provided.
        /// This is a simplified way to add traits during initialization without needing separate creation,
        /// addition, and initialization steps.
        /// 
        /// Example usage:
        /// // Simple trait addition (replaces 3+ lines of code)
        /// _owner?.selfAsEntity().AddTraitToQueue<LivingTrait>(0, initQueue);
        /// </summary>
        /// <typeparam name="T">The type of trait to add</typeparam>
        /// <param name="priority">The priority of the trait</param>
        /// <param name="initQueue">Optional initialization queue</param>
        /// <returns>The created trait instance</returns>
        public T AddTraitToQueue<T>(int priority, Queue<TraitType>? initQueue = null) where T : TraitType, new()
        {
            var trait = new T
            {
                Priority = priority
            };

            _traits.Add(trait);
            
            // Add to initialization queue if provided
            if (initQueue != null)
            {
                initQueue.Enqueue(trait);
            }
            // Otherwise, initialize directly if needed
            else if (GridArea != null && !trait.IsInitialized)
            {
                trait.Initialize();
            }
            
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
        /// _owner?.selfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
        /// </summary>
        /// <param name="trait">The trait instance to add</param>
        /// <param name="priority">The priority of the trait</param>
        /// <param name="initQueue">Optional initialization queue</param>
        /// <returns>The added trait instance</returns>
        public TraitType AddTraitToQueue(TraitType trait, int priority, Queue<TraitType>? initQueue = null)
        {
            trait.Priority = priority;
            _traits.Add(trait);
            
            // Add to initialization queue if provided
            if (initQueue != null)
            {
                initQueue.Enqueue(trait);
            }
            // Otherwise, initialize directly if needed
            else if (GridArea != null && !trait.IsInitialized)
            {
                trait.Initialize();
            }
            
            return trait;
        }

        public T? GetTrait<T>() where T : Trait
        {
            foreach (var trait in _traits)
            {
                if (trait is T typedTrait)
                {
                    return typedTrait;
                }
            }

            return default;
        }

        public bool HasTrait<T>() where T : Trait
        {
            foreach (var trait in _traits)
            {
                if (trait is T)
                {
                    return true;
                }
            }

            return false;
        }

        // Event system for traits
        public void OnTraitEvent(string eventName, params object[] args)
        {
            foreach (var trait in _traits)
            {
                trait.OnEvent(eventName, args);
            }
        }
    }
}