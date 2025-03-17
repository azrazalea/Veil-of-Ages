using Godot;
using System.Collections.Generic;

using NecromancerKingdom.Entities;
using NecromancerKingdom.Entities.Traits;

namespace NecromancerKingdom.Core.Lib
{
    public enum DijkstraGoalType
    {
        Undefined,
        LivingBeings,
        UndedBeings,
        Water,
        Safety,
        DistanceFromUndead
    }

    public class DijkstraMap
    {
        private World _world;
        private Dictionary<Vector2I, float> _values = new();
        private HashSet<Vector2I> _goals = new();

        public DijkstraGoalType GoalType { get; }

        public DijkstraMap(World world, DijkstraGoalType goalType)
        {
            _world = world;
            GoalType = goalType;

            // Set goals based on goal type
            IdentifyGoals();

            // Calculate initial values
            Recalculate();
        }

        private void IdentifyGoals()
        {
            _goals.Clear();

            switch (GoalType)
            {
                case DijkstraGoalType.LivingBeings:
                    // Find living beings
                    foreach (var entity in _world.GetEntities())
                    {
                        if (entity is Being being && !being.HasTrait<UndeadTrait>())
                        {
                            _goals.Add(being.GetCurrentGridPosition());
                        }
                    }
                    break;

                case DijkstraGoalType.DistanceFromUndead:
                    // Invert - undead positions are "anti-goals"
                    foreach (var entity in _world.GetEntities())
                    {
                        if (entity is Being being && being.HasTrait<UndeadTrait>())
                        {
                            // Mark positions around undead
                            var pos = being.GetCurrentGridPosition();
                            for (int x = -3; x <= 3; x++)
                            {
                                for (int y = -3; y <= 3; y++)
                                {
                                    _goals.Add(pos + new Vector2I(x, y));
                                }
                            }
                        }
                    }
                    break;

                    // More goal types...
            }
        }

        public void Recalculate()
        {
            // Reset values
            _values.Clear();

            // Set goal values
            foreach (var goal in _goals)
            {
                // For 'distance from' maps, goals are high value
                float goalValue = GoalType == DijkstraGoalType.DistanceFromUndead ?
                    1000f : 0f;

                _values[goal] = goalValue;
            }

            // Use Dijkstra's algorithm to fill in the map
            var queue = new PriorityQueue<Vector2I, float>();

            // Enqueue all goals
            foreach (var goal in _goals)
            {
                queue.Enqueue(goal, _values[goal]);
            }

            // Process the queue
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // Process neighbors
                foreach (var neighbor in GetNeighbors(current))
                {
                    // Calculate new value
                    float moveCost = GetMoveCost(current, neighbor);

                    // For 'distance from' maps, we subtract
                    if (GoalType == DijkstraGoalType.DistanceFromUndead)
                    {
                        float newValue = _values[current] - moveCost;

                        // Only update if higher value (further from undead)
                        if (!_values.ContainsKey(neighbor) || newValue > _values[neighbor])
                        {
                            _values[neighbor] = newValue;
                            queue.Enqueue(neighbor, -newValue); // Note negative for priority queue
                        }
                    }
                    else
                    {
                        // For attraction maps, we add cost
                        float newValue = _values[current] + moveCost;

                        // Only update if lower value (closer to goal)
                        if (!_values.ContainsKey(neighbor) || newValue < _values[neighbor])
                        {
                            _values[neighbor] = newValue;
                            queue.Enqueue(neighbor, newValue);
                        }
                    }
                }
            }
        }

        private List<Vector2I> GetNeighbors(Vector2I position)
        {
            var neighbors = new List<Vector2I>
            {
                position + new Vector2I(1, 0),
                position + new Vector2I(-1, 0),
                position + new Vector2I(0, 1),
                position + new Vector2I(0, -1),
                // Diagonals
                position + new Vector2I(-1, -1),
                position + new Vector2I(1, 1),
                position + new Vector2I(-1, 1),
                position + new Vector2I(1, -1)
            };

            // Filter out unwalkable tiles
            neighbors.RemoveAll(pos => !_world.IsWalkable(pos));

            return neighbors;
        }

        private float GetMoveCost(Vector2I from, Vector2I to)
        {
            // Base cost
            float cost = 1.0f;

            // Add terrain difficulty
            cost += _world.GetTerrainDifficulty(from, to);

            return cost;
        }

        public float GetValueAt(Vector2I position)
        {
            return _values.TryGetValue(position, out var value) ?
                value : (GoalType == DijkstraGoalType.DistanceFromUndead ? -1000f : float.MaxValue);
        }

        public List<Vector2I> FindPathFrom(Vector2I start)
        {
            var path = new List<Vector2I>();

            // Check if start is in the map
            if (!_values.ContainsKey(start))
                return path;

            // For safety maps, we want to move to higher values
            bool movingToHigher = GoalType == DijkstraGoalType.DistanceFromUndead;

            var current = start;
            int maxSteps = 20; // Prevent infinite loops

            while (maxSteps-- > 0)
            {
                var neighbors = GetNeighbors(current);
                if (neighbors.Count == 0)
                    break;

                // Find best neighbor
                Vector2I best = neighbors[0];
                float bestValue = GetValueAt(best);

                foreach (var neighbor in neighbors)
                {
                    float value = GetValueAt(neighbor);

                    if ((movingToHigher && value > bestValue) ||
                        (!movingToHigher && value < bestValue))
                    {
                        bestValue = value;
                        best = neighbor;
                    }
                }

                // Check if we're making progress
                float currentValue = GetValueAt(current);
                if ((movingToHigher && bestValue <= currentValue) ||
                    (!movingToHigher && bestValue >= currentValue))
                {
                    // No better neighbor
                    break;
                }

                // Add to path
                path.Add(best);
                current = best;

                // Stop if we reached a goal
                if (_goals.Contains(current))
                    break;
            }

            return path;
        }
    }
}
