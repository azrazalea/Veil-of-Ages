using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VeilOfAges.WorldGeneration;
public enum RoadDirection
{
    NorthSouth,
    EastWest
}

/// <summary>
/// Represents a road segment extending from the village center.
/// Roads have lots on both sides.
/// </summary>
public class RoadSegment
{
    /// <summary>Gets starting point of the road (near village center).</summary>
    public Vector2I Start { get; }

    /// <summary>Gets ending point of the road (away from center).</summary>
    public Vector2I End { get; }

    /// <summary>Gets width of the road in tiles (default 2).</summary>
    public int Width { get; }

    /// <summary>Gets direction the road runs.</summary>
    public RoadDirection Direction { get; }

    /// <summary>Gets lots on the left side of the road (when facing away from center).</summary>
    public List<VillageLot> LeftLots { get; } = new ();

    /// <summary>Gets lots on the right side of the road (when facing away from center).</summary>
    public List<VillageLot> RightLots { get; } = new ();

    /// <summary>Gets all lots adjacent to this road.</summary>
    public IEnumerable<VillageLot> AllLots => LeftLots.Concat(RightLots);

    public RoadSegment(Vector2I start, Vector2I end, int width = 2)
    {
        Start = start;
        End = end;
        Width = width;

        // Determine direction based on start/end positions
        if (start.X == end.X)
        {
            Direction = RoadDirection.NorthSouth;
        }
        else
        {
            Direction = RoadDirection.EastWest;
        }
    }

    /// <summary>
    /// Get all grid positions that this road occupies.
    /// </summary>
    public IEnumerable<Vector2I> GetRoadTiles()
    {
        if (Direction == RoadDirection.NorthSouth)
        {
            int minY = Math.Min(Start.Y, End.Y);
            int maxY = Math.Max(Start.Y, End.Y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int w = 0; w < Width; w++)
                {
                    yield return new Vector2I(Start.X + w, y);
                }
            }
        }
        else // EastWest
        {
            int minX = Math.Min(Start.X, End.X);
            int maxX = Math.Max(Start.X, End.X);

            for (int x = minX; x <= maxX; x++)
            {
                for (int w = 0; w < Width; w++)
                {
                    yield return new Vector2I(x, Start.Y + w);
                }
            }
        }
    }

    /// <summary>
    /// Gets get the length of this road segment in tiles.
    /// </summary>
    public int Length
    {
        get
        {
            if (Direction == RoadDirection.NorthSouth)
            {
                return Math.Abs(End.Y - Start.Y) + 1;
            }
            else
            {
                return Math.Abs(End.X - Start.X) + 1;
            }
        }
    }
}
