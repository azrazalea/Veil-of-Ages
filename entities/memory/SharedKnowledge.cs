using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Memory;

/// <summary>
/// Lightweight reference to a room that caches position for thread-safe access.
/// Rooms are plain C# objects and their cached properties can be safely read from background threads.
/// </summary>
public class RoomReference
{
    /// <summary>
    /// Gets the referenced room. May become invalid if room is destroyed.
    /// </summary>
    public Room? Room { get; }

    /// <summary>
    /// Gets cached grid position of the room entrance (first door, or first tile if no doors).
    /// Safe to read from any thread.
    /// </summary>
    public Vector2I Position { get; }

    /// <summary>
    /// Gets cached room type for filtering without accessing the Room object.
    /// </summary>
    public string RoomType { get; }

    /// <summary>
    /// Gets cached room name for display purposes.
    /// </summary>
    public string RoomName { get; }

    /// <summary>
    /// Gets the area this room is in, for cross-area routing.
    /// </summary>
    public Area? Area { get; }

    /// <summary>
    /// Gets a value indicating whether this reference is still valid (room not destroyed).
    /// </summary>
    public bool IsValid => Room != null && !Room.IsDestroyed;

    public RoomReference(Room room, Area? area = null)
    {
        Room = room;
        if (room.Type == null)
        {
            Log.Error($"RoomReference: Room '{room.Name}' has no Type");
        }

        RoomType = room.Type ?? string.Empty;
        RoomName = room.Name;
        Area = area ?? room.GridArea;

        // Use first door position as entrance, or first tile if no doors
        if (room.Doors.Count > 0)
        {
            Position = room.Doors[0].GridPosition;
        }
        else if (room.Tiles.Count > 0)
        {
            Position = room.Tiles.First();
        }
        else
        {
            Position = Vector2I.Zero;
        }
    }
}

/// <summary>
/// Base class for shared knowledge that can be referenced by multiple entities.
/// Knowledge is read-only from entity perspective and composable (entities can have multiple sources).
/// Different scopes (village, region, kingdom) hold different granularity of information.
///
/// IMPORTANT: Does NOT contain storage contents - that's unrealistic omniscience.
/// Storage observations belong in PersonalMemory only.
/// </summary>
public class SharedKnowledge
{
    /// <summary>
    /// Gets unique identifier for this knowledge source.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets human-readable name (e.g., "Millbrook Village", "Northern Kingdom").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets scope type identifier (e.g., "village", "region", "kingdom", "faction").
    /// String for flexibility - not an enum.
    /// </summary>
    public string ScopeType { get; }

    // Room locations by type - scope-appropriate granularity
    private readonly Dictionary<string, List<RoomReference>> _rooms = new ();

    // Facility tracking - facilities by type
    private readonly Dictionary<string, List<FacilityReference>> _facilities = new ();

    // Facility storage tag tracking - what tags each facility stores
    // Indexed automatically when RegisterFacility() is called, from the facility's StorageTrait.Tags
    private readonly Dictionary<string, List<FacilityReference>> _facilitiesByStorageTag = new ();  // tag -> facilities

    // Transition point tracking
    private readonly List<TransitionPointReference> _transitionPoints = new ();

    // Named landmarks (town square, main gate, etc.)
    private readonly Dictionary<string, Vector2I> _landmarks = new ();

    // General facts (key-value for flexibility)
    private readonly Dictionary<string, object> _facts = new ();

    // NOTE: Direct property access removed for thread safety.
    // Use method-based queries (GetRoomsOfType, TryGetLandmark, TryGetFact, etc.)
    // which return copies/snapshots safe for background thread access.
    public SharedKnowledge(string id, string name, string scopeType)
    {
        Id = id;
        Name = name;
        ScopeType = scopeType;
    }

    /// <summary>
    /// Register a room in this knowledge scope.
    /// Called during world generation or when rooms are detected.
    /// Main thread only.
    /// </summary>
    public void RegisterRoom(Room room, Area? area = null)
    {
        if (room.Type == null)
        {
            Log.Error($"SharedKnowledge.RegisterRoom: Room '{room.Name}' has no Type");
        }

        var roomType = room.Type ?? string.Empty;
        var reference = new RoomReference(room, area);

        if (!_rooms.TryGetValue(roomType, out var list))
        {
            list = new List<RoomReference>();
            _rooms[roomType] = list;
        }

        list.Add(reference);
    }

    /// <summary>
    /// Unregister a room (destroyed, etc.).
    /// Main thread only.
    /// </summary>
    public void UnregisterRoom(Room room)
    {
        var roomType = room.Type ?? string.Empty;
        if (_rooms.TryGetValue(roomType, out var list))
        {
            list.RemoveAll(r => r.Room == room);
        }
    }

    /// <summary>
    /// Set a named landmark location.
    /// </summary>
    public void SetLandmark(string name, Vector2I position)
    {
        _landmarks[name] = position;
    }

    /// <summary>
    /// Store a general fact.
    /// </summary>
    public void SetFact(string key, object value)
    {
        _facts[key] = value;
    }

    /// <summary>
    /// Try to get a landmark position by name.
    /// Thread-safe: Vector2I is a value type, so the result is a copy.
    /// </summary>
    public bool TryGetLandmark(string name, out Vector2I position)
    {
        return _landmarks.TryGetValue(name, out position);
    }

    /// <summary>
    /// Get all known landmark names.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<string> GetAllLandmarkNames() => _landmarks.Keys.ToList();

    /// <summary>
    /// Try to get a fact value.
    /// Thread-safe for value types and immutable reference types.
    /// Caller should ensure returned reference types are not mutated.
    /// </summary>
    public bool TryGetFact<T>(string key, out T? value)
    {
        if (_facts.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Get all known fact keys.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<string> GetAllFactKeys() => _facts.Keys.ToList();

    /// <summary>
    /// Get all rooms of a specific type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<RoomReference> GetRoomsOfType(string roomType)
    {
        if (_rooms.TryGetValue(roomType, out var list))
        {
            return list.ToList(); // Return a copy for thread safety
        }

        return Array.Empty<RoomReference>();
    }

    /// <summary>
    /// Try to find any room of the specified type.
    /// </summary>
    public bool TryGetRoomOfType(string roomType, out RoomReference? room)
    {
        var rooms = GetRoomsOfType(roomType);
        room = rooms.FirstOrDefault(r => r.IsValid);
        return room != null;
    }

    /// <summary>
    /// Find the nearest room of a type to a position.
    /// </summary>
    public RoomReference? GetNearestRoomOfType(string roomType, Vector2I fromPosition)
    {
        var rooms = GetRoomsOfType(roomType);
        RoomReference? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var room in rooms)
        {
            if (!room.IsValid)
            {
                continue;
            }

            float dist = fromPosition.DistanceSquaredTo(room.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = room;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all known room types in this scope.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<string> GetKnownRoomTypes() => _rooms.Keys.ToList();

    /// <summary>
    /// Get all rooms in this scope, regardless of type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<RoomReference> GetAllRooms()
    {
        var result = new List<RoomReference>();
        foreach (var list in _rooms.Values)
        {
            result.AddRange(list);
        }

        return result;
    }

    /// <summary>
    /// Register a facility in this knowledge scope.
    /// If the facility has a StorageTrait with tags, also registers in the
    /// facility-by-storage-tag index for quick tag-based lookup.
    /// Main thread only.
    /// </summary>
    public void RegisterFacility(string facilityType, Facility facility, Area? area, Vector2I position)
    {
        // Capture storage tags at registration time so they're available without god-knowledge
        var storageTrait = facility.SelfAsEntity().GetTrait<StorageTrait>();
        IReadOnlyList<string> storageTags = storageTrait != null && storageTrait.Tags.Count > 0
            ? storageTrait.Tags.ToList()
            : Array.Empty<string>();

        var facilityRef = new FacilityReference(facilityType, facility, area, position, storageTags);

        if (!_facilities.TryGetValue(facilityType, out var list))
        {
            list = new List<FacilityReference>();
            _facilities[facilityType] = list;
        }

        list.Add(facilityRef);

        // Also index by storage tags if this facility has a StorageTrait with tags
        if (storageTags.Count > 0)
        {
            foreach (var tag in storageTags)
            {
                if (!_facilitiesByStorageTag.TryGetValue(tag, out var taggedFacilities))
                {
                    taggedFacilities = new List<FacilityReference>();
                    _facilitiesByStorageTag[tag] = taggedFacilities;
                }

                taggedFacilities.Add(facilityRef);
            }
        }
    }

    /// <summary>
    /// Get all valid facilities of a specific type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<FacilityReference> GetFacilitiesOfType(string facilityType)
    {
        if (_facilities.TryGetValue(facilityType, out var list))
        {
            return list.Where(f => f.Facility != null && GodotObject.IsInstanceValid(f.Facility)).ToList();
        }

        return Array.Empty<FacilityReference>();
    }

    /// <summary>
    /// Get all valid facilities known to store items with a specific tag.
    /// This is common knowledge about what facilities are INTENDED to store,
    /// not whether they actually have items right now.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    /// <param name="tag">The tag to search for (e.g., "food", "corpse").</param>
    /// <returns>List of facility references whose storage trait has this tag.</returns>
    public IReadOnlyList<FacilityReference> GetFacilitiesByTag(string tag)
    {
        if (_facilitiesByStorageTag.TryGetValue(tag, out var facilities))
        {
            return facilities.Where(f => f.Facility != null && GodotObject.IsInstanceValid(f.Facility)).ToList();
        }

        return Array.Empty<FacilityReference>();
    }

    /// <summary>
    /// Find the nearest facility of a type to a position, preferring same-area facilities.
    /// </summary>
    public FacilityReference? GetNearestFacilityOfType(string facilityType, Area? currentArea, Vector2I fromPosition)
    {
        var facilities = GetFacilitiesOfType(facilityType);
        FacilityReference? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var facility in facilities)
        {
            // Prefer same-area facilities (simple distance), cross-area gets a penalty
            float dist = fromPosition.DistanceSquaredTo(facility.Position);
            if (facility.Area != currentArea)
            {
                dist += WorldNavigator.CROSSAREAPENALTY;
            }

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = facility;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Remove all facility registrations for a specific room.
    /// Matches by checking if facility positions are within the room's tiles.
    /// </summary>
    public void UnregisterFacilitiesForRoom(Room room)
    {
        foreach (var list in _facilities.Values)
        {
            list.RemoveAll(f =>
            {
                if (f.Facility == null)
                {
                    return true;
                }

                return room.ContainsAbsolutePosition(f.Position);
            });
        }

        // Also remove from the tag-based index
        foreach (var list in _facilitiesByStorageTag.Values)
        {
            list.RemoveAll(f =>
            {
                if (f.Facility == null)
                {
                    return true;
                }

                return room.ContainsAbsolutePosition(f.Position);
            });
        }
    }

    /// <summary>
    /// Register a transition point for cross-area routing.
    /// </summary>
    public void RegisterTransitionPoint(TransitionPoint point)
    {
        // Don't duplicate
        if (!_transitionPoints.Any(t => t.TransitionPoint == point))
        {
            _transitionPoints.Add(new TransitionPointReference(point.SourceArea, point.SourcePosition, point));
        }
    }

    /// <summary>
    /// Get all registered transition points.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<TransitionPointReference> GetAllTransitionPoints()
    {
        return _transitionPoints.ToList();
    }

    /// <summary>
    /// Get all transition points in a specific area.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<TransitionPointReference> GetTransitionPointsInArea(Area area)
    {
        return _transitionPoints.Where(t => t.SourceArea == area).ToList();
    }

    /// <summary>
    /// Get all valid facility references across all facility types.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<FacilityReference> GetAllFacilityReferences()
    {
        var result = new List<FacilityReference>();
        foreach (var list in _facilities.Values)
        {
            foreach (var f in list)
            {
                if (f.Facility != null && GodotObject.IsInstanceValid(f.Facility))
                {
                    result.Add(f);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clean up invalid room references.
    /// Call periodically on main thread.
    /// </summary>
    public void CleanupInvalidReferences()
    {
        foreach (var list in _rooms.Values)
        {
            list.RemoveAll(r => !r.IsValid);
        }

        // Clean up invalid facility references
        foreach (var list in _facilities.Values)
        {
            list.RemoveAll(f => f.Facility == null || !GodotObject.IsInstanceValid(f.Facility));
        }

        // Clean up invalid facility tag references
        foreach (var list in _facilitiesByStorageTag.Values)
        {
            list.RemoveAll(f => f.Facility == null || !GodotObject.IsInstanceValid(f.Facility));
        }
    }
}

/// <summary>
/// Lightweight reference to a facility within a building.
/// </summary>
/// <param name="FacilityType">The type of facility (e.g., "corpse_pit", "altar").</param>
/// <param name="Facility">The facility object. Use Facility.Owner to get the containing building.</param>
/// <param name="Area">The area this facility is in.</param>
/// <param name="Position">The grid position of the facility.</param>
public record FacilityReference(string FacilityType, Facility? Facility, Area? Area, Vector2I Position, IReadOnlyList<string> StorageTags);

/// <summary>
/// Reference to a transition point for cross-area routing.
/// </summary>
/// <param name="SourceArea">The area this transition point is in.</param>
/// <param name="SourcePosition">The grid position within the source area.</param>
/// <param name="TransitionPoint">The transition point object.</param>
public record TransitionPointReference(Area SourceArea, Vector2I SourcePosition, TransitionPoint TransitionPoint);
