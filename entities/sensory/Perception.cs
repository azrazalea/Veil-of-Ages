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
        private List<DijkstraMap> _dijkstraMaps = new();
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

        public void AddDijkstraMap(DijkstraMap map)
        {
            _dijkstraMaps.Add(map);
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

        public List<Vector2I> FindPathTo<T>(Vector2I start) where T : Being
        {
            // Use Dijkstra maps if available
            foreach (var map in _dijkstraMaps)
            {
                if (map.GoalType == GetDijkstraGoalForType<T>())
                {
                    return map.FindPathFrom(start);
                }
            }

            // Fallback to direct pathfinding
            var targets = GetEntitiesOfType<T>();
            if (targets.Count > 0)
            {
                // Find closest target
                var closest = targets.OrderBy(t => start.DistanceTo(t.position)).First();

                // A* pathfinding (would be implemented here)
                return FindPathBetween(start, closest.position);
            }

            return null;
        }

        // Helper to convert entity type to goal type
        private DijkstraGoalType GetDijkstraGoalForType<T>() where T : Being
        {
            return DijkstraGoalType.Undefined;
        }

        // A* pathfinding implementation
        private List<Vector2I> FindPathBetween(Vector2I start, Vector2I end)
        {
            // A* implementation would go here
            // This runs in the entity's thread where it's OK to be computationally expensive
            return null;  // Placeholder
        }
    }
}
