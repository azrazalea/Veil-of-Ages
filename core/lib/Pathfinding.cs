using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VeilOfAges.Core.Lib
{
    public class PathFinder
    {
        private const int MAX_PATH_LENGTH = 100;

        // Finds a path between two points using A* algorithm
        public static List<Vector2I> FindPath(Grid.Area gridArea, Vector2I start, Vector2I target)
        {
            // If start and target are the same, return empty path
            if (start == target)
                return new List<Vector2I>();

            // Basic variables for A* algorithm
            var openSet = new List<Vector2I>();
            var closedSet = new HashSet<Vector2I>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var gScore = new Dictionary<Vector2I, float>();
            var fScore = new Dictionary<Vector2I, float>();

            // Initialize with starting position
            openSet.Add(start);
            gScore[start] = 0;
            fScore[start] = HeuristicCost(start, target);

            int iterations = 0;

            while (openSet.Count > 0)
            {
                iterations++;
                if (iterations > MAX_PATH_LENGTH * 2)
                {
                    // Safety check to prevent infinite loops
                    break;
                }

                // Get the position with the lowest fScore
                Vector2I current = GetLowestFScore(openSet, fScore);

                // Reached the target
                if (current == target)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);
                closedSet.Add(current);

                // Check all neighbors
                foreach (var neighbor in GetNeighbors(gridArea, current))
                {
                    if (closedSet.Contains(neighbor))
                        continue;

                    float tentativeGScore = gScore[current] + GetMoveCost(gridArea, current, neighbor);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                    else if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        continue; // Not a better path
                    }

                    // This path is the best until now, record it
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HeuristicCost(neighbor, target);
                }
            }

            // If we get here, no path was found - return fallback path
            // GD.Print($"No path found from {start} to {target}, using fallback");
            return CreateFallbackPath(gridArea, start, target);
        }

        private static Vector2I GetLowestFScore(List<Vector2I> openSet, Dictionary<Vector2I, float> fScore)
        {
            Vector2I lowest = openSet[0];
            float lowestScore = fScore.GetValueOrDefault(lowest, float.MaxValue);

            for (int i = 1; i < openSet.Count; i++)
            {
                float score = fScore.GetValueOrDefault(openSet[i], float.MaxValue);
                if (score < lowestScore)
                {
                    lowest = openSet[i];
                    lowestScore = score;
                }
            }

            return lowest;
        }

        private static List<Vector2I> GetNeighbors(Grid.Area gridArea, Vector2I position)
        {
            var neighbors = new List<Vector2I>();

            // Cardinal directions (4-way movement)
            Vector2I[] directions =
            {
                new(1, 0),  // Right
                new(-1, 0), // Left
                new(0, 1),  // Down
                new(0, -1), // Up              
                new(1, 1),  // Down-Right
                new(-1, 1), // Down-Left
                new(1, -1), // Up-Right
                new(-1, -1) // Up-Left
            };

            foreach (var dir in directions)
            {
                Vector2I neighbor = position + dir;

                // Check if the neighbor is within bounds and walkable
                if (IsPositionValid(gridArea, neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        private static bool IsPositionValid(Grid.Area gridArea, Vector2I position)
        {
            // Check if position is within bounds and walkable
            return position.X >= 0 && position.X < gridArea.GridSize.X &&
                   position.Y >= 0 && position.Y < gridArea.GridSize.Y &&
                   gridArea.IsCellWalkable(position);
        }

        private static float GetMoveCost(Grid.Area gridArea, Vector2I from, Vector2I to)
        {
            // Base cost (1.0 for cardinal, 1.4 for diagonal)
            float baseCost = from.X != to.X && from.Y != to.Y ? 1.4f : 1.0f;

            // Get terrain difficulty multiplier (adjust based on your terrain system)
            float terrainMultiplier = 1.0f;

            // If using Area extension methods for terrain difficulty:
            try
            {
                terrainMultiplier = gridArea.GetTerrainDifficulty(from, to);
            }
            catch (Exception)
            {
                // Fallback if method not implemented
                terrainMultiplier = 1.0f;
            }

            return baseCost * terrainMultiplier;
        }

        private static float HeuristicCost(Vector2I from, Vector2I to)
        {
            // Using Manhattan distance as heuristic (works well for 4-way movement)
            return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        }

        private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
        {
            var totalPath = new List<Vector2I>
            {
                // Add the endpoint
                current
            };

            // Reconstruct path by following parent pointers
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                totalPath.Insert(0, current); // Insert at beginning to reverse
            }

            // Remove the starting position (we're already there)
            if (totalPath.Count > 0)
                totalPath.RemoveAt(0);

            return totalPath;
        }

        private static List<Vector2I> CreateFallbackPath(Grid.Area gridArea, Vector2I start, Vector2I target)
        {
            // Create a simple direct path for fallback
            var path = new List<Vector2I>();
            var current = start;

            for (int i = 0; i < 20; i++) // Limit iterations to prevent infinite loops
            {
                // Move towards target using simple approach
                int dx = Math.Sign(target.X - current.X);
                int dy = Math.Sign(target.Y - current.Y);

                // Try to make at least one step in the right direction
                Vector2I next = current;

                // First try diagonal
                if (dx != 0 && dy != 0)
                {
                    Vector2I diagonalStep = new Vector2I(current.X + dx, current.Y + dy);
                    if (IsPositionValid(gridArea, diagonalStep))
                    {
                        next = diagonalStep;
                    }
                }

                // Then try horizontal movement
                if (next == current && dx != 0)
                {
                    Vector2I horizontalStep = new(current.X + dx, current.Y);
                    if (IsPositionValid(gridArea, horizontalStep))
                    {
                        next = horizontalStep;
                    }
                }

                // If no horizontal movement was possible, try vertical
                if (next == current && dy != 0)
                {
                    Vector2I verticalStep = new(current.X, current.Y + dy);
                    if (IsPositionValid(gridArea, verticalStep))
                    {
                        next = verticalStep;
                    }
                }

                // If we couldn't move in any direction, we're stuck
                if (next == current)
                    break;

                path.Add(next);
                current = next;

                // If we reached the target, we're done
                if (current == target)
                    break;
            }

            return path;
        }

        // Debug function to visualize a path (can be called from GDScript)
        public static void DebugPath(List<Vector2I> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                GD.Print($"Step {i}: {path[i]}");
            }
        }
    }
}
