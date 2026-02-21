namespace VeilOfAges.Entities;

/// <summary>
/// Defines the functional type of a structural tile in the game world.
/// Used by StructuralEntity to determine walkability, room boundary behavior, and rendering.
/// </summary>
public enum TileType
{
    Wall,
    Crop,
    Floor,
    Door,
    Window,
    Stairs,
    Roof,
    Column,
    Fence,
    Gate,
    Foundation,
    Furniture,
    Decoration,
    Well,
}
