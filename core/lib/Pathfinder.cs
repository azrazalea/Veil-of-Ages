using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VeilOfAges.Entities;
using VeilOfAges.Grid;

namespace VeilOfAges.Core.Lib
{
    public class PathFinder
    {
        // Public interface remains the same
        public List<Vector2I> CurrentPath { get; private set; } = [];
        public int PathIndex { get; private set; } = 0;
        public int MAX_PATH_LENGTH = 100;

        // Path state tracking
        private PathGoalType _goalType = PathGoalType.None;
        private Being? _targetEntity = null;
        private Vector2I _targetPosition;
        private int _proximityRange = 1;
        private bool _pathNeedsCalculation = true;
        private int _recalculationAttempts = 0;
        private const int MAX_RECALCULATION_ATTEMPTS = 3;
        private uint _lastRecalculationTick = 0;
        private const uint RECALCULATION_COOLDOWN = 5;
        private bool _firstGoalCalculation = false;
        private static Dictionary<string, AStarGrid2D> AStarGrids = [];

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
            _firstGoalCalculation = false;
        }

        // Check if path is valid
        public bool HasValidPath()
        {
            return !_pathNeedsCalculation && CurrentPath.Count > 0 && PathIndex < CurrentPath.Count;
        }

        public static void createNewAStarGrid(Area gridArea)
        {
            if (Task.CurrentId != null)
            {
                GD.PrintErr("Due to thread safety we cannot modify global astar grids inside a Task");
                return;
            }

            if (AStarGrids.ContainsKey(gridArea.Name)) return;

            // Create and configure a new AStarGrid2D instance
            var astar = new AStarGrid2D();

            // Set up the grid dimensions
            var region = new Rect2I(0, 0, gridArea.GridSize.X, gridArea.GridSize.Y);
            astar.Region = region;

            // Set cell size to match our grid's tile size
            astar.CellSize = new Vector2(1, 1); // Use 1x1 for grid coordinates

            // Configure diagonal movement
            astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.AtLeastOneWalkable;

            // Initialize the grid
            astar.Update();

            // Store it
            GD.Print($"Creating astar grid for {gridArea.Name}");
            AStarGrids.Add(gridArea.Name, astar);
        }

        public static void updateAStarGrid(Area gridArea)
        {
            if (Task.CurrentId != null)
            {
                GD.PrintErr("Due to thread safety we cannot modify global astar grids inside a Task");
                return;
            }

            if (!AStarGrids.ContainsKey(gridArea.Name))
            {
                GD.Print($"Warning, did not have astar grid for {gridArea.Name}");
                createNewAStarGrid(gridArea);
            }


            // Mark unwalkable cells as solid
            var astar = AStarGrids[gridArea.Name];
            for (int x = 0; x < gridArea.GridSize.X; x++)
            {
                for (int y = 0; y < gridArea.GridSize.Y; y++)
                {
                    Vector2I pos = new(x, y);

                    if (!gridArea.IsCellWalkable(pos))
                    {
                        astar.SetPointSolid(pos, true);
                    }
                    else
                    {
                        astar.SetPointWeightScale(pos, gridArea.GetTerrainDifficulty(pos));
                    }
                }
            }
        }

        // Set a position goal
        public void SetPositionGoal(Being entity, Vector2I position)
        {
            // If this is already our goal
            if (_goalType == PathGoalType.Position && _targetPosition == position)
            {
                return;
            }
            _goalType = PathGoalType.Position;
            _targetPosition = position;
            _firstGoalCalculation = true;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // Set an entity proximity goal
        public void SetEntityProximityGoal(Being entity, Being targetEntity, int proximityRange = 1)
        {
            // If this is already our goal
            if (_goalType == PathGoalType.EntityProximity && _targetEntity == targetEntity && _proximityRange == proximityRange)
            {
                return;
            }
            _goalType = PathGoalType.EntityProximity;
            _targetEntity = targetEntity;
            _proximityRange = proximityRange;
            _firstGoalCalculation = true;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // Set an area goal
        public void SetAreaGoal(Being entity, Vector2I centerPosition, int radius)
        {
            // If this is already our goal
            if (_goalType == PathGoalType.Area && _targetPosition == centerPosition && _proximityRange == radius)
            {
                return;
            }

            _goalType = PathGoalType.Area;
            _targetPosition = centerPosition;
            _proximityRange = radius;
            _firstGoalCalculation = true;
            _pathNeedsCalculation = true;
            _recalculationAttempts = 0;
        }

        // Check if goal is reached
        public bool IsGoalReached(Being entity)
        {
            if (entity == null) return false;

            Vector2I entityPos = entity.GetCurrentGridPosition();
            bool result = false;

            switch (_goalType)
            {
                case PathGoalType.None:
                    result = true;
                    break;
                case PathGoalType.Position:
                    result = entityPos == _targetPosition;
                    break;
                case PathGoalType.EntityProximity:
                    if (_targetEntity != null)
                    {
                        Vector2I targetPos = _targetEntity.GetCurrentGridPosition();
                        result = Utils.WithinProximityRangeOf(entityPos, targetPos, _proximityRange);
                    }
                    break;
                case PathGoalType.Area:
                    result = Utils.WithinProximityRangeOf(entityPos, _targetPosition, _proximityRange);
                    break;
            }

            return result;
        }

        // Method to follow the current path
        public bool TryFollowPath(Being entity, bool secondTry = false)
        {
            CurrentTick++;

            if (entity == null)
            {
                GD.PrintErr("TryFollowPath: Entity is null");
                return false;
            }

            // First check if we've reached the goal directly or entity is currently moving
            if (IsGoalReached(entity) || entity.IsMoving())
            {
                return true;
            }

            // Calculate path if needed and not on cooldown
            if (_firstGoalCalculation ||
                (_pathNeedsCalculation &&
                  CurrentTick - _lastRecalculationTick >= RECALCULATION_COOLDOWN &&
                  _recalculationAttempts < MAX_RECALCULATION_ATTEMPTS))
            {
                _firstGoalCalculation = false;
                bool success = CalculatePathForCurrentGoal(entity);
                if (!success)
                {
                    GD.PrintErr($"Failed to calculate path for {entity.Name} with goal type {_goalType}");
                    return false;
                }
            }

            // If no valid path, can't follow
            if (!HasValidPath())
            {
                return false;
            }

            // // Our path is actually just to path to our own square
            // // This generally happens due to entity targets actively moving
            if (CurrentPath.SequenceEqual([entity.GetCurrentGridPosition()]))
            {
                _pathNeedsCalculation = true;
                return true;
            }


            // Try to move along the path
            Vector2I nextPos = CurrentPath[PathIndex];

            // skip our own position
            if (entity.GetCurrentGridPosition() == nextPos)
            {
                nextPos = CurrentPath[++PathIndex];
            }

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
                GD.PrintErr($"Move failed for {entity.Name} to {nextPos}");

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

        // Calculate path based on current goal
        private bool CalculatePathForCurrentGoal(Being entity)
        {
            var gridArea = entity.GetGridArea();
            if (gridArea == null)
            {
                GD.PrintErr("CalculatePathForCurrentGoal: GridArea is null");
                return false;
            }

            Vector2I startPos = entity.GetCurrentGridPosition();
            _lastRecalculationTick = CurrentTick;
            _recalculationAttempts++;

            try
            {
                // Reset current path before calculating new one
                CurrentPath = [];
                var astar = AStarGrids[gridArea.Name];
                // Calculate path based on goal type
                switch (_goalType)
                {
                    case PathGoalType.Position:
                        // Check if target position is in bounds
                        if (!astar.IsInBoundsv(_targetPosition))
                        {
                            GD.PrintErr($"Target position {_targetPosition} is out of bounds");
                            return false;
                        }

                        // If start and target are the same, no path needed
                        if (startPos == _targetPosition)
                        {
                            return true;
                        }

                        // Get path to specific position
                        var positionPath = astar.GetIdPath(startPos, _targetPosition, true);

                        if (positionPath.Count > 0)
                        {
                            CurrentPath = positionPath.Cast<Vector2I>().ToList();
                        }
                        else
                        {
                            GD.PrintErr($"No path found to position {_targetPosition}");
                            return false;
                        }
                        break;

                    case PathGoalType.EntityProximity:
                        if (_targetEntity != null)
                        {
                            Vector2I targetPos = _targetEntity.GetCurrentGridPosition();

                            // If already within proximity, no path needed
                            if (Utils.WithinProximityRangeOf(startPos, targetPos, _proximityRange))
                            {
                                return true;
                            }

                            var proximityPath = astar.GetIdPath(startPos, targetPos, true);
                            if (proximityPath.Count > 0)
                            {
                                CurrentPath = proximityPath.Cast<Vector2I>().ToList();
                            }
                            else
                            {
                                GD.PrintErr($"No path found to entity {_targetEntity.Name}");
                                return false;
                            }
                        }
                        else
                        {
                            GD.PrintErr("Target entity is null");
                            return false;
                        }
                        break;
                    case PathGoalType.Area:
                        // If already within area, no path needed
                        if (Utils.WithinProximityRangeOf(entity.GetCurrentGridPosition(), _targetPosition, _proximityRange))
                        {
                            return true;
                        }

                        // Get area positions and ensure they're not solid
                        var areaPositions = GetValidPositionsInArea(_targetPosition, _proximityRange, gridArea);

                        if (areaPositions.Count > 0)
                        {
                            // Sort by distance to start position
                            areaPositions.Sort((a, b) =>
                                startPos.DistanceSquaredTo(a).CompareTo(startPos.DistanceSquaredTo(b)));

                            // Try to find path to closest position
                            bool foundAreaPath = false;
                            foreach (var pos in areaPositions)
                            {
                                var areaPath = astar.GetIdPath(startPos, pos, true);
                                if (areaPath.Count > 0)
                                {
                                    CurrentPath = areaPath.Cast<Vector2I>().ToList();
                                    foundAreaPath = true;
                                    break;
                                }
                            }

                            if (!foundAreaPath)
                            {
                                GD.PrintErr("No path found to any position in area");
                                return false;
                            }
                        }
                        else
                        {
                            GD.PrintErr("No valid positions found in area");
                            return false;
                        }
                        break;
                }

                // Limit path length if needed
                if (CurrentPath.Count > MAX_PATH_LENGTH)
                {
                    CurrentPath = CurrentPath.GetRange(0, MAX_PATH_LENGTH);
                }

                PathIndex = 0;
                _pathNeedsCalculation = false;
                return CurrentPath.Count > 0;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Exception in path calculation: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // Get positions in a ring around a target
        private List<Vector2I> GetPositionsAroundEntity(Vector2I center, int range)
        {
            var result = new List<Vector2I>();

            // Add positions in expanding rings
            for (int r = 1; r <= range; r++)
            {
                // Add positions in a square perimeter
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

        // Get valid positions within an area
        private List<Vector2I> GetValidPositionsInArea(Vector2I center, int radius, Grid.Area gridArea)
        {
            List<Vector2I> positions = [];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    // Check if position is within circular radius
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
    }
}
