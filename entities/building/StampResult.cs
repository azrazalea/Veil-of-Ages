using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VeilOfAges.Entities;

/// <summary>
/// Return value from TemplateStamper.Stamp(). Contains all entities created
/// from stamping a building template, plus detected rooms.
/// </summary>
public class StampResult
{
    /// <summary>
    /// Gets the template name that was stamped.
    /// </summary>
    public string TemplateName { get; }

    /// <summary>
    /// Gets the building type from the template (e.g., "House", "Farm").
    /// </summary>
    public string BuildingType { get; }

    /// <summary>
    /// Gets the capacity from the template.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the absolute grid origin where the template was stamped.
    /// </summary>
    public Vector2I Origin { get; }

    /// <summary>
    /// Gets the size in tiles from the template.
    /// </summary>
    public Vector2I Size { get; }

    /// <summary>
    /// Gets the grid area this stamp was placed in.
    /// </summary>
    public VeilOfAges.Grid.Area GridArea { get; }

    /// <summary>
    /// Gets all structural entities (walls, floors, doors, etc.) created by the stamp.
    /// </summary>
    public List<StructuralEntity> StructuralEntities { get; } = [];

    /// <summary>
    /// Gets all facilities created by the stamp.
    /// </summary>
    public List<Facility> Facilities { get; } = [];

    /// <summary>
    /// Gets all decorations created by the stamp.
    /// </summary>
    public List<Decoration> Decorations { get; } = [];

    /// <summary>
    /// Gets the rooms detected after stamping (populated by RoomSystem).
    /// </summary>
    public List<Room> Rooms { get; } = [];

    /// <summary>
    /// Gets the absolute positions of door/gate structural entities.
    /// Used for entrance tracking and room boundary detection.
    /// </summary>
    public List<Vector2I> DoorPositions { get; } = [];

    /// <summary>
    /// Gets the entrance positions from the template (absolute coordinates).
    /// </summary>
    public List<Vector2I> EntrancePositions { get; } = [];

    public StampResult(
        string templateName,
        string buildingType,
        int capacity,
        Vector2I origin,
        Vector2I size,
        VeilOfAges.Grid.Area gridArea)
    {
        TemplateName = templateName;
        BuildingType = buildingType;
        Capacity = capacity;
        Origin = origin;
        Size = size;
        GridArea = gridArea;
    }

    /// <summary>
    /// Gets the room containing the given absolute grid position, or null.
    /// </summary>
    public Room? GetRoomAtPosition(Vector2I absolutePos)
    {
        return Rooms.FirstOrDefault(r => r.ContainsAbsolutePosition(absolutePos));
    }
}
