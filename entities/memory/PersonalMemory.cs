using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Memory;

/// <summary>
/// Personal memory storage for an entity. Contains observations and sightings
/// that the entity has personally witnessed - not shared omniscient knowledge.
///
/// Storage contents are ONLY known through personal observation stored here,
/// not through SharedKnowledge (which handles building locations, etc.).
/// </summary>
public class PersonalMemory
{
    private readonly Being _owner;

    // Storage observations - what I personally saw in storage containers, keyed by Facility
    private readonly Dictionary<Facility, StorageObservation> _storageObservations = new ();

    // Entity sightings - where I last saw specific entities
    private readonly Dictionary<Being, EntitySighting> _entitySightings = new ();

    // Location memories - places I've been
    private readonly Dictionary<Vector2I, LocationMemory> _locationMemories = new ();

    // Facility observations - facilities I've personally discovered
    private readonly List<FacilityObservation> _facilityObservations = new ();

    /// <summary>
    /// Gets or sets default duration for general memories in ticks (~12 game hours).
    /// At 8 ticks/second and 36.8 game seconds per real second,
    /// 7,000 ticks is approximately 12 game hours.
    /// </summary>
    public uint DefaultMemoryDuration { get; set; } = 7_000;

    /// <summary>
    /// Gets or sets duration for storage observations in ticks (~2 game days).
    /// Longer because storage contents change less frequently.
    /// 28,000 ticks is approximately 2 game days.
    /// </summary>
    public uint StorageMemoryDuration { get; set; } = 28_000;

    /// <summary>
    /// Gets or sets duration for entity sightings in ticks (~12 game hours).
    /// </summary>
    public uint EntitySightingDuration { get; set; } = 7_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalMemory"/> class.
    /// Creates a new personal memory system for the specified entity.
    /// </summary>
    /// <param name="owner">The entity that owns this memory.</param>
    public PersonalMemory(Being owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Record an observation of storage contents.
    /// Call this when entity examines or accesses a storage container.
    /// </summary>
    /// <param name="facility">The facility containing the storage.</param>
    /// <param name="storage">The storage container to observe.</param>
    public void ObserveStorage(Facility facility, IStorageContainer storage)
    {
        uint currentTick = GameController.CurrentTick;

        var items = storage.GetAllItems()
            .GroupBy(i => i.Definition.Id ?? string.Empty)
            .Select(g => new ItemSnapshot(
                g.Key,
                g.First().Definition.LocalizedName ?? string.Empty,
                g.Sum(i => i.Quantity),
                new List<string>(g.First().Definition.Tags)))
            .ToList();

        var observation = new StorageObservation(
            facility,
            currentTick,
            currentTick + StorageMemoryDuration,
            items);

        _storageObservations[facility] = observation;
    }

    /// <summary>
    /// Recall what was last seen in a specific storage facility.
    /// Returns null if no memory or memory expired.
    /// </summary>
    /// <param name="facility">The facility to recall storage contents for.</param>
    /// <returns>The storage observation if remembered and not expired, null otherwise.</returns>
    public StorageObservation? RecallStorageContents(Facility facility)
    {
        if (_storageObservations.TryGetValue(facility, out var observation))
        {
            if (!observation.IsExpired(GameController.CurrentTick))
            {
                return observation;
            }
        }

        return null;
    }

    /// <summary>
    /// Find all remembered storage locations that had a specific item (by tag).
    /// Returns list of (facility, remembered quantity) tuples, sorted by quantity descending.
    /// </summary>
    /// <param name="itemTag">The tag to search for (e.g., "food", "grain").</param>
    /// <returns>List of facilities and quantities where the item was seen.</returns>
    public List<(Facility facility, int quantity)> RecallStorageWithItem(string itemTag)
    {
        uint currentTick = GameController.CurrentTick;
        var results = new List<(Facility facility, int quantity)>();

        foreach (var (facility, observation) in _storageObservations)
        {
            if (observation.IsExpired(currentTick))
            {
                continue;
            }

            if (!GodotObject.IsInstanceValid(facility))
            {
                continue;
            }

            if (observation.HasItemWithTag(itemTag))
            {
                // Sum quantities of all items with this tag
                int qty = observation.Items
                    .Where(i => i.Tags.Contains(itemTag, StringComparer.OrdinalIgnoreCase))
                    .Sum(i => i.Quantity);

                results.Add((facility, qty));
            }
        }

        return results.OrderByDescending(r => r.quantity).ToList();
    }

    /// <summary>
    /// Find all remembered storage locations that had a specific item (by definition ID).
    /// Returns list of (facility, remembered quantity) tuples, sorted by quantity descending.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to search for.</param>
    /// <returns>List of facilities and quantities where the item was seen.</returns>
    public List<(Facility facility, int quantity)> RecallStorageWithItemById(string itemDefId)
    {
        uint currentTick = GameController.CurrentTick;
        var results = new List<(Facility facility, int quantity)>();

        foreach (var (facility, observation) in _storageObservations)
        {
            if (observation.IsExpired(currentTick))
            {
                continue;
            }

            if (!GodotObject.IsInstanceValid(facility))
            {
                continue;
            }

            var matchingItems = observation.Items
                .Where(i => string.Equals(i.ItemDefId, itemDefId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingItems.Count > 0)
            {
                int qty = matchingItems.Sum(i => i.Quantity);
                results.Add((facility, qty));
            }
        }

        return results.OrderByDescending(r => r.quantity).ToList();
    }

    /// <summary>
    /// Check if entity remembers seeing a specific item anywhere.
    /// </summary>
    /// <param name="itemTag">The tag to search for.</param>
    /// <returns>True if the entity remembers seeing an item with this tag.</returns>
    public bool RemembersItemAvailable(string itemTag)
    {
        uint currentTick = GameController.CurrentTick;

        foreach (var observation in _storageObservations.Values)
        {
            if (observation.IsExpired(currentTick))
            {
                continue;
            }

            if (!GodotObject.IsInstanceValid(observation.Facility))
            {
                continue;
            }

            if (observation.HasItemWithTag(itemTag))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if entity remembers seeing a specific item by definition ID.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to search for.</param>
    /// <returns>True if the entity remembers seeing an item with this ID.</returns>
    public bool RemembersItemAvailableById(string itemDefId)
    {
        uint currentTick = GameController.CurrentTick;

        foreach (var observation in _storageObservations.Values)
        {
            if (observation.IsExpired(currentTick))
            {
                continue;
            }

            if (!GodotObject.IsInstanceValid(observation.Facility))
            {
                continue;
            }

            if (observation.Items.Any(i => string.Equals(i.ItemDefId, itemDefId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get all remembered storage observations (non-expired, valid facilities only).
    /// </summary>
    /// <returns>Enumerable of all valid storage observations.</returns>
    public IEnumerable<StorageObservation> GetAllStorageObservations()
    {
        uint currentTick = GameController.CurrentTick;
        return _storageObservations.Values
            .Where(o => !o.IsExpired(currentTick) && GodotObject.IsInstanceValid(o.Facility));
    }

    /// <summary>
    /// Record seeing an entity at a location.
    /// </summary>
    /// <param name="entity">The entity that was seen.</param>
    /// <param name="position">The grid position where the entity was seen.</param>
    public void RecordEntitySighting(Being entity, Vector2I position)
    {
        if (entity == _owner)
        {
            return; // Don't record self-sightings
        }

        uint currentTick = GameController.CurrentTick;

        if (_entitySightings.TryGetValue(entity, out var existing))
        {
            // Update existing sighting
            existing.Update(position, currentTick, currentTick + EntitySightingDuration);
        }
        else
        {
            _entitySightings[entity] = new EntitySighting(
                entity,
                position,
                currentTick,
                currentTick + EntitySightingDuration);
        }
    }

    /// <summary>
    /// Recall where an entity was last seen.
    /// Returns null if no memory or memory expired.
    /// </summary>
    /// <param name="entity">The entity to look up.</param>
    /// <returns>The entity sighting if remembered and valid, null otherwise.</returns>
    public EntitySighting? RecallEntityLocation(Being entity)
    {
        if (_entitySightings.TryGetValue(entity, out var sighting))
        {
            if (!sighting.IsExpired(GameController.CurrentTick) && sighting.IsValid)
            {
                return sighting;
            }
        }

        return null;
    }

    /// <summary>
    /// Get all remembered entity sightings (non-expired, valid).
    /// </summary>
    /// <returns>Enumerable of all valid entity sightings.</returns>
    public IEnumerable<EntitySighting> GetAllEntitySightings()
    {
        uint currentTick = GameController.CurrentTick;
        return _entitySightings.Values
            .Where(s => !s.IsExpired(currentTick) && s.IsValid);
    }

    /// <summary>
    /// Find all remembered sightings of entities of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity to find.</typeparam>
    /// <returns>Enumerable of entity sightings for entities of type T.</returns>
    public IEnumerable<EntitySighting> GetSightingsOfType<T>()
        where T : Being
    {
        uint currentTick = GameController.CurrentTick;
        return _entitySightings.Values
            .Where(s => !s.IsExpired(currentTick) && s.IsValid && s.Entity is T);
    }

    /// <summary>
    /// Remember visiting a location.
    /// </summary>
    /// <param name="position">The grid position visited.</param>
    /// <param name="description">Optional description of what was found there.</param>
    public void RememberLocation(Vector2I position, string? description = null)
    {
        uint currentTick = GameController.CurrentTick;

        if (_locationMemories.TryGetValue(position, out var existing))
        {
            existing.Update(currentTick, currentTick + DefaultMemoryDuration, description);
        }
        else
        {
            _locationMemories[position] = new LocationMemory(
                position,
                description,
                currentTick,
                currentTick + DefaultMemoryDuration);
        }
    }

    /// <summary>
    /// Check if entity remembers a location.
    /// </summary>
    /// <param name="position">The grid position to check.</param>
    /// <returns>True if the location is remembered and not expired.</returns>
    public bool RemembersLocation(Vector2I position)
    {
        if (_locationMemories.TryGetValue(position, out var memory))
        {
            return !memory.IsExpired(GameController.CurrentTick);
        }

        return false;
    }

    /// <summary>
    /// Recall memory of a location.
    /// Returns null if no memory or memory expired.
    /// </summary>
    /// <param name="position">The grid position to recall.</param>
    /// <returns>The location memory if remembered, null otherwise.</returns>
    public LocationMemory? RecallLocation(Vector2I position)
    {
        if (_locationMemories.TryGetValue(position, out var memory))
        {
            if (!memory.IsExpired(GameController.CurrentTick))
            {
                return memory;
            }
        }

        return null;
    }

    /// <summary>
    /// Get all remembered locations (non-expired).
    /// </summary>
    /// <returns>Enumerable of all valid location memories.</returns>
    public IEnumerable<LocationMemory> GetAllLocationMemories()
    {
        uint currentTick = GameController.CurrentTick;
        return _locationMemories.Values.Where(m => !m.IsExpired(currentTick));
    }

    /// <summary>
    /// Record an observation of a facility at a specific location.
    /// Call this when entity discovers or visits a facility.
    /// </summary>
    public void ObserveFacility(string facilityType, Facility facility, Area? area, Vector2I position)
    {
        uint currentTick = GameController.CurrentTick;

        // Update existing observation if found
        var existing = _facilityObservations.FirstOrDefault(f =>
            f.FacilityType == facilityType && f.Facility == facility);
        if (existing != null)
        {
            existing.Update(currentTick, currentTick + StorageMemoryDuration);
            return;
        }

        // Capture storage tags at observation time — the entity can see what this facility stores
        var storageTrait = facility.SelfAsEntity().GetTrait<StorageTrait>();
        IReadOnlyList<string> storageTags = storageTrait != null && storageTrait.Tags.Count > 0
            ? storageTrait.Tags.ToList()
            : Array.Empty<string>();

        _facilityObservations.Add(new FacilityObservation(
            facilityType, facility, area, position, storageTags,
            currentTick, currentTick + StorageMemoryDuration));
    }

    /// <summary>
    /// Recall all remembered facilities of a specific type that are still valid and not expired.
    /// </summary>
    public List<FacilityObservation> RecallFacilitiesOfType(string facilityType)
    {
        uint currentTick = GameController.CurrentTick;
        return _facilityObservations
            .Where(f => f.FacilityType == facilityType && !f.IsExpired(currentTick) && f.IsValid)
            .ToList();
    }

    /// <summary>
    /// Remove expired memories to free memory.
    /// Call periodically (e.g., once per game hour or during idle time).
    /// </summary>
    public void CleanupExpiredMemories()
    {
        uint currentTick = GameController.CurrentTick;

        // Clean storage observations
        var expiredStorage = _storageObservations
            .Where(kv => kv.Value.IsExpired(currentTick) || !GodotObject.IsInstanceValid(kv.Key))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expiredStorage)
        {
            _storageObservations.Remove(key);
        }

        // Clean entity sightings
        var expiredSightings = _entitySightings
            .Where(kv => kv.Value.IsExpired(currentTick) || !kv.Value.IsValid)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expiredSightings)
        {
            _entitySightings.Remove(key);
        }

        // Clean location memories
        var expiredLocations = _locationMemories
            .Where(kv => kv.Value.IsExpired(currentTick))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expiredLocations)
        {
            _locationMemories.Remove(key);
        }

        // Clean facility observations
        _facilityObservations.RemoveAll(f => f.IsExpired(currentTick) || !f.IsValid);
    }

    /// <summary>
    /// Clear all memories. Use when entity is reset or for testing.
    /// </summary>
    public void ClearAllMemories()
    {
        _storageObservations.Clear();
        _entitySightings.Clear();
        _locationMemories.Clear();
        _facilityObservations.Clear();
    }

    /// <summary>
    /// Get count of active (non-expired) memories for debugging.
    /// </summary>
    /// <returns>Tuple of (storage count, entity count, location count).</returns>
    public (int storage, int entities, int locations) GetMemoryCounts()
    {
        uint currentTick = GameController.CurrentTick;
        return (
            _storageObservations.Count(kv => !kv.Value.IsExpired(currentTick) && GodotObject.IsInstanceValid(kv.Key)),
            _entitySightings.Count(kv => !kv.Value.IsExpired(currentTick) && kv.Value.IsValid),
            _locationMemories.Count(kv => !kv.Value.IsExpired(currentTick)));
    }

    /// <summary>
    /// Get a debug summary string of the current memory state.
    /// </summary>
    /// <returns>A human-readable summary of memory contents.</returns>
    public string GetDebugSummary()
    {
        var (storage, entities, locations) = GetMemoryCounts();
        int facilities = _facilityObservations.Count(f => !f.IsExpired(GameController.CurrentTick) && f.IsValid);
        return $"PersonalMemory[{_owner.Name}]: {storage} storage obs, {entities} entity sightings, {locations} locations, {facilities} facilities";
    }
}

/// <summary>
/// A snapshot of an item's key properties at the time of observation.
/// Immutable record to prevent accidental modification.
/// </summary>
/// <param name="ItemDefId">The item definition ID.</param>
/// <param name="Name">The display name of the item.</param>
/// <param name="Quantity">The quantity observed.</param>
/// <param name="Tags">The tags on the item definition.</param>
public record ItemSnapshot(
    string ItemDefId,
    string Name,
    int Quantity,
    List<string> Tags);

/// <summary>
/// A memory of what was observed in a storage container.
/// </summary>
public class StorageObservation
{
    /// <summary>
    /// Gets the facility containing the observed storage.
    /// </summary>
    public Facility Facility { get; }

    /// <summary>
    /// Gets the tick when this observation was made.
    /// </summary>
    public uint ObservedTick { get; private set; }

    /// <summary>
    /// Gets the tick when this memory expires.
    /// </summary>
    public uint ExpirationTick { get; private set; }

    /// <summary>
    /// Gets snapshots of items that were observed in the storage.
    /// </summary>
    public List<ItemSnapshot> Items { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageObservation"/> class.
    /// Creates a new storage observation.
    /// </summary>
    public StorageObservation(
        Facility facility,
        uint observedTick,
        uint expirationTick,
        List<ItemSnapshot> items)
    {
        Facility = facility;
        ObservedTick = observedTick;
        ExpirationTick = expirationTick;
        Items = items;
    }

    /// <summary>
    /// Check if this memory has expired.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>True if the memory has expired.</returns>
    public bool IsExpired(uint currentTick) => currentTick > ExpirationTick;

    /// <summary>
    /// Check if any observed item has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>True if any item has the tag.</returns>
    public bool HasItemWithTag(string tag)
    {
        return Items.Any(i => i.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the total quantity of items with a specific tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>The total quantity of matching items.</returns>
    public int GetQuantityWithTag(string tag)
    {
        return Items
            .Where(i => i.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Sum(i => i.Quantity);
    }

    /// <summary>
    /// Get the total quantity of a specific item by definition ID.
    /// </summary>
    /// <param name="itemDefId">The item definition ID.</param>
    /// <returns>The total quantity of the item.</returns>
    public int GetQuantityById(string itemDefId)
    {
        return Items
            .Where(i => string.Equals(i.ItemDefId, itemDefId, StringComparison.OrdinalIgnoreCase))
            .Sum(i => i.Quantity);
    }
}

/// <summary>
/// A memory of where an entity was last seen.
/// </summary>
public class EntitySighting
{
    /// <summary>
    /// Gets the entity that was sighted.
    /// </summary>
    public Being Entity { get; }

    /// <summary>
    /// Gets the last known position of the entity.
    /// </summary>
    public Vector2I LastKnownPosition { get; private set; }

    /// <summary>
    /// Gets the tick when this sighting was recorded.
    /// </summary>
    public uint ObservedTick { get; private set; }

    /// <summary>
    /// Gets the tick when this memory expires.
    /// </summary>
    public uint ExpirationTick { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntitySighting"/> class.
    /// Creates a new entity sighting record.
    /// </summary>
    public EntitySighting(
        Being entity,
        Vector2I lastKnownPosition,
        uint observedTick,
        uint expirationTick)
    {
        Entity = entity;
        LastKnownPosition = lastKnownPosition;
        ObservedTick = observedTick;
        ExpirationTick = expirationTick;
    }

    /// <summary>
    /// Update this sighting with new information.
    /// </summary>
    public void Update(Vector2I position, uint observedTick, uint expirationTick)
    {
        LastKnownPosition = position;
        ObservedTick = observedTick;
        ExpirationTick = expirationTick;
    }

    /// <summary>
    /// Check if this memory has expired.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>True if the memory has expired.</returns>
    public bool IsExpired(uint currentTick) => currentTick > ExpirationTick;

    /// <summary>
    /// Gets a value indicating whether check if the entity reference is still valid (not freed).
    /// </summary>
    public bool IsValid => GodotObject.IsInstanceValid(Entity);

    /// <summary>
    /// Get the age of this sighting in ticks.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>The number of ticks since this sighting was recorded.</returns>
    public uint GetAge(uint currentTick) => currentTick - ObservedTick;
}

/// <summary>
/// A memory of visiting a location.
/// </summary>
public class LocationMemory
{
    /// <summary>
    /// Gets the grid position of the remembered location.
    /// </summary>
    public Vector2I Position { get; }

    /// <summary>
    /// Gets optional description of what was found at this location.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the tick when this location was visited.
    /// </summary>
    public uint VisitedTick { get; private set; }

    /// <summary>
    /// Gets the tick when this memory expires.
    /// </summary>
    public uint ExpirationTick { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationMemory"/> class.
    /// Creates a new location memory.
    /// </summary>
    public LocationMemory(
        Vector2I position,
        string? description,
        uint visitedTick,
        uint expirationTick)
    {
        Position = position;
        Description = description;
        VisitedTick = visitedTick;
        ExpirationTick = expirationTick;
    }

    /// <summary>
    /// Update this memory with new visit information.
    /// </summary>
    public void Update(uint visitedTick, uint expirationTick, string? description = null)
    {
        VisitedTick = visitedTick;
        ExpirationTick = expirationTick;
        if (description != null)
        {
            Description = description;
        }
    }

    /// <summary>
    /// Check if this memory has expired.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>True if the memory has expired.</returns>
    public bool IsExpired(uint currentTick) => currentTick > ExpirationTick;

    /// <summary>
    /// Get the age of this memory in ticks.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>The number of ticks since this location was visited.</returns>
    public uint GetAge(uint currentTick) => currentTick - VisitedTick;
}

/// <summary>
/// A memory of observing a facility at a specific location.
/// </summary>
public class FacilityObservation
{
    /// <summary>
    /// Gets the type of facility observed (e.g., "corpse_pit", "altar").
    /// </summary>
    public string FacilityType { get; }

    /// <summary>
    /// Gets the facility that was observed.
    /// </summary>
    public Facility Facility { get; }

    /// <summary>
    /// Gets the area the facility is in.
    /// </summary>
    public Area? Area { get; }

    /// <summary>
    /// Gets the grid position of the facility.
    /// </summary>
    public Vector2I Position { get; }

    /// <summary>
    /// Gets the storage tags observed on this facility (e.g., "food", "grain").
    /// Empty if the facility has no storage.
    /// </summary>
    public IReadOnlyList<string> StorageTags { get; }

    /// <summary>
    /// Gets the tick when this observation was made.
    /// </summary>
    public uint ObservedTick { get; private set; }

    /// <summary>
    /// Gets the tick when this memory expires.
    /// </summary>
    public uint ExpirationTick { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FacilityObservation"/> class.
    /// </summary>
    public FacilityObservation(
        string facilityType,
        Facility facility,
        Area? area,
        Vector2I position,
        IReadOnlyList<string> storageTags,
        uint observedTick,
        uint expirationTick)
    {
        FacilityType = facilityType;
        Facility = facility;
        Area = area;
        Position = position;
        StorageTags = storageTags;
        ObservedTick = observedTick;
        ExpirationTick = expirationTick;
    }

    /// <summary>
    /// Check if this memory has expired.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <returns>True if the memory has expired.</returns>
    public bool IsExpired(uint currentTick) => currentTick > ExpirationTick;

    /// <summary>
    /// Gets a value indicating whether the facility reference is still valid (not freed).
    /// </summary>
    public bool IsValid => GodotObject.IsInstanceValid(Facility);

    /// <summary>
    /// Update this observation with new timing information.
    /// </summary>
    public void Update(uint observedTick, uint expirationTick)
    {
        ObservedTick = observedTick;
        ExpirationTick = expirationTick;
    }
}
