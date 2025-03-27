using Godot;
using VeilOfAges.Core.Lib;
using System.Collections.Generic;
using System.Linq;

namespace VeilOfAges.Entities.Sensory
{
    // Represents what an entity actually perceives after filtering
    public class Perception
    {
        private Dictionary<Vector2I, List<ISensable>> _detectedSensables = new();
        private List<WorldEvent> _perceivedEvents = new();
        private Dictionary<Being, float> _threatLevels = new();

        public void AddDetectedSensable(ISensable sensable, Vector2I position)
        {
            if (!_detectedSensables.TryGetValue(position, out var list))
            {
                list = new List<ISensable>();
                _detectedSensables[position] = list;
            }
            list.Add(sensable);
        }

        public void AddPerceivedEvent(WorldEvent evt)
        {
            _perceivedEvents.Add(evt);
        }

        // Helper methods for working with perceptions
        public List<(T entity, Vector2I position)> GetEntitiesOfType<T>() where T : Being
        {
            var result = new List<(T entity, Vector2I position)>();

            foreach (var pair in _detectedSensables)
            {
                foreach (var sensable in pair.Value)
                {
                    if (sensable is T typedEntity)
                    {
                        result.Add((typedEntity, pair.Key));
                    }
                }
            }

            return result;
        }
    }
}
