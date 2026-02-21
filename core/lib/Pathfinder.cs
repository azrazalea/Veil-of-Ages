using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Sensory;
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

    // Pooled HashSet for perceived entity positions - reused to reduce GC pressure
    private readonly HashSet<Vector2I> _perceivedEntityPositions = [];

    // Path state tracking
    private PathGoalType _goalType = PathGoalType.None;
    private Being? _targetEntity;
    private Vector2I _targetPosition;
    private int _proximityRange = 1;
    private bool _pathNeedsCalculation = true;
    private int _recalculationAttempts;
    private const int MAXRECALCULATIONATTEMPTS = 3;
    private uint _lastRecalculationTick;
    private const uint RECALCULATIONCOOLDOWN = 5;
    private bool _firstGoalCalculation;

    // Periodic perception-based recalculation
    // Every N successful steps, re-evaluate path with fresh perception to handle newly-seen entities
    private int _stepsSinceLastRecalculation;
    private const int STEPSBEFOREPERIODICRECALC = 5;

    // Facility goal tracking - stores the facility position(s) to check adjacency in IsGoalReached
    private Vector2I? _targetFacilityPosition;
    private List<Vector2I>? _targetFacilityPositions;

    // Room goal: if true, entity must be inside; if false, adjacent is acceptable
    private bool _requireInterior;

    // Room goal tracking (new Rimworld-style architecture)
    private Room? _targetRoom;

    // Cross-area navigation state
    // When the target building/facility is in a different area, PathFinder internally
    // plans the route (via WorldNavigator.FindRouteToArea which uses entity's knowledge —
    // NO god knowledge), walks to transition points, and signals when a transition is needed.
    private List<TransitionPoint>? _crossAreaRoute;
    private int _crossAreaRouteIndex;
    private bool _needsAreaTransition;
    private TransitionPoint? _pendingTransition;

    // Final goal storage — the real goal, restored after all area transitions complete.
    // While cross-area navigation is active, _goalType is set to intermediate Position goals.
    private PathGoalType _finalGoalType = PathGoalType.None;
    private Room? _finalGoalRoom;
    private string? _finalGoalFacilityId;
    private bool _finalRequireInterior;

    /// <summary>
    /// Gets a value indicating whether true when the entity has reached a transition point and needs to change area.
    /// NavigationActivity checks this and returns a ChangeAreaAction.
    /// </summary>
    public bool NeedsAreaTransition => _needsAreaTransition;

    /// <summary>
    /// Gets the transition point the entity needs to traverse. Only valid when NeedsAreaTransition is true.
    /// </summary>
    public TransitionPoint? PendingTransition => _pendingTransition;

    // Simple enum to track goal type
    public enum PathGoalType
    {
        None,
        Position,
        EntityProximity,
        Area,
        Facility,
        Room
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

            if (_goalType == PathGoalType.Room && _targetRoom != null)
            {
                sb.AppendLine(culture, $"--- Room Goal Details ---");
                sb.AppendLine(culture, $"Room: {_targetRoom.Name} (Id: {_targetRoom.Id})");
                sb.AppendLine(culture, $"RequireInterior: {_requireInterior}");
                sb.AppendLine(culture, $"Tiles: {_targetRoom.Tiles.Count}");
                sb.AppendLine(culture, $"Doors: {_targetRoom.Doors.Count}");
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
        _stepsSinceLastRecalculation = 0;

        // Clear cross-area route state — HandleCrossAreaPlanning will re-plan if needed.
        // Keep _finalGoalType and stored goal params so it knows what to plan toward.
        _crossAreaRoute = null;
        _crossAreaRouteIndex = 0;
        _needsAreaTransition = false;
        _pendingTransition = null;
    }

    public void Reset()
    {
        ClearPath();
        _goalType = PathGoalType.None;
        _firstGoalCalculation = false;
        _targetFacilityPositions = null;

        // Clear final goal storage (ClearPath preserves these)
        _finalGoalType = PathGoalType.None;
        _finalGoalRoom = null;
        _finalGoalFacilityId = null;
        _finalRequireInterior = false;
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

    /// <summary>
    /// Set goal to navigate into a room (Rimworld-style architecture).
    /// Uses room's walkable tiles as candidates, with doors as entrance points.
    /// Cross-area capable via WorldNavigator.
    /// </summary>
    /// <param name="entity">The entity navigating (used for cross-area detection).</param>
    /// <param name="room">The room to navigate to.</param>
    /// <param name="requireInterior">If true, entity must reach interior; if false, adjacent to door is acceptable.</param>
    public void SetRoomGoal(Being entity, Room room, bool requireInterior = true)
    {
        // Cross-area detection: if entity and room are in different areas,
        // store the final goal and defer route planning to CalculatePathIfNeeded.
        if (entity.GridArea != null && room.GridArea != null
            && entity.GridArea != room.GridArea)
        {
            _finalGoalType = PathGoalType.Room;
            _finalGoalRoom = room;
            _finalRequireInterior = requireInterior;
            _finalGoalFacilityId = null;
            _goalType = PathGoalType.None;
            _pathNeedsCalculation = true;
            _firstGoalCalculation = true;
            _recalculationAttempts = 0;
            return;
        }

        // Same-area: set room goal directly
        _goalType = PathGoalType.Room;
        _targetRoom = room;
        _requireInterior = requireInterior;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
    }

    /// <summary>
    /// Set goal to navigate adjacent to a specific facility.
    /// Cross-area capable: detects if facility is in a different area and defers routing.
    /// </summary>
    /// <param name="entity">The entity navigating (used for cross-area detection).</param>
    /// <param name="facility">The facility to navigate to.</param>
    /// <returns>True if a valid facility goal was established, false otherwise.</returns>
    public bool SetFacilityGoal(Being entity, Facility facility)
    {
        var facilityArea = facility.ContainingRoom?.GridArea ?? facility.GridArea;

        // Cross-area detection: if entity and facility are in different areas,
        // store the final goal and defer route planning to CalculatePathIfNeeded.
        if (entity.GridArea != null && facilityArea != null
            && entity.GridArea != facilityArea)
        {
            _finalGoalType = PathGoalType.Facility;
            _finalGoalRoom = facility.ContainingRoom;
            _finalGoalFacilityId = facility.Id;
            _finalRequireInterior = false;
            _goalType = PathGoalType.None;
            _pathNeedsCalculation = true;
            _firstGoalCalculation = true;
            _recalculationAttempts = 0;
            return true;
        }

        // Same-area: set position(s) directly for pathfinding.
        _targetFacilityPositions = new List<Vector2I>(facility.Positions);
        _targetFacilityPosition = facility.GridPosition;
        _goalType = PathGoalType.Facility;
        _firstGoalCalculation = true;
        _pathNeedsCalculation = true;
        _recalculationAttempts = 0;
        return true;
    }

    // Check if goal is reached
    public bool IsGoalReached(Being entity)
    {
        if (entity == null)
        {
            return false;
        }

        // During cross-area navigation, the real goal hasn't been reached yet —
        // we're still navigating intermediate positions or waiting for transitions.
        if (_finalGoalType != PathGoalType.None)
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
            case PathGoalType.Facility:
                if (_targetFacilityPositions is { Count: > 0 })
                {
                    // Check if entity is adjacent to ANY facility position (including diagonals)
                    foreach (var facilityPos in _targetFacilityPositions)
                    {
                        if (DirectionUtils.IsAdjacent(entityPos, facilityPos))
                        {
                            result = true;
                            break;
                        }
                    }
                }
                else if (_targetFacilityPosition.HasValue)
                {
                    result = DirectionUtils.IsAdjacent(entityPos, _targetFacilityPosition.Value);
                }

                break;
            case PathGoalType.Room:
                if (_targetRoom != null)
                {
                    // Check if entity is inside the room
                    result = _targetRoom.ContainsAbsolutePosition(entityPos);

                    // If adjacency is acceptable, also check adjacent to any door
                    if (!result && !_requireInterior)
                    {
                        foreach (var door in _targetRoom.Doors)
                        {
                            if (DirectionUtils.IsAdjacent(entityPos, door.GridPosition))
                            {
                                result = true;
                                break;
                            }
                        }
                    }
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
            case PathGoalType.Facility:
                if (_targetFacilityPositions is { Count: > 0 })
                {
                    foreach (var facilityPos in _targetFacilityPositions)
                    {
                        if (entityPos.DistanceSquaredTo(facilityPos) <= closeDistance * closeDistance)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (_targetFacilityPosition.HasValue)
                {
                    return entityPos.DistanceSquaredTo(_targetFacilityPosition.Value) <= closeDistance * closeDistance;
                }

                return false;
            case PathGoalType.Room:
                if (_targetRoom != null)
                {
                    // Check if close to any door (primary entrance points)
                    foreach (var door in _targetRoom.Doors)
                    {
                        if (entityPos.DistanceSquaredTo(door.GridPosition) <= closeDistance * closeDistance)
                        {
                            return true;
                        }
                    }

                    // Also check if already inside the room
                    return _targetRoom.ContainsAbsolutePosition(entityPos);
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
                if (_targetFacilityPositions is { Count: > 0 })
                {
                    var seen = new HashSet<Vector2I>();
                    foreach (var facilityPos in _targetFacilityPositions)
                    {
                        // Adjacent positions to the facility (cardinal + diagonal)
                        foreach (var dir in DirectionUtils.All)
                        {
                            var pos = facilityPos + dir;
                            if (pos != entityPos && gridArea != null && gridArea.IsCellWalkable(pos) && seen.Add(pos))
                            {
                                result.Add(pos);
                            }
                        }
                    }
                }
                else if (_targetFacilityPosition.HasValue)
                {
                    // Adjacent positions to the facility (cardinal + diagonal)
                    foreach (var dir in DirectionUtils.All)
                    {
                        var pos = _targetFacilityPosition.Value + dir;
                        if (pos != entityPos && gridArea != null && gridArea.IsCellWalkable(pos))
                        {
                            result.Add(pos);
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

            case PathGoalType.Room:
                if (_targetRoom != null)
                {
                    // Walkable tiles inside the room
                    foreach (var tile in _targetRoom.Tiles)
                    {
                        if (tile != entityPos && gridArea != null && gridArea.IsCellWalkable(tile))
                        {
                            result.Add(tile);
                        }
                    }
                }

                break;
        }

        // Sort by distance from current position (prefer nearby)
        result.Sort((a, b) => entityPos.DistanceSquaredTo(a).CompareTo(entityPos.DistanceSquaredTo(b)));
        return result;
    }

    /// <summary>
    /// Calculate path if needed. MUST be called from the Think thread (background).
    /// This is where A* calculation happens - never during Execute().
    /// </summary>
    /// <param name="entity">The entity to calculate path for.</param>
    /// <param name="perception">Perception data for perception-aware pathfinding. The entity will
    /// path around other entities it can currently see.</param>
    /// <returns>True if a valid path exists (calculated or already cached), false if path calculation failed.</returns>
    public bool CalculatePathIfNeeded(Being entity, Perception perception)
    {
        if (entity == null)
        {
            Log.Error("CalculatePathIfNeeded: Entity is null");
            return false;
        }

        // Cross-area: if waiting for area transition, let NavigationActivity handle it
        if (_needsAreaTransition)
        {
            return true;
        }

        // Cross-area: if we have a final goal in another area, handle route planning.
        // Uses WorldNavigator.FindRouteToArea which queries entity's knowledge (BDI-compliant).
        if (_finalGoalType != PathGoalType.None)
        {
            if (!HandleCrossAreaPlanning(entity))
            {
                return false;
            }

            // If HandleCrossAreaPlanning signaled a transition, we're done for this tick
            if (_needsAreaTransition)
            {
                return true;
            }

            // Otherwise it set an intermediate goal — fall through to normal path calc
        }

        // If goal is already reached, no path needed
        if (IsGoalReached(entity))
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

            // Update tick BEFORE calculation so failures also respect cooldown
            _lastRecalculationTick = GameController.CurrentTick;
            bool success = CalculatePathForCurrentGoal(entity, perception);
            if (!success)
            {
                Log.Error($"Failed to calculate path for {entity.Name} with goal type {_goalType}");
                return false;
            }
        }

        return HasValidPath() || IsGoalReached(entity);
    }

    /// <summary>
    /// Follow a pre-calculated path. MUST be called from the Execute thread (main).
    /// Does NOT calculate paths - only follows what was already calculated in Think().
    /// If path needs recalculation, marks it for next Think() cycle and returns false.
    /// </summary>
    /// <param name="entity">The entity to move along the path.</param>
    /// <returns>True if movement succeeded or goal reached, false if blocked or no valid path.</returns>
    public bool FollowPath(Being entity)
    {
        if (entity == null)
        {
            Log.Error("FollowPath: Entity is null");
            return false;
        }

        // First check if we've reached the goal directly or entity is currently moving
        if (IsGoalReached(entity) || entity.IsMoving())
        {
            return true;
        }

        // If no valid path, can't follow - need to calculate in next Think()
        if (!HasValidPath())
        {
            _pathNeedsCalculation = true;
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
            _stepsSinceLastRecalculation++;

            // Periodic perception-based recalculation: every N steps, re-evaluate path
            // This handles "new entity appeared in my way, path around it" scenarios
            if (_stepsSinceLastRecalculation >= STEPSBEFOREPERIODICRECALC)
            {
                _pathNeedsCalculation = true;
                _stepsSinceLastRecalculation = 0;
            }

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
    private bool CalculatePathForCurrentGoal(Being entity, Perception perception)
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

        // Collect positions of perceived entities to path around
        // Entity will try to path around beings it can see
        var perceivedEntityPositions = GetPerceivedEntityPositions(entity, perception);

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
                        _stepsSinceLastRecalculation = 0;
                        return true;
                    }

                    // Early exit: Check if entity can leave current position
                    if (!CanLeavePosition(astar, startPos))
                    {
                        Log.Error($"Entity at {startPos} is surrounded - cannot leave position");
                        return false;
                    }

                    // Get path to specific position
                    var positionPath = ThreadSafeAStar.GetPath(astar, startPos, _targetPosition, true, perceivedEntityPositions);

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
                    if (_targetEntity == null)
                    {
                        Log.Error("Target entity is null");
                        return false;
                    }

                    Vector2I entityTargetPos = _targetEntity.GetCurrentGridPosition();

                    if (_targetEntity.IsMoving())
                    {
                        _recalculationAttempts = 0;
                        _pathNeedsCalculation = true;
                        return true;
                    }

                    // If already within proximity, create single-element path
                    if (Utils.WithinProximityRangeOf(startPos, entityTargetPos, _proximityRange))
                    {
                        CurrentPath = [startPos];
                        PathIndex = 0;
                        _pathNeedsCalculation = false;
                        _stepsSinceLastRecalculation = 0;
                        return true;
                    }

                    // Get walkable positions around target, filtering out perceived blocked
                    var proximityPositions = GetPositionsAroundEntity(entityTargetPos, 1)
                        .Where(pos => astar.IsInBoundsv(pos) &&
                                      !astar.IsPointSolid(pos) &&
                                      (perceivedEntityPositions == null || !perceivedEntityPositions.Contains(pos)))
                        .ToList();

                    var proximityResult = TryPathToCandidates(astar, startPos, proximityPositions, perceivedEntityPositions);
                    if (!proximityResult.Found)
                    {
                        Log.Error($"No path found to entity {_targetEntity.Name}");
                        return false;
                    }

                    CurrentPath = proximityResult.Path;

                    break;
                case PathGoalType.Area:
                    // If already within area, create single-element path
                    if (Utils.WithinProximityRangeOf(entity.GetCurrentGridPosition(), _targetPosition, _proximityRange))
                    {
                        CurrentPath = [startPos];
                        PathIndex = 0;
                        _pathNeedsCalculation = false;
                        _stepsSinceLastRecalculation = 0;
                        return true;
                    }

                    // Get area positions and ensure they're not solid
                    var areaPositions = GetValidPositionsInArea(_targetPosition, _proximityRange, gridArea);
                    if (areaPositions.Count == 0)
                    {
                        Log.Error("No valid positions found in area");
                        return false;
                    }

                    var areaResult = TryPathToCandidates(astar, startPos, areaPositions, perceivedEntityPositions);
                    if (!areaResult.Found)
                    {
                        Log.Error("No path found to any position in area");
                        return false;
                    }

                    CurrentPath = areaResult.Path;

                    break;

                case PathGoalType.Facility:
                    if (_targetFacilityPositions is not { Count: > 0 } && !_targetFacilityPosition.HasValue)
                    {
                        Log.Error("Facility goal missing target position");
                        return false;
                    }

                    // Collect adjacent tiles from ALL facility positions (multi-position support).
                    var facilityAdjacentPositions = new List<Vector2I>();
                    var seenAdjacentPositions = new HashSet<Vector2I>();
                    var positionsToCheck = _targetFacilityPositions ?? (_targetFacilityPosition.HasValue ? [_targetFacilityPosition.Value] : []);
                    foreach (var facPos in positionsToCheck)
                    {
                        foreach (var adjPos in GetAdjacentPositions(facPos))
                        {
                            if (IsValidCandidate(astar, adjPos, perceivedEntityPositions) && seenAdjacentPositions.Add(adjPos))
                            {
                                facilityAdjacentPositions.Add(adjPos);
                            }
                        }
                    }

                    var facilityResult = TryPathToCandidates(astar, startPos, facilityAdjacentPositions, perceivedEntityPositions);
                    if (!facilityResult.Found)
                    {
                        Log.Error($"No path to facility: {facilityResult.FailureReason}");
                        return false;
                    }

                    if (facilityResult.SuccessIndex >= 0 && facilityResult.SuccessIndex < facilityAdjacentPositions.Count)
                    {
                        _targetPosition = facilityAdjacentPositions[facilityResult.SuccessIndex];
                    }

                    CurrentPath = facilityResult.Path;

                    break;

                case PathGoalType.Room:
                    if (_targetRoom == null)
                    {
                        Log.Error("Target room is null");
                        return false;
                    }

                    // Collect walkable tiles in the room as candidates
                    var roomCandidates = new List<Vector2I>();
                    foreach (var tile in _targetRoom.Tiles)
                    {
                        if (IsValidCandidate(astar, tile, perceivedEntityPositions))
                        {
                            roomCandidates.Add(tile);
                        }
                    }

                    // If not requiring interior, also add positions adjacent to doors
                    if (!_requireInterior)
                    {
                        foreach (var door in _targetRoom.Doors)
                        {
                            foreach (var adjacentPos in GetAdjacentPositions(door.GridPosition))
                            {
                                if (!_targetRoom.ContainsAbsolutePosition(adjacentPos) &&
                                    IsValidCandidate(astar, adjacentPos, perceivedEntityPositions))
                                {
                                    roomCandidates.Add(adjacentPos);
                                }
                            }
                        }
                    }

                    var roomResult = TryPathToCandidates(astar, startPos, roomCandidates, perceivedEntityPositions);
                    if (!roomResult.Found)
                    {
                        Log.Error($"No path to room {_targetRoom.Name}: {roomResult.FailureReason} (tried {roomResult.CandidatesTried}/{roomCandidates.Count} candidates)");
                        return false;
                    }

                    CurrentPath = roomResult.Path;

                    break;
            }

            // Limit path length if needed
            if (CurrentPath.Count > MAXPATHLENGTH)
            {
                CurrentPath = CurrentPath.GetRange(0, MAXPATHLENGTH);
            }

            PathIndex = 0;
            _pathNeedsCalculation = false;
            _stepsSinceLastRecalculation = 0; // Reset step counter on fresh path
            return CurrentPath.Count > 0;
        }
        catch (Exception e)
        {
            Log.Error($"Exception in path calculation: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // Get positions in a ring around a target (all 8 directions — cardinal first)
    private static List<Vector2I> GetPositionsAroundEntity(Vector2I center, int range)
    {
        var result = new List<Vector2I>();

        // Always add all 8 adjacent positions (cardinal first for preference)
        foreach (var dir in DirectionUtils.All)
        {
            result.Add(center + dir);
        }

        // For larger ranges, add positions at further distances
        if (range > 1)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    // Skip the inner ring already added
                    if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
                    {
                        continue;
                    }

                    result.Add(new Vector2I(center.X + dx, center.Y + dy));
                }
            }
        }

        return result;
    }

    // Check if entity can leave their current position (has at least one non-blocked neighbor)
    // This is a quick early-exit check to avoid expensive A* when completely surrounded
    private static bool CanLeavePosition(AStarGrid2D astar, Vector2I pos)
    {
        // Check all 8 directions (diagonal movement is allowed)
        // Only real solids (walls, terrain) count — entity-occupied tiles are just expensive, not blocking
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var neighbor = new Vector2I(pos.X + dx, pos.Y + dy);

                // Must be in bounds
                if (!astar.IsInBoundsv(neighbor))
                {
                    continue;
                }

                // Must not be solid terrain
                if (astar.IsPointSolid(neighbor))
                {
                    continue;
                }

                // Found at least one valid exit
                return true;
            }
        }

        return false;
    }

    // Get adjacent positions (all 8 directions — cardinal first)
    private static IEnumerable<Vector2I> GetAdjacentPositions(Vector2I pos)
    {
        foreach (var dir in DirectionUtils.All)
        {
            yield return pos + dir;
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
    /// Result of trying to find a path to candidate positions.
    /// </summary>
    private readonly struct PathSearchResult
    {
        public readonly bool Found;
        public readonly List<Vector2I> Path;
        public readonly int SuccessIndex; // Index of successful candidate, or -1 for partial
        public readonly string FailureReason;
        public readonly int CandidatesTried;

        private PathSearchResult(bool found, List<Vector2I> path, int successIndex, string failureReason, int candidatesTried)
        {
            Found = found;
            Path = path;
            SuccessIndex = successIndex;
            FailureReason = failureReason;
            CandidatesTried = candidatesTried;
        }

        public static PathSearchResult Success(List<Vector2I> path, int index) =>
            new (true, path, index, string.Empty, 0);

        public static PathSearchResult PartialSuccess(List<Vector2I> path, int candidatesTried) =>
            new (true, path, -1, string.Empty, candidatesTried);

        public static PathSearchResult Fail(string reason, int candidatesTried) =>
            new (false, [], -1, reason, candidatesTried);
    }

    /// <summary>
    /// Try to find a path to any of the candidate positions.
    /// First tries exact paths (fast fail), then partial path as fallback.
    /// </summary>
    private static PathSearchResult TryPathToCandidates(
        AStarGrid2D astar,
        Vector2I startPos,
        List<Vector2I> candidates,
        HashSet<Vector2I>? perceivedWeights)
    {
        if (candidates.Count == 0)
        {
            return PathSearchResult.Fail("no candidates", 0);
        }

        // Check if already at any candidate
        for (int i = 0; i < candidates.Count; i++)
        {
            if (startPos == candidates[i])
            {
                return PathSearchResult.Success([startPos], i);
            }
        }

        // Early exit if surrounded by real solids
        if (!CanLeavePosition(astar, startPos))
        {
            return PathSearchResult.Fail("surrounded", 0);
        }

        // Try exact paths first (fast fail if unreachable)
        for (int i = 0; i < candidates.Count; i++)
        {
            var path = ThreadSafeAStar.GetPath(astar, startPos, candidates[i], false, perceivedWeights);
            if (path.Count > 0)
            {
                return PathSearchResult.Success(path, i);
            }
        }

        // All exact paths failed - try partial path to first candidate
        var partialPath = ThreadSafeAStar.GetPath(astar, startPos, candidates[0], true, perceivedWeights);
        if (partialPath.Count > 1) // Must make progress
        {
            return PathSearchResult.PartialSuccess(partialPath, candidates.Count);
        }

        return PathSearchResult.Fail($"no path to any of {candidates.Count} candidates", candidates.Count);
    }

    /// <summary>
    /// Check if a position is a valid pathfinding candidate.
    /// </summary>
    private static bool IsValidCandidate(AStarGrid2D astar, Vector2I pos, HashSet<Vector2I>? perceivedBlocked)
    {
        if (!astar.IsInBoundsv(pos))
        {
            return false;
        }

        if (astar.IsPointSolid(pos))
        {
            return false;
        }

        if (perceivedBlocked != null && perceivedBlocked.Contains(pos))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called by NavigationActivity after it returns a ChangeAreaAction.
    /// Advances to the next step in the cross-area route.
    /// The entity hasn't actually transitioned yet — ChangeAreaAction handles that.
    /// On the next tick, HandleCrossAreaPlanning will determine the next step.
    /// </summary>
    public void CompleteTransition(Being entity)
    {
        _needsAreaTransition = false;
        _pendingTransition = null;
        _crossAreaRouteIndex++;

        // Clear current path data — entity will be in a new area after ChangeAreaAction executes
        CurrentPath = [];
        PathIndex = 0;
        _pathNeedsCalculation = true;
        _firstGoalCalculation = true;
        _stepsSinceLastRecalculation = 0;
        _recalculationAttempts = 0;
        _goalType = PathGoalType.None; // HandleCrossAreaPlanning sets this on next tick
    }

    /// <summary>
    /// Handles cross-area route planning when the final goal is in another area.
    /// Called from CalculatePathIfNeeded when _finalGoalType != None.
    /// Uses WorldNavigator.FindRouteToArea which queries entity's SharedKnowledge (BDI-compliant).
    /// </summary>
    /// <returns>True if planning succeeded (may have set an intermediate goal or signaled transition),
    /// false if no route could be found.</returns>
    private bool HandleCrossAreaPlanning(Being entity)
    {
        var targetArea = _finalGoalRoom?.GridArea;
        if (targetArea == null)
        {
            Log.Error("HandleCrossAreaPlanning: final goal has no GridArea");
            return false;
        }

        // 1. Entity now in target area? Restore the real goal and let normal calc handle it.
        if (entity.GridArea == targetArea)
        {
            SetFinalGoal(entity);
            return true;
        }

        var currentArea = entity.GridArea;
        if (currentArea == null)
        {
            Log.Error("HandleCrossAreaPlanning: entity has no GridArea");
            return false;
        }

        // 2. No route planned? Plan one using entity's knowledge (no god knowledge).
        if (_crossAreaRoute == null)
        {
            _crossAreaRoute = WorldNavigator.FindRouteToArea(entity, currentArea, targetArea);
            _crossAreaRouteIndex = 0;

            if (_crossAreaRoute == null)
            {
                Log.Error($"No cross-area route from {currentArea.Name} to {targetArea.Name} (entity lacks knowledge?)");
                return false;
            }
        }

        // 3. All transitions traversed but still not in target area? Re-plan.
        if (_crossAreaRouteIndex >= _crossAreaRoute.Count)
        {
            _crossAreaRoute = null;
            return HandleCrossAreaPlanning(entity);
        }

        var currentTransition = _crossAreaRoute[_crossAreaRouteIndex];

        // 4. Entity at the current transition point? Signal that NavigationActivity
        //    should return a ChangeAreaAction.
        if (entity.GetCurrentGridPosition() == currentTransition.SourcePosition
            && entity.GridArea == currentTransition.SourceArea)
        {
            _needsAreaTransition = true;
            _pendingTransition = currentTransition;
            return true;
        }

        // 5. Set intermediate position goal to walk to the transition point.
        _goalType = PathGoalType.Position;
        _targetPosition = currentTransition.SourcePosition;
        _pathNeedsCalculation = true;
        _firstGoalCalculation = true;
        _recalculationAttempts = 0;
        return true;
    }

    /// <summary>
    /// Restores the real goal (Room or Facility) after all area transitions are done.
    /// Called from HandleCrossAreaPlanning when entity has arrived in the target area.
    /// </summary>
    private void SetFinalGoal(Being entity)
    {
        var goalType = _finalGoalType;
        var room = _finalGoalRoom;
        var facilityId = _finalGoalFacilityId;
        var requireInterior = _finalRequireInterior;

        // Clear all cross-area state
        _finalGoalType = PathGoalType.None;
        _finalGoalRoom = null;
        _finalGoalFacilityId = null;
        _finalRequireInterior = false;
        _crossAreaRoute = null;
        _crossAreaRouteIndex = 0;

        // Set the real goal — entity is now in the same area as the target,
        // so these will NOT detect cross-area and will use normal pathfinding.
        switch (goalType)
        {
            case PathGoalType.Facility:
                // Resolve the facility from the room to restore the position-based goal
                if (room != null && facilityId != null)
                {
                    var facility = room.GetFacility(facilityId);
                    if (facility != null)
                    {
                        SetFacilityGoal(entity, facility);
                        return;
                    }
                }

                Log.Error($"SetFinalGoal: Could not resolve facility '{facilityId}' in room '{room?.Name}'");
                break;
            case PathGoalType.Room:
                if (room != null)
                {
                    SetRoomGoal(entity, room, requireInterior);
                }

                break;
        }
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
    /// Extracts positions of perceived entities (excluding self) to treat as blocked during pathfinding.
    /// Entity will try to path around beings it can see.
    /// Uses pooled HashSet to reduce GC pressure.
    /// </summary>
    /// <param name="entity">The entity doing pathfinding (excluded from blocked positions).</param>
    /// <param name="perception">Current perception data.</param>
    /// <returns>HashSet of positions occupied by perceived beings, or null if none.</returns>
    private HashSet<Vector2I>? GetPerceivedEntityPositions(Being entity, Perception perception)
    {
        _perceivedEntityPositions.Clear();

        foreach (var (sensable, position) in perception.GetAllDetected())
        {
            // Only add weight penalties for other Beings (not buildings or other sensables)
            if (sensable is Being otherBeing && otherBeing != entity)
            {
                _perceivedEntityPositions.Add(position);
            }
        }

        // Return null if no weighted positions (optimization for ThreadSafeAStar)
        return _perceivedEntityPositions.Count > 0 ? _perceivedEntityPositions : null;
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
