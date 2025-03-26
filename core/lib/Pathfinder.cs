using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings;

namespace VeilOfAges.Core.Lib
{
    public class PathFinder
    {
        private const int MAX_PATH_LENGTH = 100;
        public List<Vector2I> CurrentPath { get; private set; } = [];
        public int PathIndex { get; private set; } = 0;

        public void ClearPath()
        {
            CurrentPath = [];
        }

        // Finds a path between two points using A* algorithm
        public void SetPath(Grid.Area gridArea, Vector2I start, Vector2I target)
        {
            PathIndex = 0;
            // If start and target are the same, return empty path
            if (start == target)
            {
                CurrentPath = [];
                return;
            }

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
                    CurrentPath = ReconstructPath(cameFrom, current);
                    return;
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
            CurrentPath = CreateFallbackPath(gridArea, start, target);
            return;
        }


        // Find a path between two entities
        public void SetPathBetween(Being source, Being target)
        {
            if (source.GridArea == null) return;

            SetPath(
                source.GridArea,
                source.GetCurrentGridPosition(),
                target.GetCurrentGridPosition()
                );

            return;
        }

        // Find a path to a position within a certain range of a target being
        public void SetPathToWithinRange(Being source, Being target, int range = 1)
        {
            if (source.GridArea == null) return; // Why doesn't this fix the null warnings on line 298 and 302?

            var sourcePos = source.GetCurrentGridPosition();
            var targetPos = target.GetCurrentGridPosition();

            // If already within range, return empty path
            if (sourcePos.DistanceTo(targetPos) <= range)
            {
                CurrentPath = [];
                return;
            }

            // Get possible positions around target
            var possibleEndPositions = GetPositionsAroundEntity(target, range);

            // Find the closest walkable position
            Vector2I? bestPos = null;
            float bestDistance = float.MaxValue;

            foreach (var pos in possibleEndPositions)
            {
                if (source.GridArea?.IsCellWalkable(pos) == true)
                {
                    float dist = sourcePos.DistanceTo(pos);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestPos = pos;
                    }
                }
            }

            if (source.GridArea == null) return; // Why do I need this down here as well?

            if (bestPos.HasValue)
            {
                SetPath(source.GridArea, sourcePos, bestPos.Value);
                return;
            }

            // Fallback to direct path if no accessible position found
            SetPath(source.GridArea, sourcePos, targetPos);
            return;
        }

        // Find a path to the nearest entity of a specific type
        public void SetPathToNearest<T>(Being source, List<T> potentialTargets) where T : Being
        {
            if (source.GridArea == null) return;

            var sourcePos = source.GetCurrentGridPosition();

            T? closestTarget = default;
            float closestDistance = float.MaxValue;

            foreach (var target in potentialTargets)
            {
                var targetPos = target.GetCurrentGridPosition();
                float distance = sourcePos.DistanceTo(targetPos);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }

            if (closestTarget != null)
            {
                SetPath(
                    source.GridArea,
                    sourcePos,
                    closestTarget.GetCurrentGridPosition());
                return;
            }

            CurrentPath = [];
            return;
        }

        public bool SetPathTo(Being? entity, Vector2I position)
        {
            if (entity == null) return false;

            var gridArea = entity.GetGridArea();
            if (gridArea == null || entity == null) return false;

            Vector2I currentPos = entity.GetCurrentGridPosition();
            SetPath(gridArea, currentPos, position);

            return true;
        }

        // Move an entity along a path
        public bool MoveAlongPath(Being entity)
        {
            if (CurrentPath == null || IsPathComplete())
                return false;

            if (entity.IsMoving())
                return false;

            Vector2I nextPos = CurrentPath[PathIndex];
            bool moveSuccessful = entity.TryMoveToGridPosition(nextPos);

            if (moveSuccessful)
            {
                PathIndex++;
            }
            else // Try once to recalculate the path
            {
                SetPathTo(entity, CurrentPath.Last());
                if (CurrentPath.Count > 0) moveSuccessful = entity.TryMoveToGridPosition(CurrentPath.First());
                if (moveSuccessful) PathIndex++;
            }

            return moveSuccessful;
        }

        public bool IsPathComplete()
        {
            return CurrentPath.Count == 0 || PathIndex >= CurrentPath.Count;
        }

        // Get positions in a ring around a target
        private List<Vector2I> GetPositionsAroundEntity(Being entity, int range)
        {
            var result = new List<Vector2I>();
            var center = entity.GetCurrentGridPosition();

            for (int r = 1; r <= range; r++)
            {
                // Add positions in a square around the entity
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        // Only add positions at exactly distance r (creates a ring)
                        if (Math.Abs(x) == r || Math.Abs(y) == r)
                        {
                            result.Add(new Vector2I(center.X + x, center.Y + y));
                        }
                    }
                }
            }

            return result;
        }

        private Vector2I GetLowestFScore(List<Vector2I> openSet, Dictionary<Vector2I, float> fScore)
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

        private List<Vector2I> GetNeighbors(Grid.Area gridArea, Vector2I position)
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

        private bool IsPositionValid(Grid.Area gridArea, Vector2I position)
        {
            // Check if position is within bounds and walkable
            return position.X >= 0 && position.X < gridArea.GridSize.X &&
                   position.Y >= 0 && position.Y < gridArea.GridSize.Y &&
                   gridArea.IsCellWalkable(position);
        }

        private float GetMoveCost(Grid.Area gridArea, Vector2I from, Vector2I to)
        {
            // Base cost (1.0 for cardinal, 1.5 for diagonal)
            float baseCost = from.X != to.X && from.Y != to.Y ? 1.5f : 1.0f;

            // Get terrain difficulty multiplier (adjust based on your terrain system)
            float terrainMultiplier;

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

        private float HeuristicCost(Vector2I from, Vector2I to)
        {
            // Using Manhattan distance as heuristic (works well for 4-way movement)
            return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        }

        private List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
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

        private List<Vector2I> CreateFallbackPath(Grid.Area gridArea, Vector2I start, Vector2I target)
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
        public void DebugPath(List<Vector2I> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                GD.Print($"Step {i}: {path[i]}");
            }
        }
    }
}
