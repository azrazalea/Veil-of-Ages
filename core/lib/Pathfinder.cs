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

        Vector2I buildingPos = building.GetCurrentGridPosition();

        // Find a facility position that has an adjacent walkable tile
        foreach (var relativePos in facilityPositions)
        {
            Vector2I absoluteFacilityPos = buildingPos + relativePos;
            Vector2I? adjacentPos = building.GetAdjacentWalkablePosition(relativePos);

            if (adjacentPos.HasValue)
            {
                _targetFacilityPosition = absoluteFacilityPos;

                // Set the actual pathfinding goal to the adjacent walkable position
                Vector2I absoluteAdjacentPos = buildingPos + adjacentPos.Value;
                _targetPosition = absoluteAdjacentPos;
                _goalType = PathGoalType.Facility;
                _firstGoalCalculation = true;
                _pathNeedsCalculation = true;
                _recalculationAttempts = 0;

                return true;
            }
        }

        return false; // No accessible facility found
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

    // Method to follow the current path
    public bool TryFollowPath(Being entity, bool secondTry = false)
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
                Log.Error($"Move failed for {entity.Name} to {nextPos}");
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
            Log.Error("CalculatePathForCurrentGoal: GridArea is null");
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
                        Log.Error($"Target position {_targetPosition} is out of bounds");
                        return false;
                    }

                    // If start and target are the same, no path needed
                    if (startPos == _targetPosition)
                    {
                        Log.Print("Target and start are same");
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

                        // Add walkable interior positions
                        var walkableInterior = _targetBuilding.GetWalkableInteriorPositions();
                        foreach (var relativePos in walkableInterior)
                        {
                            Vector2I absolutePos = buildingPos + relativePos;
                            if (astar.IsInBoundsv(absolutePos) && !astar.IsPointSolid(absolutePos))
                            {
                                candidatePositions.Add(absolutePos);
                            }
                        }

                        // Add perimeter positions if adjacency is acceptable
                        if (!_requireInterior)
                        {
                            foreach (var pos in GetBuildingPerimeterPositions(buildingPos, buildingSize))
                            {
                                if (astar.IsInBoundsv(pos) && !astar.IsPointSolid(pos))
                                {
                                    candidatePositions.Add(pos);
                                }
                            }
                        }

                        // Sort by distance and try to path to each
                        candidatePositions.Sort((a, b) =>
                            startPos.DistanceSquaredTo(a).CompareTo(startPos.DistanceSquaredTo(b)));

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
                        }

                        if (!foundPath)
                        {
                            Log.Error($"No path found to building {_targetBuilding.BuildingType} - tried {candidatePositions.Count} positions");
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
                    if (_targetFacilityBuilding == null || _targetFacilityId == null)
                    {
                        Log.Error("Facility goal missing building or facility ID");
                        return false;
                    }

                    var facilityPositions = _targetFacilityBuilding.GetFacilityPositions(_targetFacilityId);
                    Vector2I facilityBuildingPos = _targetFacilityBuilding.GetCurrentGridPosition();

                    bool foundFacilityPath = false;

                    // Try all facilities and all their adjacent positions
                    foreach (var relativePos in facilityPositions)
                    {
                        Vector2I absoluteFacilityPos = facilityBuildingPos + relativePos;

                        // Get adjacent positions (cardinal directions)
                        foreach (var adjacentPos in GetCardinalAdjacentPositions(absoluteFacilityPos))
                        {
                            // Check if position is in bounds
                            if (!astar.IsInBoundsv(adjacentPos))
                            {
                                continue;
                            }

                            // Check if this position is actually walkable (accounts for entities)
                            if (!gridArea.IsCellWalkable(adjacentPos))
                            {
                                continue;
                            }

                            // If we're already at this position, we found it!
                            if (startPos == adjacentPos)
                            {
                                _targetFacilityPosition = absoluteFacilityPos;
                                _targetPosition = adjacentPos;
                                foundFacilityPath = true;
                                break;
                            }

                            // Try to path to this position
                            var facilityPath = astar.GetIdPath(startPos, adjacentPos, true);
                            if (facilityPath.Count > 0)
                            {
                                // Found a valid path!
                                _targetFacilityPosition = absoluteFacilityPos;
                                _targetPosition = adjacentPos;
                                CurrentPath = facilityPath.Cast<Vector2I>().ToList();
                                foundFacilityPath = true;
                                break;
                            }
                        }

                        if (foundFacilityPath)
                        {
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
}
