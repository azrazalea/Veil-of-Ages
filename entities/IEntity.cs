using System.Collections.Generic;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities
{
    public interface IEntity : ISensable
    {
        public Grid.Area GridArea { get; }
        public List<ITrait> _traits { get; }

        public void AddTrait<T>() where T : ITrait, new()
        {
            var trait = new T();
            _traits.Add(trait);

            // If we're already initialized, initialize the trait immediately
            if (GridArea != null)
            {
                trait.Initialize(this);
            }
        }

        public void AddTrait(ITrait trait)
        {
            _traits.Add(trait);

            // If we're already initialized, initialize the trait immediately
            if (GridArea != null)
            {
                trait.Initialize(this);
            }
        }

        public T GetTrait<T>() where T : ITrait
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

        public bool HasTrait<T>() where T : ITrait
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
