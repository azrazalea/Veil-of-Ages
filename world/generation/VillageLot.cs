using Godot;
using VeilOfAges.Entities;

namespace VeilOfAges.WorldGeneration;
public enum LotState
{
    Available,
    Occupied,
    Reserved
}

/// <summary>
/// Represents a buildable lot section along a road in the village.
/// Lots are 10x10 tiles and can hold one building.
/// </summary>
public class VillageLot
{
    private static int _nextId;

    public int Id { get; }

    /// <summary>Gets top-left corner of the lot in grid coordinates.</summary>
    public Vector2I Position { get; }

    /// <summary>Gets size of the lot (default 10x10).</summary>
    public Vector2I Size { get; }

    public LotState State { get; set; } = LotState.Available;

    /// <summary>Gets the road segment this lot is adjacent to (null for corner lots).</summary>
    public RoadSegment? AdjacentRoad { get; }

    /// <summary>Gets which side of the lot faces the road.</summary>
    public CardinalDirection RoadSide { get; }

    /// <summary>Gets or sets the building occupying this lot, if any.</summary>
    public Building? OccupyingBuilding { get; set; }

    /// <summary>Gets setback from road edge in tiles.</summary>
    public int Setback { get; }

    public VillageLot(Vector2I position, Vector2I size, RoadSegment? adjacentRoad, CardinalDirection roadSide, int setback = 1)
    {
        Id = _nextId++;
        Position = position;
        Size = size;
        AdjacentRoad = adjacentRoad;
        RoadSide = roadSide;
        Setback = setback;
    }

    /// <summary>
    /// Calculate the position where a building should be placed within this lot.
    /// Buildings are centered in the lot on both axes, with a minimum setback from the road edge.
    /// </summary>
    public Vector2I GetBuildingPlacementPosition(Vector2I buildingSize)
    {
        // Calculate ideal centered position within the full lot
        int xOffset = (Size.X - buildingSize.X) / 2;
        int yOffset = (Size.Y - buildingSize.Y) / 2;

        // Adjust based on which side faces the road, ensuring minimum setback is maintained
        return RoadSide switch
        {
            // Road is to the north, ensure minimum setback from top edge
            CardinalDirection.North => new Vector2I(
                Position.X + xOffset,
                Position.Y + System.Math.Max(yOffset, Setback)),

            // Road is to the south, ensure minimum setback from bottom edge
            CardinalDirection.South => new Vector2I(
                Position.X + xOffset,
                Position.Y + System.Math.Min(yOffset, Size.Y - buildingSize.Y - Setback)),

            // Road is to the east, ensure minimum setback from right edge
            CardinalDirection.East => new Vector2I(
                Position.X + System.Math.Min(xOffset, Size.X - buildingSize.X - Setback),
                Position.Y + yOffset),

            // Road is to the west, ensure minimum setback from left edge
            CardinalDirection.West => new Vector2I(
                Position.X + System.Math.Max(xOffset, Setback),
                Position.Y + yOffset),

            _ => new Vector2I(Position.X + xOffset, Position.Y + yOffset)
        };
    }

    /// <summary>
    /// Check if a building of the given size can fit in this lot.
    /// </summary>
    public bool CanFitBuilding(Vector2I buildingSize)
    {
        // Account for setback on the road-facing side
        int availableWidth = Size.X;
        int availableHeight = Size.Y;

        if (RoadSide is CardinalDirection.North or CardinalDirection.South)
        {
            availableHeight -= Setback;
        }
        else
        {
            availableWidth -= Setback;
        }

        return buildingSize.X <= availableWidth && buildingSize.Y <= availableHeight;
    }
}

/// <summary>
/// Cardinal directions for road orientation.
/// </summary>
public enum CardinalDirection
{
    North,
    South,
    East,
    West
}
