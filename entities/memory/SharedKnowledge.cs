using System.Globalization;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Memory;

/// <summary>
/// Lightweight reference to a building that caches position for thread-safe access.
/// Buildings are Godot nodes and their properties should only be accessed on main thread,
/// but the cached position can be safely read from background threads.
/// </summary>
public class BuildingReference
{
    /// <summary>
    /// Gets the referenced building. May become invalid if building is destroyed.
    /// Only access Building properties on main thread.
    /// </summary>
    public Building? Building { get; }

    /// <summary>
    /// Gets cached grid position of the building entrance (or origin if no entrance).
    /// Safe to read from any thread.
    /// </summary>
    public Vector2I Position { get; }

    /// <summary>
    /// Gets cached building type for filtering without accessing the Building object.
    /// </summary>
    public string BuildingType { get; }

    /// <summary>
    /// Gets cached building name for display purposes.
    /// </summary>
    public string BuildingName { get; }

    /// <summary>
    /// Gets a value indicating whether whether this reference is still valid (building not destroyed).
    /// </summary>
    public bool IsValid => Building != null && GodotObject.IsInstanceValid(Building);

    /// <summary>
    /// Gets the area this building is in, for cross-area routing.
    /// </summary>
    public Area? Area { get; }

    public BuildingReference(Building building, Area? area = null)
    {
        Building = building;
        BuildingType = building.BuildingType;
        BuildingName = building.BuildingName;
        Area = area;

        // Cache the entrance position if available, otherwise use building origin
        var entrances = building.GetEntrancePositions();
        Position = entrances.Count > 0 ? entrances[0] : building.GetCurrentGridPosition();
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

    // Building locations by type - scope-appropriate granularity
    private readonly Dictionary<string, List<BuildingReference>> _buildings = new ();

    // Storage tag tracking - what tags each building stores
    // This is common knowledge about what types of items each building is INTENDED to store
    // (e.g., "granary stores food", "well provides water"), NOT current contents
    private readonly Dictionary<string, HashSet<string>> _buildingStorageTags = new ();  // buildingId -> tags
    private readonly Dictionary<string, List<BuildingReference>> _buildingsByStorageTag = new ();  // tag -> buildings

    // Facility tracking - facilities by type
    private readonly Dictionary<string, List<FacilityReference>> _facilities = new ();

    // Transition point tracking
    private readonly List<TransitionPointReference> _transitionPoints = new ();

    // Named landmarks (town square, main gate, etc.)
    private readonly Dictionary<string, Vector2I> _landmarks = new ();

    // General facts (key-value for flexibility)
    private readonly Dictionary<string, object> _facts = new ();

    // NOTE: Direct property access removed for thread safety.
    // Use method-based queries (GetBuildingsOfType, TryGetLandmark, TryGetFact, etc.)
    // which return copies/snapshots safe for background thread access.
    public SharedKnowledge(string id, string name, string scopeType)
    {
        Id = id;
        Name = name;
        ScopeType = scopeType;
    }

    /// <summary>
    /// Register a building in this knowledge scope.
    /// Called during world generation or when buildings are constructed.
    /// Main thread only.
    /// </summary>
    public void RegisterBuilding(Building building, Area? area = null)
    {
        var reference = new BuildingReference(building, area);

        if (!_buildings.TryGetValue(building.BuildingType, out var list))
        {
            list = new List<BuildingReference>();
            _buildings[building.BuildingType] = list;
        }

        list.Add(reference);
    }

    /// <summary>
    /// Unregister a building (destroyed, etc.).
    /// Main thread only.
    /// </summary>
    public void UnregisterBuilding(Building building)
    {
        if (_buildings.TryGetValue(building.BuildingType, out var list))
        {
            list.RemoveAll(r => r.Building == building);
        }

        // Also clean up storage tag registrations
        var buildingId = building.GetInstanceId().ToString(CultureInfo.InvariantCulture);
        if (_buildingStorageTags.TryGetValue(buildingId, out var tags))
        {
            foreach (var tag in tags)
            {
                if (_buildingsByStorageTag.TryGetValue(tag, out var taggedBuildings))
                {
                    taggedBuildings.RemoveAll(r => r.Building == building);
                }
            }

            _buildingStorageTags.Remove(buildingId);
        }
    }

    /// <summary>
    /// Register a building's storage tags indicating what types of items it stores.
    /// This represents common knowledge about what a building is INTENDED to store,
    /// not the actual current contents (use PersonalMemory for that).
    /// Example: Granary stores "food", "grain"; Well provides "water".
    /// Main thread only.
    /// </summary>
    /// <param name="building">The building to register.</param>
    /// <param name="tags">Tags indicating what this building stores (e.g., "food", "grain", "water").</param>
    public void RegisterBuildingStorageTags(Building building, IEnumerable<string> tags, Area? area = null)
    {
        // First ensure the building is registered normally
        RegisterBuilding(building, area);

        var buildingId = building.GetInstanceId().ToString(CultureInfo.InvariantCulture);
        var reference = new BuildingReference(building, area);

        // Store the tags for this building
        if (!_buildingStorageTags.TryGetValue(buildingId, out var existingTags))
        {
            existingTags = new HashSet<string>();
            _buildingStorageTags[buildingId] = existingTags;
        }

        foreach (var tag in tags)
        {
            existingTags.Add(tag);

            // Add to the reverse lookup
            if (!_buildingsByStorageTag.TryGetValue(tag, out var taggedBuildings))
            {
                taggedBuildings = new List<BuildingReference>();
                _buildingsByStorageTag[tag] = taggedBuildings;
            }

            // Only add if not already present
            if (!taggedBuildings.Any(r => r.Building == building))
            {
                taggedBuildings.Add(reference);
            }
        }
    }

    /// <summary>
    /// Get all buildings known to store items with a specific tag.
    /// This is common knowledge about what buildings are INTENDED to store,
    /// not whether they actually have items right now.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    /// <param name="tag">The tag to search for (e.g., "food", "water").</param>
    /// <returns>List of building references that store this type of item.</returns>
    public IReadOnlyList<BuildingReference> GetBuildingsByTag(string tag)
    {
        if (_buildingsByStorageTag.TryGetValue(tag, out var buildings))
        {
            return buildings.Where(b => b.IsValid).ToList(); // Return a copy for thread safety
        }

        return Array.Empty<BuildingReference>();
    }

    /// <summary>
    /// Get all tags registered for a specific building's storage.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<string> GetBuildingTags(Building building)
    {
        var buildingId = building.GetInstanceId().ToString(CultureInfo.InvariantCulture);
        if (_buildingStorageTags.TryGetValue(buildingId, out var tags))
        {
            return tags.ToList(); // Return a copy for thread safety
        }

        return Array.Empty<string>();
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
    /// Get all buildings of a specific type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<BuildingReference> GetBuildingsOfType(string buildingType)
    {
        if (_buildings.TryGetValue(buildingType, out var list))
        {
            return list.ToList(); // Return a copy for thread safety
        }

        return Array.Empty<BuildingReference>();
    }

    /// <summary>
    /// Try to find any building of the specified type.
    /// </summary>
    public bool TryGetBuildingOfType(string buildingType, out BuildingReference? building)
    {
        var buildings = GetBuildingsOfType(buildingType);
        building = buildings.FirstOrDefault(b => b.IsValid);
        return building != null;
    }

    /// <summary>
    /// Find the nearest building of a type to a position.
    /// </summary>
    public BuildingReference? GetNearestBuildingOfType(string buildingType, Vector2I fromPosition)
    {
        var buildings = GetBuildingsOfType(buildingType);
        BuildingReference? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var building in buildings)
        {
            if (!building.IsValid)
            {
                continue;
            }

            float dist = fromPosition.DistanceSquaredTo(building.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = building;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all known building types in this scope.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<string> GetKnownBuildingTypes() => _buildings.Keys.ToList();

    /// <summary>
    /// Get all buildings in this scope, regardless of type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<BuildingReference> GetAllBuildings()
    {
        var result = new List<BuildingReference>();
        foreach (var list in _buildings.Values)
        {
            result.AddRange(list);
        }

        return result;
    }

    /// <summary>
    /// Register a facility in this knowledge scope.
    /// </summary>
    public void RegisterFacility(string facilityType, Building building, Area? area, Vector2I position)
    {
        var buildingRef = new BuildingReference(building, area);
        var facilityRef = new FacilityReference(facilityType, buildingRef, area, position);

        if (!_facilities.TryGetValue(facilityType, out var list))
        {
            list = new List<FacilityReference>();
            _facilities[facilityType] = list;
        }

        list.Add(facilityRef);
    }

    /// <summary>
    /// Get all valid facilities of a specific type.
    /// Returns a snapshot copy safe for background thread access.
    /// </summary>
    public IReadOnlyList<FacilityReference> GetFacilitiesOfType(string facilityType)
    {
        if (_facilities.TryGetValue(facilityType, out var list))
        {
            return list.Where(f => f.Building.IsValid).ToList();
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
    /// Remove all facility registrations for a specific building.
    /// </summary>
    public void UnregisterFacilitiesForBuilding(Building building)
    {
        foreach (var list in _facilities.Values)
        {
            list.RemoveAll(f => f.Building.Building == building);
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
    /// Clean up invalid building references.
    /// Call periodically on main thread.
    /// </summary>
    public void CleanupInvalidReferences()
    {
        foreach (var list in _buildings.Values)
        {
            list.RemoveAll(r => !r.IsValid);
        }

        // Also clean up storage tag references
        var invalidBuildingIds = new List<string>();
        foreach (var (buildingId, _) in _buildingStorageTags)
        {
            // Check if any building reference with this ID is still valid
            // We need to check by iterating through the tagged buildings
            bool isValid = false;
            foreach (var taggedList in _buildingsByStorageTag.Values)
            {
                if (taggedList.Any(r => r.IsValid && r.Building?.GetInstanceId().ToString(CultureInfo.InvariantCulture) == buildingId))
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid)
            {
                invalidBuildingIds.Add(buildingId);
            }
        }

        foreach (var buildingId in invalidBuildingIds)
        {
            _buildingStorageTags.Remove(buildingId);
        }

        // Clean up invalid references in the tagged buildings lists
        foreach (var list in _buildingsByStorageTag.Values)
        {
            list.RemoveAll(r => !r.IsValid);
        }

        // Clean up invalid facility references
        foreach (var list in _facilities.Values)
        {
            list.RemoveAll(f => !f.Building.IsValid);
        }
    }
}

/// <summary>
/// Lightweight reference to a facility within a building.
/// </summary>
/// <param name="FacilityType">The type of facility (e.g., "corpse_pit", "altar").</param>
/// <param name="Building">Reference to the building containing this facility.</param>
/// <param name="Area">The area this facility is in.</param>
/// <param name="Position">The grid position of the facility.</param>
public record FacilityReference(string FacilityType, BuildingReference Building, Area? Area, Vector2I Position);

/// <summary>
/// Reference to a transition point for cross-area routing.
/// </summary>
/// <param name="SourceArea">The area this transition point is in.</param>
/// <param name="SourcePosition">The grid position within the source area.</param>
/// <param name="TransitionPoint">The transition point object.</param>
public record TransitionPointReference(Area SourceArea, Vector2I SourcePosition, TransitionPoint TransitionPoint);
