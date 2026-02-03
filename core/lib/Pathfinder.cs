using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Grid;

namespace VeilOfAges.Core.Lib;

public class PathFinder
{
    // Public interface remains the same
    public List<Vector2I> CurrentPath { get; private set; } = [];
    public int PathIndex { get; private set; }
    public int MAXPATHLENGTH = 100;

    // Cached pathfinding grid for non-village residents
    private AStarGrid2D? _cachedPathfindingGrid;

    // Path state tracking
    private PathGoalType _goalType = PathGoalType.None;
    private Being? _targetEntity;
    private Vector2I _targetPosition;
    private Building? _targetBuilding;
    private int _proximityRange = 1;
    private bool _pathNeedsCalculation = true;
    private int _recalculationAttempts;
    private const int MAXRECALCULATIONATTEMPTS = 3;
    private uint _lastRecalculationTick;
    private const uint RECALCULATIONCOOLDOWN = 5;
    private bool _firstGoalCalculation;

    // Facility goal tracking - stores the facility position to check adjacency in IsGoalReached
    private Vector2I? _targetFacilityPosition;
    private Building? _targetFacilityBuilding;
    private string? _targetFacilityId;

    // Building goal: if true, entity must be inside; if false, adjacent is acceptable
    private bool _requireInterior;

    // Simple enum to track goal type
    public enum PathGoalType
    {
        None,
        Position,
        EntityProximity,
        Area,
        Building,
        Facility
    }

    /// <summary>
    /// Returns a detailed summary of the current pathfinder state for debugging.
    /// </summary>
    public string GetDebugSummary(Being? entity = null)
    {
        try
        {
            var culture = new System.Globalization.CultureInfo("en-US");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(culture, $"=== PathFinder State ===");
            sb.AppendLine(culture, $"GoalType: {_goalType}");
            sb.AppendLine(culture, $"PathNeedsCalculation: {_pathNeedsCalculation}");
            sb.AppendLine(culture, $"FirstGoalCalculation: {_firstGoalCalculation}");
            sb.AppendLine(culture, $"RecalculationAttempts: {_recalculationAttempts}/{MAXRECALCULATIONATTEMPTS}");
            sb.AppendLine(culture, $"LastRecalculationTick: {_lastRecalculationTick} (current: {GameController.CurrentTick}, cooldown: {RECALCULATIONCOOLDOWN})");
            sb.AppendLine(culture, $"CurrentPath: {CurrentPath.Count} nodes, PathIndex: {PathIndex}");

            if (CurrentPath.Count > 0)
            {
                sb.AppendLine(culture, $"  Path: [{string.Join(" -> ", CurrentPath)}]");
            }

            if (entity != null)
            {
                var entityPos = entity.GetCurrentGridPosition();
                sb.AppendLine(culture, $"Entity: {entity.Name} at {entityPos}, IsMoving: {entity.IsMoving()}");
                sb.AppendLine(culture, $"IsGoalReached: {IsGoalReached(entity)}");
                sb.AppendLine(culture, $"HasValidPath: {HasValidPath()}");
            }

            if (_goalType == PathGoalType.Building && _targetBuilding != null)
            {
                var buildingPos = _targetBuilding.GetCurrentGridPosition();
                var buildingSize = _targetBuilding.GridSize;
                sb.AppendLine(culture, $"--- Building Goal Details ---");
                sb.AppendLine(culture, $"Building: {_targetBuilding.BuildingType} at {buildingPos}, size {buildingSize}");
                sb.AppendLine(culture, $"RequireInterior: {_requireInterior}");

                // Interior positions (from tile definitions)
                var interiorPositions = _targetBuilding.GetInteriorPositions();
                sb.AppendLine(culture, $"Interior positions ({interiorPositions.Count}): [{string.Join(", ", interiorPositions.Select(p => $"{p}->abs{buildingPos + p}"))}]");

                // Walkable interior positions (filtered by walkability)
                var walkableInterior = _targetBuilding.GetWalkableInteriorPositions();
                sb.AppendLine(culture, $"Walkable interior ({walkableInterior.Count}): [{string.Join(", ", walkableInterior.Select(p => $"{p}->abs{buildingPos + p}"))}]");

                // Perimeter positions if adjacency allowed
                if (!_requireInterior)
                {
                    var perimeterPositions = GetBuildingPerimeterPositions(buildingPos, buildingSize).ToList();
                    sb.AppendLine(culture, $"Perimeter positions ({perimeterPositions.Count}): [{string.Join(", ", perimeterPositions)}]");
                }
            }

            return sb.ToString();
        }
        catch (Exception e)
        {
            return $"GetDebugSummary ERROR: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
        }
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
            Log.Error("Due to thread safety we cannot modify global astar grids inside a Task");
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
        Log.Print($"Creating astar grid for {gridArea.Name}");
        return astar;
    }

    public static void UpdateAStarGrid(Area gridArea)
    {
        if (Task.CurrentId != null)
        {
            Log.Error("Due to thread safety we cannot modify global astar grids inside a Task");
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

    public void SetBuildingGoal(Being entity, Building targetBuilding, bool requireInterior = true)
    {
        _goalType = PathGoalType.Building;
        _targetBuilding = targetBuilding;
        _requireInterior = requireInterior;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
    }

    /// <summary>
    /// Set goal to navigate adjacent to a specific facility in a building.
    /// </summary>
    /// <param name="building">The building containing the facility.</param>
    /// <param name="facilityId">The facility ID (e.g., "oven", "quern", "storage", "crop").</param>
    /// <returns>True if a valid facility position was found, false otherwise.</returns>
    public bool SetFacilityGoal(Building building, string facilityId)
    {
        // Get facility positions from building
        var facilityPositions = building.GetFacilityPositions(facilityId);
        if (facilityPositions.Count == 0)
        {
            return false;
        }

        // Store the building and facility ID for smart recalculation
        _targetFacilityBuilding = building;
        _targetFacilityId = facilityId;
        _goalType = PathGoalType.Facility;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;

        // Actual facility selection happens in CalculatePathForCurrentGoal
        // which tries all facilities and finds one with a valid path
        return true;
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

                    // Build list of valid positions - interior, plus perimeter if adjacency allowed
                    var validPositions = new HashSet<Vector2I>();

                    // Interior positions (based on tile definitions, ignores current occupancy)
                    foreach (var relativePos in _targetBuilding.GetInteriorPositions())
                    {
                        validPositions.Add(buildingPos + relativePos);
                    }

                    // Perimeter positions if adjacency is acceptable
                    if (!_requireInterior)
                    {
                        foreach (var pos in GetBuildingPerimeterPositions(buildingPos, buildingSize))
                        {
                            validPositions.Add(pos);
                        }
                    }

                    result = validPositions.Contains(entityPos);
                }

                break;
            case PathGoalType.Facility:
                if (_targetFacilityPosition.HasValue)
                {
                    // Check if entity is adjacent to the facility (cardinal directions)
                    Vector2I diff = entityPos - _targetFacilityPosition.Value;
                    result = (Math.Abs(diff.X) + Math.Abs(diff.Y)) == 1;
                }

                break;
        }

        return result;
    }

    /// <summary>
    /// Check if entity is close to (within 3 tiles of) the goal.
    /// Used to be more persistent with pathfinding when almost there.
    /// </summary>
    private bool IsCloseToGoal(Being entity)
    {
        if (entity == null)
        {
            return false;
        }

        Vector2I entityPos = entity.GetCurrentGridPosition();
        const int closeDistance = 3;

        switch (_goalType)
        {
            case PathGoalType.None:
                return true;
            case PathGoalType.Position:
                return entityPos.DistanceSquaredTo(_targetPosition) <= closeDistance * closeDistance;
            case PathGoalType.EntityProximity:
                if (_targetEntity != null)
                {
                    Vector2I targetPos = _targetEntity.GetCurrentGridPosition();
                    return entityPos.DistanceSquaredTo(targetPos) <= (closeDistance + _proximityRange) * (closeDistance + _proximityRange);
                }

                return false;
            case PathGoalType.Area:
                return entityPos.DistanceSquaredTo(_targetPosition) <= (closeDistance + _proximityRange) * (closeDistance + _proximityRange);
            case PathGoalType.Building:
                if (_targetBuilding != null)
                {
                    // Use IsAdjacentToBuilding-style check with larger tolerance
                    Vector2I buildingPos = _targetBuilding.GetCurrentGridPosition();
                    Vector2I buildingSize = _targetBuilding.GridSize;
                    int minX = buildingPos.X - closeDistance;
                    int maxX = buildingPos.X + buildingSize.X + closeDistance - 1;
                    int minY = buildingPos.Y - closeDistance;
                    int maxY = buildingPos.Y + buildingSize.Y + closeDistance - 1;
                    return entityPos.X >= minX && entityPos.X <= maxX &&
                           entityPos.Y >= minY && entityPos.Y <= maxY;
                }

                return false;
            case PathGoalType.Facility:
                if (_targetFacilityPosition.HasValue)
                {
                    return entityPos.DistanceSquaredTo(_targetFacilityPosition.Value) <= closeDistance * closeDistance;
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Get alternative positions within the goal area, excluding the current position.
    /// Used for stepping aside while staying within work range.
    /// </summary>
    /// <param name="entity">The entity looking for step-aside positions.</param>
    /// <returns>List of valid positions within the goal area, sorted by distance from current position.</returns>
    public List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        var result = new List<Vector2I>();
        if (entity == null)
        {
            return result;
        }

        var entityPos = entity.GetCurrentGridPosition();
        var gridArea = entity.GetGridArea();

        switch (_goalType)
        {
            case PathGoalType.Facility:
                if (_targetFacilityPosition.HasValue)
                {
                    // Cardinal-adjacent positions to the facility
                    var directions = new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };
                    foreach (var dir in directions)
                    {
                        var pos = _targetFacilityPosition.Value + dir;
                        if (pos != entityPos && gridArea != null && gridArea.IsCellWalkable(pos))
                        {
                            result.Add(pos);
                        }
                    }
                }

                break;

            case PathGoalType.Building:
                if (_targetBuilding != null)
                {
                    var walkableInterior = _targetBuilding.GetWalkableInteriorPositions();
                    var buildingPos = _targetBuilding.GetCurrentGridPosition();
                    foreach (var relPos in walkableInterior)
                    {
                        var absPos = buildingPos + relPos;
                        if (absPos != entityPos && gridArea != null && gridArea.IsCellWalkable(absPos))
                        {
                            result.Add(absPos);
                        }
                    }
                }

                break;

            case PathGoalType.Area:
                // Positions within the area radius
                for (int dx = -_proximityRange; dx <= _proximityRange; dx++)
                {
                    for (int dy = -_proximityRange; dy <= _proximityRange; dy++)
                    {
                        if ((dx * dx) + (dy * dy) <= _proximityRange * _proximityRange)
                        {
                            var pos = _targetPosition + new Vector2I(dx, dy);
                            if (pos != entityPos && gridArea != null && gridArea.IsCellWalkable(pos))
                            {
                                result.Add(pos);
                            }
                        }
                    }
                }

                break;
        }

        // Sort by distance from current position (prefer nearby)
        result.Sort((a, b) => entityPos.DistanceSquaredTo(a).CompareTo(entityPos.DistanceSquaredTo(b)));
        return result;
    }

    // Method to follow the current path
    public bool TryFollowPath(Being entity)
    {
        if (entity == null)
        {
            Log.Error("TryFollowPath: Entity is null");
            return false;
        }

        // First check if we've reached the goal directly or entity is currently moving
        if (IsGoalReached(entity) || entity.IsMoving())
        {
            return true;
        }

        // Reset recalculation attempts when close to goal - be more persistent when almost there
        if (IsCloseToGoal(entity) && _recalculationAttempts > 0)
        {
            _recalculationAttempts = 0;
        }

        // Calculate path if needed and not on cooldown
        if (_firstGoalCalculation ||
            (_pathNeedsCalculation &&
              GameController.CurrentTick - _lastRecalculationTick >= RECALCULATIONCOOLDOWN &&
              _recalculationAttempts < MAXRECALCULATIONATTEMPTS))
        {
            _firstGoalCalculation = false;
            bool success = CalculatePathForCurrentGoal(entity);
            if (!success)
            {
                Log.Error($"Failed to calculate path for {entity.Name} with goal type {_goalType}");
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
            PathIndex++;
            if (PathIndex >= CurrentPath.Count)
            {
                // We're already at the end of the path
                _pathNeedsCalculation = true;
                return true;
            }

            nextPos = CurrentPath[PathIndex];
        }

        bool moveSuccessful = entity.TryMoveToGridPosition(nextPos);

        if (moveSuccessful)
        {
            PathIndex++;
            _recalculationAttempts = 0; // Reset throttle counter when making progress

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
            // Path is blocked
            // If entity is in queue, path is still valid - don't mark for recalculation
            // (the entity is intentionally waiting, not truly blocked)
            if (!entity.IsInQueue)
            {
                _pathNeedsCalculation = true;
            }

            return false;
        }

        return moveSuccessful;
    }

    // Calculate path based on current goal
    private bool CalculatePathForCurrentGoal(Being entity)
    {
        var gridArea = entity.GetGridArea();
        if (gridArea == null || gridArea.AStarGrid == null)
        {
            Log.Error("CalculatePathForCurrentGoal: GridArea is null");
            return false;
        }

        Vector2I startPos = entity.GetCurrentGridPosition();
        _recalculationAttempts++;

        // Reset current path before calculating new one
        CurrentPath = [];

        // Use perception-aware pathfinding grid
        // Village residents use the base grid, non-residents get perception-limited view
        var astar = CreatePathfindingGrid(entity);
        if (astar == null)
        {
            Log.Error("CalculatePathForCurrentGoal: Failed to create pathfinding grid");
            return false;
        }

        // Godot 4.6 behavior change: get_id_path returns empty if start position is solid.
        // Entities mark their own position as solid, so we must temporarily unmark it.
        bool startWasSolid = astar.IsPointSolid(startPos);
        if (startWasSolid)
        {
            astar.SetPointSolid(startPos, false);
        }

        try
        {
            // Calculate path based on goal type
            switch (_goalType)
            {
                case PathGoalType.Position:
                    // Check if target position is in bounds
                    if (!astar.IsInBoundsv(_targetPosition))
                    {
                        Log.Error($"Target position {_targetPosition} is out of bounds");
                        return false;
                    }

                    // If start and target are the same, create single-element path
                    if (startPos == _targetPosition)
                    {
                        CurrentPath = [startPos];
                        PathIndex = 0;
                        _pathNeedsCalculation = false;
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
                        Log.Error($"No path found to position {_targetPosition}");
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

                        // If already within proximity, create single-element path
                        if (Utils.WithinProximityRangeOf(startPos, targetPos, _proximityRange))
                        {
                            CurrentPath = [startPos];
                            PathIndex = 0;
                            _pathNeedsCalculation = false;
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
                            Log.Error($"No path found to entity {_targetEntity.Name}");
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("Target entity is null");
                        return false;
                    }

                    break;
                case PathGoalType.Area:
                    // If already within area, create single-element path
                    if (Utils.WithinProximityRangeOf(entity.GetCurrentGridPosition(), _targetPosition, _proximityRange))
                    {
                        CurrentPath = [startPos];
                        PathIndex = 0;
                        _pathNeedsCalculation = false;
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
                            Log.Error("No path found to any position in area");
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("No valid positions found in area");
                        return false;
                    }

                    break;

                case PathGoalType.Building:
                    if (_targetBuilding != null)
                    {
                        Vector2I buildingPos = _targetBuilding.GetCurrentGridPosition();
                        Vector2I buildingSize = _targetBuilding.GridSize;
                        bool foundPath = false;

                        // Build list of candidate positions - interior first, then perimeter if allowed
                        var candidatePositions = new List<Vector2I>();

                        // Debug tracking
                        var rejectedInterior = new List<(Vector2I pos, string reason)>();
                        var rejectedPerimeter = new List<(Vector2I pos, string reason)>();

                        // Add walkable interior positions
                        var walkableInterior = _targetBuilding.GetWalkableInteriorPositions();
                        foreach (var relativePos in walkableInterior)
                        {
                            Vector2I absolutePos = buildingPos + relativePos;
                            if (!astar.IsInBoundsv(absolutePos))
                            {
                                rejectedInterior.Add((absolutePos, "out of bounds"));
                            }
                            else if (astar.IsPointSolid(absolutePos))
                            {
                                rejectedInterior.Add((absolutePos, "solid in A*"));
                            }
                            else
                            {
                                candidatePositions.Add(absolutePos);
                            }
                        }

                        // Add perimeter positions if adjacency is acceptable
                        if (!_requireInterior)
                        {
                            foreach (var pos in GetBuildingPerimeterPositions(buildingPos, buildingSize))
                            {
                                if (!astar.IsInBoundsv(pos))
                                {
                                    rejectedPerimeter.Add((pos, "out of bounds"));
                                }
                                else if (astar.IsPointSolid(pos))
                                {
                                    rejectedPerimeter.Add((pos, "solid in A*"));
                                }
                                else
                                {
                                    candidatePositions.Add(pos);
                                }
                            }
                        }

                        // Sort by distance and try to path to each
                        candidatePositions.Sort((a, b) =>
                            startPos.DistanceSquaredTo(a).CompareTo(startPos.DistanceSquaredTo(b)));

                        // Track path failures
                        var pathFailures = new List<Vector2I>();

                        foreach (var pos in candidatePositions)
                        {
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
                            else
                            {
                                pathFailures.Add(pos);
                            }
                        }

                        if (!foundPath)
                        {
                            var culture = System.Globalization.CultureInfo.InvariantCulture;
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine(culture, $"No path found to building {_targetBuilding.BuildingType} at {buildingPos} (size {buildingSize})");
                            sb.AppendLine(culture, $"  Entity at: {startPos}, requireInterior: {_requireInterior}");
                            sb.AppendLine(culture, $"  Walkable interior count: {walkableInterior.Count}");
                            sb.AppendLine(culture, $"  Candidates accepted: {candidatePositions.Count}");

                            if (rejectedInterior.Count > 0)
                            {
                                sb.AppendLine(culture, $"  Rejected interior ({rejectedInterior.Count}):");
                                foreach (var (pos, reason) in rejectedInterior.Take(5))
                                {
                                    sb.AppendLine(culture, $"    {pos}: {reason}");
                                }

                                if (rejectedInterior.Count > 5)
                                {
                                    sb.AppendLine(culture, $"    ... and {rejectedInterior.Count - 5} more");
                                }
                            }

                            if (rejectedPerimeter.Count > 0)
                            {
                                sb.AppendLine(culture, $"  Rejected perimeter ({rejectedPerimeter.Count}):");
                                foreach (var (pos, reason) in rejectedPerimeter.Take(10))
                                {
                                    sb.AppendLine(culture, $"    {pos}: {reason}");
                                }

                                if (rejectedPerimeter.Count > 10)
                                {
                                    sb.AppendLine(culture, $"    ... and {rejectedPerimeter.Count - 10} more");
                                }
                            }

                            if (pathFailures.Count > 0)
                            {
                                sb.AppendLine(culture, $"  Path calculation failed for ({pathFailures.Count}):");
                                foreach (var pos in pathFailures.Take(5))
                                {
                                    sb.AppendLine(culture, $"    {pos}: no path from {startPos}");
                                }

                                if (pathFailures.Count > 5)
                                {
                                    sb.AppendLine(culture, $"    ... and {pathFailures.Count - 5} more");
                                }
                            }

                            Log.Error(sb.ToString());
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("Target building is null");
                        return false;
                    }

                    break;

                case PathGoalType.Facility:
                    // Smart recalculation: try ALL adjacent positions to ALL facilities of this ID
                    // Prefer facilities where adjacent positions are not occupied by other entities
                    // Deprioritize entrance and entrance-adjacent positions to avoid blocking doorways
                    if (_targetFacilityBuilding == null || _targetFacilityId == null)
                    {
                        Log.Error("Facility goal missing building or facility ID");
                        return false;
                    }

                    var facilityPositions = _targetFacilityBuilding.GetFacilityPositions(_targetFacilityId);
                    Vector2I facilityBuildingPos = _targetFacilityBuilding.GetCurrentGridPosition();

                    // Get entrance positions to avoid blocking doorways
                    var entrancePositions = new HashSet<Vector2I>(_targetFacilityBuilding.GetEntrancePositions());
                    var entranceAdjacentPositions = _targetFacilityBuilding.GetEntranceAdjacentPositions();

                    // Collect all valid (facilityPos, adjacentPos, blocksEntrance) candidates
                    // Note: We do NOT check entity occupancy here - that would be "god knowledge"
                    // Entities will handle encountering others via the blocking response system
                    var facilityCandidates = new List<(Vector2I facilityPos, Vector2I adjacentPos, bool blocksEntrance)>();

                    foreach (var relativePos in facilityPositions)
                    {
                        Vector2I absoluteFacilityPos = facilityBuildingPos + relativePos;

                        foreach (var adjacentPos in GetCardinalAdjacentPositions(absoluteFacilityPos))
                        {
                            // Check if position is in bounds
                            if (!astar.IsInBoundsv(adjacentPos))
                            {
                                continue;
                            }

                            // Check if this position is solid terrain (walls, water, etc.)
                            // This uses astar.IsPointSolid() which only knows about terrain/buildings, not entities
                            if (astar.IsPointSolid(adjacentPos))
                            {
                                continue;
                            }

                            // Check if this position blocks an entrance
                            bool blocksEntrance = entrancePositions.Contains(adjacentPos) ||
                                                  entranceAdjacentPositions.Contains(adjacentPos);

                            facilityCandidates.Add((absoluteFacilityPos, adjacentPos, blocksEntrance));
                        }
                    }

                    // Sort: non-entrance-blocking positions first, then by distance
                    facilityCandidates.Sort((a, b) =>
                    {
                        // Non-entrance-blocking positions first
                        if (a.blocksEntrance != b.blocksEntrance)
                        {
                            return a.blocksEntrance ? 1 : -1;
                        }

                        // Then by distance
                        return startPos.DistanceSquaredTo(a.adjacentPos)
                            .CompareTo(startPos.DistanceSquaredTo(b.adjacentPos));
                    });

                    bool foundFacilityPath = false;

                    foreach (var (facilityPos, adjacentPos, _) in facilityCandidates)
                    {
                        // If we're already at this position, we found it!
                        if (startPos == adjacentPos)
                        {
                            _targetFacilityPosition = facilityPos;
                            _targetPosition = adjacentPos;
                            foundFacilityPath = true;
                            break;
                        }

                        // Try to path to this position
                        var facilityPath = astar.GetIdPath(startPos, adjacentPos, true);
                        if (facilityPath.Count > 0)
                        {
                            _targetFacilityPosition = facilityPos;
                            _targetPosition = adjacentPos;
                            CurrentPath = facilityPath.Cast<Vector2I>().ToList();
                            foundFacilityPath = true;
                            break;
                        }
                    }

                    if (!foundFacilityPath)
                    {
                        Log.Error($"No path found to any '{_targetFacilityId}' facility in building {_targetFacilityBuilding.BuildingType}");
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
            _lastRecalculationTick = GameController.CurrentTick;
            return CurrentPath.Count > 0;
        }
        catch (Exception e)
        {
            Log.Error($"Exception in path calculation: {e.Message}\n{e.StackTrace}");
            return false;
        }
        finally
        {
            // Always restore start position solid state if we changed it
            if (startWasSolid)
            {
                astar.SetPointSolid(startPos, true);
            }
        }
    }

    // Get positions in a ring around a target
    private static List<Vector2I> GetPositionsAroundEntity(Vector2I center, int range)
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

    // Get cardinal adjacent positions (4 directions)
    private static IEnumerable<Vector2I> GetCardinalAdjacentPositions(Vector2I pos)
    {
        yield return pos + Vector2I.Up;
        yield return pos + Vector2I.Down;
        yield return pos + Vector2I.Left;
        yield return pos + Vector2I.Right;
    }

    // Get perimeter positions around a building (one tile outside the building bounds)
    private static IEnumerable<Vector2I> GetBuildingPerimeterPositions(Vector2I buildingPos, Vector2I buildingSize)
    {
        for (int x = -1; x <= buildingSize.X; x++)
        {
            for (int y = -1; y <= buildingSize.Y; y++)
            {
                if (x == -1 || y == -1 || x == buildingSize.X || y == buildingSize.Y)
                {
                    yield return new Vector2I(buildingPos.X + x, buildingPos.Y + y);
                }
            }
        }
    }

    // Get valid positions within an area
    private static List<Vector2I> GetValidPositionsInArea(Vector2I center, int radius, Grid.Area gridArea)
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

    /// <summary>
    /// Creates a perception-aware pathfinding grid for an entity.
    /// - Village residents use the base terrain grid as-is (they know the village layout)
    /// - Non-residents (undead, wanderers) can only pathfind within their perception range
    ///   by marking the border of their perception as solid walls (fog-of-war effect).
    /// </summary>
    /// <param name="entity">The entity doing the pathfinding.</param>
    /// <returns>A cloned AStarGrid2D with perception-based modifications, or the base grid for village residents.</returns>
    private AStarGrid2D? CreatePathfindingGrid(Being entity)
    {
        var gridArea = entity.GetGridArea();
        if (gridArea?.AStarGrid == null)
        {
            return null;
        }

        var baseGrid = gridArea.AStarGrid;

        // Village residents use the terrain grid directly - they know the village layout
        if (entity.IsVillageResident)
        {
            return baseGrid;
        }

        // Non-residents need a perception-limited grid
        // Clone the base grid and mark perception border as solid
        _cachedPathfindingGrid = CloneAStarGrid(baseGrid);

        // Mark the border of perception range as solid (fog-of-war wall)
        var entityPos = entity.GetCurrentGridPosition();
        int range = (int)entity.MaxSenseRange;

        // Iterate through the perception border ring (square boundary)
        for (int x = entityPos.X - range; x <= entityPos.X + range; x++)
        {
            for (int y = entityPos.Y - range; y <= entityPos.Y + range; y++)
            {
                // Check if this cell is ON the border (not inside)
                int dx = Math.Abs(x - entityPos.X);
                int dy = Math.Abs(y - entityPos.Y);
                if (dx == range || dy == range)
                {
                    var pos = new Vector2I(x, y);

                    // Don't mark the entity's own position as solid
                    if (pos == entityPos)
                    {
                        continue;
                    }

                    // Check bounds and mark as solid
                    if (_cachedPathfindingGrid.IsInBoundsv(pos))
                    {
                        _cachedPathfindingGrid.SetPointSolid(pos, true);
                    }
                }
            }
        }

        return _cachedPathfindingGrid;
    }

    /// <summary>
    /// Creates a deep clone of an AStarGrid2D, copying all configuration and solid/weight states.
    /// </summary>
    /// <param name="source">The source grid to clone.</param>
    /// <returns>A new AStarGrid2D with the same configuration and state as the source.</returns>
    private static AStarGrid2D CloneAStarGrid(AStarGrid2D source)
    {
        var clone = new AStarGrid2D
        {
            Region = source.Region,
            CellSize = source.CellSize,
            DiagonalMode = source.DiagonalMode,
            DefaultComputeHeuristic = source.DefaultComputeHeuristic,
            DefaultEstimateHeuristic = source.DefaultEstimateHeuristic
        };
        clone.Update();

        // Copy solid states and weights
        for (int x = 0; x < source.Region.Size.X; x++)
        {
            for (int y = 0; y < source.Region.Size.Y; y++)
            {
                var pos = new Vector2I(x + source.Region.Position.X, y + source.Region.Position.Y);
                clone.SetPointSolid(pos, source.IsPointSolid(pos));
                clone.SetPointWeightScale(pos, source.GetPointWeightScale(pos));
            }
        }

        return clone;
    }
}
