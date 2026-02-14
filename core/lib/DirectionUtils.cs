using System;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Static helper for directional offsets and adjacency checks.
/// Cardinal directions are listed first in <see cref="All"/> so they are
/// preferred when iterating candidates.
/// </summary>
public static class DirectionUtils
{
    /// <summary>Cardinal directions: Up, Right, Down, Left.</summary>
    public static readonly Vector2I[] Cardinal =
    [
        Vector2I.Up,
        Vector2I.Right,
        Vector2I.Down,
        Vector2I.Left
    ];

    /// <summary>Diagonal directions: UpRight, DownRight, DownLeft, UpLeft.</summary>
    public static readonly Vector2I[] Diagonal =
    [
        new Vector2I(1, -1),   // Up-Right
        new Vector2I(1, 1),    // Down-Right
        new Vector2I(-1, 1),   // Down-Left
        new Vector2I(-1, -1) // Up-Left
    ];

    /// <summary>All 8 directions — cardinal first, then diagonal.</summary>
    public static readonly Vector2I[] All =
    [

        // Cardinal
        Vector2I.Up,
        Vector2I.Right,
        Vector2I.Down,
        Vector2I.Left,

        // Diagonal
        new Vector2I(1, -1),
        new Vector2I(1, 1),
        new Vector2I(-1, 1),
        new Vector2I(-1, -1)
    ];

    /// <summary>
    /// Chebyshev distance == 1 (includes diagonals).
    /// </summary>
    public static bool IsAdjacent(Vector2I a, Vector2I b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) == 1;
    }

    /// <summary>
    /// Manhattan distance == 1 (cardinal only).
    /// </summary>
    public static bool IsCardinallyAdjacent(Vector2I a, Vector2I b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return (dx + dy) == 1;
    }
}
