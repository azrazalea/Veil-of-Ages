using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges.Entities;

/// <summary>
/// Lightweight organizational unit grouping tiles, facilities, decorations, and residents
/// within a building. Created automatically via flood fill of walkable interior positions.
/// Template RoomData provides optional hints (name, purpose, IsSecret) matched by overlap.
///
/// Room is a plain C# class, NOT a Godot node — it's purely organizational.
/// </summary>
public class Room
{
    /// <summary>
    /// Gets unique identifier for this room within its building.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets human-readable name (from template hint or auto-generated).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the purpose of this room (from template hint or auto-detected from contents).
    /// Examples: "Living", "Kitchen", "Workshop", "Storage", "Burial".
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether this room is secret.
    /// Secret rooms create their own SharedKnowledge scope — their facilities are
    /// NOT registered in village SharedKnowledge, only in the room's own knowledge.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Gets the building that contains this room.
    /// </summary>
    public Building Owner { get; }

    /// <summary>
    /// Gets or sets the grid area this room exists in.
    /// </summary>
    public VeilOfAges.Grid.Area? GridArea { get; set; }

    /// <summary>
    /// Gets the interior tile positions of this room (relative to building origin).
    /// Populated by flood fill during building initialization.
    /// </summary>
    private readonly HashSet<Vector2I> _tiles;
    public IReadOnlySet<Vector2I> Tiles => _tiles;

    // Contained entities
    private readonly List<Facility> _facilities = [];
    private readonly List<Decoration> _decorations = [];
    private readonly List<Being> _residents = [];

    /// <summary>
    /// Gets the facilities contained within this room.
    /// </summary>
    public IReadOnlyList<Facility> Facilities => _facilities;

    /// <summary>
    /// Gets the decorations contained within this room.
    /// </summary>
    public IReadOnlyList<Decoration> Decorations => _decorations;

    /// <summary>
    /// Gets the residents assigned to this room.
    /// </summary>
    public IReadOnlyList<Being> Residents => _residents;

    /// <summary>
    /// Gets the SharedKnowledge scope for this room.
    /// Only non-null for secret rooms — contains facility registrations hidden from village knowledge.
    /// </summary>
    public SharedKnowledge? RoomKnowledge { get; private set; }

    /// <summary>
    /// Gets or sets the maximum number of residents this room can hold.
    /// Derived from building capacity or template hint. 0 = unlimited.
    /// </summary>
    public int Capacity { get; set; }

    public Room(string id, Building owner, HashSet<Vector2I> tiles)
    {
        Id = id;
        Name = id;
        Owner = owner;
        _tiles = tiles;
    }

    // --- Resident management ---

    /// <summary>
    /// Add a resident to this room. Respects capacity if set.
    /// </summary>
    public void AddResident(Being being)
    {
        if (being != null && !_residents.Contains(being))
        {
            if (Capacity <= 0 || _residents.Count < Capacity)
            {
                _residents.Add(being);
            }
        }
    }

    /// <summary>
    /// Remove a resident from this room.
    /// </summary>
    public void RemoveResident(Being being)
    {
        _residents.Remove(being);
    }

    /// <summary>
    /// Check if a being is a resident of this room.
    /// </summary>
    public bool HasResident(Being being) => _residents.Contains(being);

    // --- Content management ---

    /// <summary>
    /// Register a facility as belonging to this room.
    /// </summary>
    public void AddFacility(Facility facility)
    {
        if (!_facilities.Contains(facility))
        {
            _facilities.Add(facility);
        }
    }

    /// <summary>
    /// Register a decoration as belonging to this room.
    /// </summary>
    public void AddDecoration(Decoration decoration)
    {
        if (!_decorations.Contains(decoration))
        {
            _decorations.Add(decoration);
        }
    }

    // --- Position queries ---

    /// <summary>
    /// Check if a position (relative to building origin) is within this room.
    /// </summary>
    public bool ContainsRelativePosition(Vector2I relativePos) => _tiles.Contains(relativePos);

    /// <summary>
    /// Check if an absolute grid position is within this room.
    /// </summary>
    public bool ContainsAbsolutePosition(Vector2I absolutePos)
    {
        var buildingPos = Owner.GetCurrentGridPosition();
        return _tiles.Contains(absolutePos - buildingPos);
    }

    // --- Secret room support ---

    /// <summary>
    /// Initialize this room as a secret room with its own SharedKnowledge scope.
    /// Facilities in secret rooms are registered in this scope instead of village knowledge.
    /// Authorized entities receive this knowledge via Being.AddSharedKnowledge().
    /// </summary>
    public void InitializeSecrecy(string knowledgeId, string knowledgeName)
    {
        IsSecret = true;
        RoomKnowledge = new SharedKnowledge(knowledgeId, knowledgeName, "personal");
    }
}
