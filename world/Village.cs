using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Grid;

namespace VeilOfAges;

/// <summary>
/// Represents a village settlement with shared knowledge about buildings and landmarks.
/// Entities that are residents share access to this village's knowledge by reference.
/// </summary>
public partial class Village : Node
{
    public string VillageName { get; private set; } = string.Empty;
    public Vector2I Center { get; private set; }

    /// <summary>
    /// Gets shared knowledge for this village, passed by reference to all residents.
    /// </summary>
    public SharedKnowledge Knowledge { get; private set; } = null!;

    private readonly List<Being> _residents = new ();
    private readonly List<Room> _rooms = new ();

    public IReadOnlyList<Being> Residents => _residents;
    public IReadOnlyList<Room> Rooms => _rooms;

    /// <summary>
    /// Initialize the village with a name and center position.
    /// </summary>
    public void Initialize(string name, Vector2I center)
    {
        VillageName = name;
        Name = name; // Godot node name
        Center = center;

        Knowledge = new SharedKnowledge(
            id: $"village_{name.ToLowerInvariant().Replace(" ", "_")}",
            name: name,
            scopeType: "village");

        // Set the village center as a landmark
        Knowledge.SetLandmark("center", center);
        Knowledge.SetLandmark("square", center);
        Knowledge.SetFact("name", name);
    }

    /// <summary>
    /// Add a room to this village. Registers it in shared knowledge.
    /// </summary>
    public void AddRoom(Room room, Area? area = null)
    {
        if (_rooms.Contains(room))
        {
            return;
        }

        _rooms.Add(room);
        Knowledge.RegisterRoom(room, area);
    }

    /// <summary>
    /// Remove a room from this village.
    /// </summary>
    public void RemoveRoom(Room room)
    {
        _rooms.Remove(room);
        Knowledge.UnregisterRoom(room);
    }

    /// <summary>
    /// Add a resident to this village.
    /// Sets their village membership AND gives them access to village shared knowledge.
    /// These are managed separately because beings can have shared knowledge from
    /// multiple sources (village, faction, region, etc.).
    /// </summary>
    public void AddResident(Being being)
    {
        if (_residents.Contains(being))
        {
            return;
        }

        _residents.Add(being);
        being.SetVillage(this);
        being.AddSharedKnowledge(Knowledge);
    }

    /// <summary>
    /// Remove a resident from this village.
    /// Clears their village membership AND removes access to village shared knowledge.
    /// </summary>
    public void RemoveResident(Being being)
    {
        _residents.Remove(being);
        being.SetVillage(null);
        being.RemoveSharedKnowledge(Knowledge);
    }

    /// <summary>
    /// Clean up invalid room and resident references periodically.
    /// </summary>
    public void CleanupInvalidReferences()
    {
        _rooms.RemoveAll(r => r.IsDestroyed);
        _residents.RemoveAll(r => !GodotObject.IsInstanceValid(r));
        Knowledge.CleanupInvalidReferences();
    }
}
