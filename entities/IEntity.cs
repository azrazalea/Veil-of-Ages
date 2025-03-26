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

            // If we're already initialized, initialize the trait immediately
            if (GridArea != null)
            {
                trait.Initialize();
            }
        }

        public void AddTrait(Trait trait, int priority)
        {
            trait.Priority = priority;
            _traits.Add((TraitType)trait);

            // If we're already initialized, initialize the trait immediately
            if (GridArea != null)
            {
                trait.Initialize();
            }
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
