using System;
using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Thread-safe A* pathfinding that reads from Godot's AStarGrid2D but uses its own
/// scoring state. This avoids the thread-safety issues with Godot's GetIdPath which
/// modifies internal Point state during pathfinding.
///
/// Also supports additional blocked positions (e.g., perceived entities) without
/// modifying the underlying grid.
/// </summary>
public static class ThreadSafeAStar
{
    /// <summary>
    /// Per-search state for a grid cell. Allocated per-search, not shared.
    /// </summary>
    private sealed class PointState
    {
        public Vector2I Position;
        public float GScore;      // Cost from start to this point
        public float FScore;      // GScore + heuristic estimate to goal
        public Vector2I? PrevPoint;  // For path reconstruction
        public bool InClosedSet;
    }

    /// <summary>
    /// Calculate path from start to end, optionally treating additional positions as blocked.
    /// Thread-safe - uses no shared mutable state.
    /// </summary>
    /// <param name="grid">The AStarGrid2D to read structure from (solid states, weights).</param>
    /// <param name="from">Start position.</param>
    /// <param name="to">End position.</param>
    /// <param name="allowPartialPath">If true, return path to closest reachable point when goal is unreachable.</param>
    /// <param name="additionalBlocked">Optional set of positions to treat as blocked (e.g., perceived entities).</param>
    /// <returns>List of positions from start to end, or empty if no path found.</returns>
    public static List<Vector2I> GetPath(
        AStarGrid2D grid,
        Vector2I from,
        Vector2I to,
        bool allowPartialPath = true,
        HashSet<Vector2I>? additionalBlocked = null)
    {
        // Validate inputs
        if (!grid.IsInBoundsv(from) || !grid.IsInBoundsv(to))
        {
            return [];
        }

        // Start is blocked - can't path from here
        if (IsBlocked(grid, from, additionalBlocked))
        {
            return [];
        }

        // Already at goal
        if (from == to)
        {
            return [from];
        }

        // If goal is blocked and no partial path allowed, fail immediately
        if (!allowPartialPath && IsBlocked(grid, to, additionalBlocked))
        {
            return [];
        }

        // Per-search state - not shared between threads
        var pointStates = new Dictionary<Vector2I, PointState>();
        var openSet = new PriorityQueue<Vector2I, float>();
        PointState? closestPoint = null;

        // Initialize start point
        var startState = new PointState
        {
            Position = from,
            GScore = 0,
            FScore = Heuristic(from, to),
            PrevPoint = null,
            InClosedSet = false
        };
        pointStates[from] = startState;
        openSet.Enqueue(from, startState.FScore);

        while (openSet.Count > 0)
        {
            var currentPos = openSet.Dequeue();
            var current = pointStates[currentPos];

            // Skip if already processed (can happen with priority queue updates)
            if (current.InClosedSet)
            {
                continue;
            }

            // Track closest point to goal for partial paths
            if (closestPoint == null ||
                current.FScore < closestPoint.FScore ||
                (current.FScore == closestPoint.FScore && current.GScore < closestPoint.GScore))
            {
                closestPoint = current;
            }

            // Reached goal
            if (currentPos == to)
            {
                return ReconstructPath(pointStates, currentPos);
            }

            // Mark as processed
            current.InClosedSet = true;

            // Check all neighbors (8-directional based on grid's diagonal mode)
            foreach (var neighborPos in GetNeighbors(grid, currentPos))
            {
                // Skip blocked cells
                if (IsBlocked(grid, neighborPos, additionalBlocked))
                {
                    continue;
                }

                // Check diagonal movement rules
                if (!IsDiagonalMoveAllowed(grid, currentPos, neighborPos, additionalBlocked))
                {
                    continue;
                }

                // Get or create neighbor state
                if (!pointStates.TryGetValue(neighborPos, out var neighborState))
                {
                    neighborState = new PointState
                    {
                        Position = neighborPos,
                        GScore = float.MaxValue,
                        FScore = float.MaxValue,
                        PrevPoint = null,
                        InClosedSet = false
                    };
                    pointStates[neighborPos] = neighborState;
                }

                // Skip if already in closed set
                if (neighborState.InClosedSet)
                {
                    continue;
                }

                // Calculate tentative g score
                float moveCost = GetMoveCost(currentPos, neighborPos);
                float weightScale = grid.GetPointWeightScale(neighborPos);
                float tentativeG = current.GScore + (moveCost * weightScale);

                // Found a better path to this neighbor
                if (tentativeG < neighborState.GScore)
                {
                    neighborState.PrevPoint = currentPos;
                    neighborState.GScore = tentativeG;
                    neighborState.FScore = tentativeG + Heuristic(neighborPos, to);

                    // Add to open set (may add duplicates, handled by InClosedSet check)
                    openSet.Enqueue(neighborPos, neighborState.FScore);
                }
            }
        }

        // No path to goal - return partial path if allowed
        if (allowPartialPath && closestPoint != null && closestPoint.Position != from)
        {
            return ReconstructPath(pointStates, closestPoint.Position);
        }

        return [];
    }

    /// <summary>
    /// Check if a position is blocked (solid in grid or in additional blocked set).
    /// </summary>
    private static bool IsBlocked(AStarGrid2D grid, Vector2I pos, HashSet<Vector2I>? additionalBlocked)
    {
        if (!grid.IsInBoundsv(pos))
        {
            return true;
        }

        if (grid.IsPointSolid(pos))
        {
            return true;
        }

        if (additionalBlocked != null && additionalBlocked.Contains(pos))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get valid neighbor positions based on grid's diagonal mode.
    /// </summary>
    private static IEnumerable<Vector2I> GetNeighbors(AStarGrid2D grid, Vector2I pos)
    {
        // Cardinal directions
        yield return new Vector2I(pos.X + 1, pos.Y);
        yield return new Vector2I(pos.X - 1, pos.Y);
        yield return new Vector2I(pos.X, pos.Y + 1);
        yield return new Vector2I(pos.X, pos.Y - 1);

        // Diagonal directions (if enabled)
        var diagonalMode = grid.DiagonalMode;
        if (diagonalMode != AStarGrid2D.DiagonalModeEnum.Never)
        {
            yield return new Vector2I(pos.X + 1, pos.Y + 1);
            yield return new Vector2I(pos.X + 1, pos.Y - 1);
            yield return new Vector2I(pos.X - 1, pos.Y + 1);
            yield return new Vector2I(pos.X - 1, pos.Y - 1);
        }
    }

    /// <summary>
    /// Check if diagonal movement is allowed based on grid's diagonal mode and obstacles.
    /// </summary>
    private static bool IsDiagonalMoveAllowed(
        AStarGrid2D grid,
        Vector2I from,
        Vector2I to,
        HashSet<Vector2I>? additionalBlocked)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        // Not a diagonal move
        if (dx == 0 || dy == 0)
        {
            return true;
        }

        var diagonalMode = grid.DiagonalMode;

        switch (diagonalMode)
        {
            case AStarGrid2D.DiagonalModeEnum.Never:
                return false;

            case AStarGrid2D.DiagonalModeEnum.Always:
                return true;

            case AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles:
                // Both adjacent cardinal cells must be clear
                var adj1 = new Vector2I(from.X + dx, from.Y);
                var adj2 = new Vector2I(from.X, from.Y + dy);
                return !IsBlocked(grid, adj1, additionalBlocked) &&
                       !IsBlocked(grid, adj2, additionalBlocked);

            case AStarGrid2D.DiagonalModeEnum.AtLeastOneWalkable:
                // At least one adjacent cardinal cell must be clear
                var adjA = new Vector2I(from.X + dx, from.Y);
                var adjB = new Vector2I(from.X, from.Y + dy);
                return !IsBlocked(grid, adjA, additionalBlocked) ||
                       !IsBlocked(grid, adjB, additionalBlocked);

            case AStarGrid2D.DiagonalModeEnum.Max:
            default:
                return true;
        }
    }

    /// <summary>
    /// Get movement cost between two adjacent cells.
    /// Diagonal movement costs sqrt(2), cardinal costs 1.
    /// </summary>
    private static float GetMoveCost(Vector2I from, Vector2I to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);

        // Diagonal
        if (dx == 1 && dy == 1)
        {
            return 1.41421356f; // sqrt(2)
        }

        return 1.0f;
    }

    /// <summary>
    /// Octile heuristic - accurate for 8-directional movement.
    /// </summary>
    private static float Heuristic(Vector2I from, Vector2I to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);

        // Octile distance: diagonal moves cost sqrt(2), cardinal moves cost 1
        // D * max(dx, dy) + (D2 - D) * min(dx, dy) where D=1, D2=sqrt(2)
        return Math.Max(dx, dy) + (0.41421356f * Math.Min(dx, dy));
    }

    /// <summary>
    /// Reconstruct path from goal back to start.
    /// </summary>
    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, PointState> states, Vector2I goal)
    {
        var path = new List<Vector2I>();
        Vector2I? current = goal;

        while (current.HasValue)
        {
            path.Add(current.Value);
            current = states[current.Value].PrevPoint;
        }

        path.Reverse();
        return path;
    }
}
