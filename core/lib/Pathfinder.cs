using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Entities;

namespace VeilOfAges.Core.Lib
{
    public class PathFinder
    {
        public List<Vector2I> CurrentPath { get; private set; } = [];
        public int PathIndex { get; private set; } = 0;
        public int MAX_PATH_LENGTH = 100;

        // Add new fields for lazy evaluation
        private PathGoalType _goalType = PathGoalType.None;
        private Being? _targetEntity = null;
        private Vector2I _targetPosition;
        private int _proximityRange = 1;
        private bool _pathNeedsCalculation = false;

        // Path state tracking
        private int _recalculationAttempts = 0;
        private const int MAX_RECALCULATION_ATTEMPTS = 3;
        private uint _lastRecalculationTick = 0;
        private const uint RECALCULATION_COOLDOWN = 5;

        // Current game tick (set by GameController)
        public uint CurrentTick { get; set; } = 0;

        // Simple enum to track goal type
        public enum PathGoalType
        {
            None,
            Position,
            EntityProximity,
            Area
        }

        public void ClearPath()
        {
            CurrentPath = [];
            PathIndex = 0;
            _pathNeedsCalculation = true;
        }

        public void Reset()
        {
            ClearPath();
            _goalType = PathGoalType.None;
        }

        // New method to check if path is valid
        public bool HasValidPath()
        {
            return !_pathNeedsCalculation && CurrentPath.Count > 0 && PathIndex < CurrentPath.Count;
        }

        // New method to set a position goal
        public void SetPositionGoal(Being entity, Vector2I position)
        {
            _goalType = PathGoalType.Position;
            _targetPosition = position;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // New method to set an entity proximity goal
        public void SetEntityProximityGoal(Being entity, Being targetEntity, int proximityRange = 1)
        {
            _goalType = PathGoalType.EntityProximity;
            _targetEntity = targetEntity;
            _proximityRange = proximityRange;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // New method to set an area goal
        public void SetAreaGoal(Being entity, Vector2I centerPosition, int radius)
        {
            _goalType = PathGoalType.Area;
            _targetPosition = centerPosition;
            _proximityRange = radius;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // Check if goal is reached
        public bool IsGoalReached(Being entity)
        {
            // Verify by goal type
            return _goalType switch
            {
                PathGoalType.None => true,
                PathGoalType.Position => entity.GetCurrentGridPosition() == _targetPosition,
                PathGoalType.EntityProximity => _targetEntity != null &&
                                               entity.GetCurrentGridPosition().DistanceTo(
                                                   _targetEntity.GetCurrentGridPosition()) <= _proximityRange,
                PathGoalType.Area => entity.GetCurrentGridPosition().DistanceTo(_targetPosition) <= _proximityRange,
                _ => false
            };
        }

        // New method for lazy path following with calculation as needed
        public bool TryFollowPath(Being entity, bool secondTry = false)
        {
            CurrentTick++;

            // First check if we've reached the goal directly
            if (IsGoalReached(entity))
                return true;

            // Calculate path if needed and not on cooldown
            if (_pathNeedsCalculation &&
                CurrentTick - _lastRecalculationTick >= RECALCULATION_COOLDOWN &&
                _recalculationAttempts < MAX_RECALCULATION_ATTEMPTS)
            {
                CalculatePathForCurrentGoal(entity);
            }

            // If no valid path, can't follow
            if (!HasValidPath())
                return false;

            // Try to move along the path
            Vector2I nextPos = CurrentPath[PathIndex];
            bool moveSuccessful = entity.TryMoveToGridPosition(nextPos);

            if (moveSuccessful)
            {
                PathIndex++;

                // Check if we've completed the path
                if (PathIndex >= CurrentPath.Count)
                {
                    // We've reached the end but maybe not the goal
                    if (!IsGoalReached(entity))
                    {
                        _pathNeedsCalculation = true;
                    }
                    return true;
                }
            }
            else
            {
                // Path is blocked, mark for recalculation
                _pathNeedsCalculation = true;
                if (!secondTry)
                {
                    return TryFollowPath(entity, true);
                }
                return false;
            }

            return moveSuccessful;
        }

        // Private method to calculate path based on current goal
        private void CalculatePathForCurrentGoal(Being entity)
        {
            var gridArea = entity.GetGridArea();
            if (gridArea == null) return;

            Vector2I startPos = entity.GetCurrentGridPosition();
            _lastRecalculationTick = CurrentTick;
            _recalculationAttempts++;

            // Calculate based on goal type
            switch (_goalType)
            {
                case PathGoalType.Position:
                    // Use existing SetPath method you already have
                    SetPath(gridArea, startPos, _targetPosition);
                    break;

                case PathGoalType.EntityProximity:
                    if (_targetEntity != null)
                    {
                        // Use existing SetPathToWithinRange method
                        SetPathToWithinRange(entity, _targetEntity, _proximityRange);
                    }
                    break;

                case PathGoalType.Area:
                    // Find a valid position in the area
                    List<Vector2I> validPositions = GetValidPositionsInArea(_targetPosition, _proximityRange, gridArea);

                    if (validPositions.Count > 0)
                    {
                        // Sort by distance
                        validPositions.Sort((a, b) =>
                            startPos.DistanceSquaredTo(a).CompareTo(startPos.DistanceSquaredTo(b)));

                        // Try closest positions
                        foreach (var pos in validPositions)
                        {
                            if (SetPath(gridArea, startPos, pos))
                                break;
                        }
                    }
                    break;
            }

            _pathNeedsCalculation = false; // Mark as calculated
        }

        // Helper for area goals
        private List<Vector2I> GetValidPositionsInArea(Vector2I center, int radius, Grid.Area gridArea)
        {
            List<Vector2I> positions = [];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        Vector2I pos = new(center.X + x, center.Y + y);
                        if (gridArea.IsCellWalkable(pos))
                        {
                            positions.Add(pos);
                        }
                    }
                }
            }

            return positions;
        }
        // Finds a path between two points using A* algorithm
        private bool SetPath(Grid.Area gridArea, Vector2I start, Vector2I target)
        {
            PathIndex = 0;
            // If start and target are the same, return empty path
            if (start == target)
            {
                CurrentPath = [];
                return false;
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
                    return true;
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
            return CurrentPath.Count > 0;
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
