using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Grid;

namespace VeilOfAges.Core.Lib;

public class PathFinder
{
    // Public interface remains the same
    public List<Vector2I> CurrentPath { get; private set; } = [];
    public int PathIndex { get; private set; } = 0;
    public int MAXPATHLENGTH = 100;

    // Path state tracking
    private PathGoalType _goalType = PathGoalType.None;
    private Being? _targetEntity = null;
    private Vector2I _targetPosition;
    private Building? _targetBuilding = null;
    private int _proximityRange = 1;
    private bool _pathNeedsCalculation = true;
    private int _recalculationAttempts = 0;
    private const int MAXRECALCULATIONATTEMPTS = 3;
    private uint _lastRecalculationTick = 0;
    private const uint RECALCULATIONCOOLDOWN = 5;
    private bool _firstGoalCalculation = false;

    // Current game tick (set by GameController)
    public uint CurrentTick { get; set; } = 0;

    // Simple enum to track goal type
    public enum PathGoalType
    {
        None,
        Position,
        EntityProximity,
        Area,
        Building
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

    public static AStarGrid2D? CreateNewAStarGrid(Area gridArea)
    {
        if (Task.CurrentId != null)
        {
            GD.PrintErr("Due to thread safety we cannot modify global astar grids inside a Task");
            return null;
        }

        // Create and configure a new AStarGrid2D instance
        var astar = new AStarGrid2D();

        // Set up the grid dimensions
        var region = new Rect2I(0, 0, gridArea.GridSize.X, gridArea.GridSize.Y);
        astar.Region = region;

        // Set cell size to match our grid's tile size
        astar.CellSize = new Vector2(1, 1); // Use 1x1 for grid coordinates

        // Configure diagonal movement
        astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;

        astar.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile;
        astar.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile;

        // Initialize the grid
        astar.Update();

        // Store it
        GD.Print($"Creating astar grid for {gridArea.Name}");
        return astar;
    }

    public static void UpdateAStarGrid(Area gridArea)
    {
        if (Task.CurrentId != null)
        {
            GD.PrintErr("Due to thread safety we cannot modify global astar grids inside a Task");
            return;
        }

        // Mark unwalkable cells as solid
        var astar = gridArea.AStarGrid;
        if (astar == null)
        {
            return;
        }

        astar.Clear();
        var region = new Rect2I(0, 0, gridArea.GridSize.X, gridArea.GridSize.Y);
        astar.Region = region;
        astar.Update();
        for (int x = 0; x < gridArea.GridSize.X; x++)
        {
            for (int y = 0; y < gridArea.GridSize.Y; y++)
            {
                Vector2I pos = new (x, y);

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
        _goalType = PathGoalType.Position;
        _targetPosition = position;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
    }

    // Set an entity proximity goal
    public void SetEntityProximityGoal(Being entity, Being targetEntity, int proximityRange = 1)
    {
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
        _goalType = PathGoalType.Area;
        _targetPosition = centerPosition;
        _proximityRange = radius;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
    }

    public void SetBuildingGoal(Being entity, Building targetBuilding)
    {
        _goalType = PathGoalType.Building;
        _targetBuilding = targetBuilding;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
    }

    // Check if goal is reached
    public bool IsGoalReached(Being entity)
    {
        if (entity == null)
        {
            return false;
        }

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
            case PathGoalType.Building:
                if (_targetBuilding != null)
                {
                    Vector2I buildingPos = _targetBuilding.GetCurrentGridPosition();
                    Vector2I buildingSize = _targetBuilding.GridSize;

                    // Check if entity is adjacent to any part of the building (including diagonals)
                    for (int x = -1; x <= buildingSize.X; x++)
                    {
                        for (int y = -1; y <= buildingSize.Y; y++)
                        {
                            // Only check the perimeter positions
                            if (x == -1 || y == -1 || x == buildingSize.X || y == buildingSize.Y)
                            {
                                Vector2I perimeterPos = new (buildingPos.X + x, buildingPos.Y + y);
                                if (entityPos == perimeterPos)
                                {
                                    result = true;
                                    break;
                                }
                            }
                        }

                        if (result)
                        {
                            break;
                        }
                    }
                }

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
              CurrentTick - _lastRecalculationTick >= RECALCULATIONCOOLDOWN &&
              _recalculationAttempts < MAXRECALCULATIONATTEMPTS))
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

        // Our path is actually just to path to our own square
        // This generally happens due to entity targets actively moving
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
            // Path is blocked, mark for recalculation
            _pathNeedsCalculation = true;
            if (secondTry)
            {
                GD.PrintErr($"Move failed for {entity.Name} to {nextPos}");
                return false;
            }
            else
            {
                return TryFollowPath(entity, true);
            }
        }

        return moveSuccessful;
    }

    // Calculate path based on current goal
    private bool CalculatePathForCurrentGoal(Being entity)
    {
        var gridArea = entity.GetGridArea();
        if (gridArea == null || gridArea.AStarGrid == null)
        {
            GD.PrintErr("CalculatePathForCurrentGoal: GridArea is null");
            return false;
        }

        Vector2I startPos = entity.GetCurrentGridPosition();
        _recalculationAttempts++;

        try
        {
            // Reset current path before calculating new one
            CurrentPath = [];
            var astar = gridArea.AStarGrid;

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
                        GD.Print("Target and start are same");
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

                        if (_targetEntity.IsMoving())
                        {
                            _recalculationAttempts = 0;
                            _pathNeedsCalculation = true;
                            return true;
                        }

                        // If already within proximity, no path needed
                        if (Utils.WithinProximityRangeOf(startPos, targetPos, _proximityRange))
                        {
                            return true;
                        }

                        var adjacentPositions = GetPositionsAroundEntity(targetPos, 1);
                        var walkablePositions = adjacentPositions
                            .Where(pos => astar.IsInBoundsv(pos) && !astar.IsPointSolid(pos))
                            .ToList();

                        // Try to find path to any walkable position around target
                        bool foundPath = false;
                        foreach (var pos in walkablePositions)
                        {
                            // We're only 1 tile away
                            if (startPos.DistanceSquaredTo(pos) <= 2)
                            {
                                CurrentPath = [pos];
                                foundPath = true;
                                break;
                            }

                            var path = astar.GetIdPath(startPos, pos, true);
                            if (path.Count > 0)
                            {
                                CurrentPath = path.Cast<Vector2I>().ToList();
                                foundPath = true;
                                break;
                            }
                        }

                        if (!foundPath)
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

                case PathGoalType.Building:
                    if (_targetBuilding != null)
                    {
                        Vector2I buildingPos = _targetBuilding.GetCurrentGridPosition();
                        Vector2I buildingSize = _targetBuilding.GridSize;

                        // Get all possible adjacent positions around the building
                        var adjacentPositions = new List<Vector2I>();

                        // Add positions around the building perimeter
                        for (int x = -1; x <= buildingSize.X; x++)
                        {
                            for (int y = -1; y <= buildingSize.Y; y++)
                            {
                                // Only include perimeter positions
                                if (x == -1 || y == -1 || x == buildingSize.X || y == buildingSize.Y)
                                {
                                    Vector2I pos = new (buildingPos.X + x, buildingPos.Y + y);
                                    if (astar.IsInBoundsv(pos) && !astar.IsPointSolid(pos))
                                    {
                                        adjacentPositions.Add(pos);
                                    }
                                }
                            }
                        }

                        // Sort by distance to start position
                        adjacentPositions.Sort((a, b) =>
                            startPos.DistanceSquaredTo(a).CompareTo(startPos.DistanceSquaredTo(b)));

                        // Try to find path to any adjacent position
                        bool foundPath = false;
                        foreach (var pos in adjacentPositions)
                        {
                            // Skip if we're already at this position
                            if (startPos == pos)
                            {
                                CurrentPath = [pos];
                                foundPath = true;
                                break;
                            }

                            var path = astar.GetIdPath(startPos, pos, true);
                            if (path.Count > 0)
                            {
                                CurrentPath = path.Cast<Vector2I>().ToList();
                                foundPath = true;
                                break;
                            }
                        }

                        if (!foundPath)
                        {
                            GD.PrintErr($"No path found to building {_targetBuilding.BuildingType}");
                            return false;
                        }
                    }
                    else
                    {
                        GD.PrintErr("Target building is null");
                        return false;
                    }

                    break;
            }

            // Limit path length if needed
            if (CurrentPath.Count > MAXPATHLENGTH)
            {
                CurrentPath = CurrentPath.GetRange(0, MAXPATHLENGTH);
            }

            PathIndex = 0;
            _pathNeedsCalculation = false;
            _lastRecalculationTick = CurrentTick;
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
        var result = new List<Vector2I>
        {
            // Add positions in cardinal directions first (more natural movement)
            new (center.X + 1, center.Y),
            new (center.X - 1, center.Y),
            new (center.X, center.Y + 1),
            new (center.X, center.Y - 1)
        };

        // Then add diagonals if needed
        if (range > 1)
        {
            result.AddRange([
                new Vector2I(center.X + 1, center.Y + 1),
                new Vector2I(center.X - 1, center.Y + 1),
                new Vector2I(center.X + 1, center.Y - 1),
                new Vector2I(center.X - 1, center.Y - 1)
            ]);
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
                if ((x * x) + (y * y) <= radius * radius)
                {
                    Vector2I pos = new (center.X + x, center.Y + y);
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
