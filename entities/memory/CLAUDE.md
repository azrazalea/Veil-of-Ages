# Memory System

## CRITICAL DESIGN RULE - REMOTE STORAGE ACCESS BANNED

**Entities CANNOT remotely access storage contents. EVER. FOR ANY REASON. NEVER.**

This is a fundamental design principle that MUST NOT be violated. AI agents are BANNED from adding any capability for entities to remotely know storage contents.

### How Entities Know About Storage

1. **Personal Memory** - They visited the storage and observed it. Memory decays over time and may be wrong.

2. **Shared Knowledge** - They know WHERE buildings are (e.g., "the bakery is at position 45,23"). They do NOT know what's inside.

3. **Physical Presence** - They go to the building and call AccessStorage(), which updates their memory.

### Correct Patterns

```csharp
// CORRECT: Check memory for what I remember
if (_owner.Memory?.RemembersItemAvailable("wheat") == true)
{
    // I remember seeing wheat somewhere
}

// CORRECT: Query memory for specific building
var observation = _owner.Memory?.RecallStorageContents(building);
if (observation?.HasItemWithTag("food") == true)
{
    // I remember this building having food
}

// CORRECT: Physically go to building, then access storage
// (AccessStorage will return null if not adjacent)
var storage = _owner.AccessStorage(building);
if (storage != null)
{
    // Now I'm here and can see/take/put items
    // My memory is also updated
}

// CORRECT: If I don't know, go check
if (!_owner.Memory?.RemembersItemAvailableById("wheat") ?? true)
{
    // Start CheckHomeStorageActivity to go observe storage
}
```

### BANNED Patterns

```csharp
// BANNED: Direct remote storage access
var storage = building.GetStorage(); // NEVER for gameplay logic

// BANNED: Checking real storage contents remotely
if (building.GetStorage()?.HasItem("wheat") == true) // NEVER

// BANNED: Any method that returns live storage data without proximity check
```

---

## Purpose

Provides a three-tier memory architecture for entity knowledge and observations, enabling realistic information flow in the simulation.

## Architecture

### SharedKnowledge (Shared by Reference)
- **Read-only** from entity perspective during Think()
- Entities hold `List<SharedKnowledge>` references - multiple sources composable
- Different scopes hold different granularity (no conflicts):
  - Village scope: "bakery at coordinates (45, 23)"
  - Kingdom scope: "Village Millbrook has a bakery"
- Sources added/removed dynamically (entity moves, joins faction, etc.)
- Scope identified by string, not enum (flexible for future expansion)
- **Does NOT contain storage contents** - that would be unrealistic

### PersonalMemory (Owned per Entity)
- Storage observations: "I saw 5 bread at bakery at tick 12000"
- Entity sightings: "I saw Bob at town square at tick 11500"
- Must personally observe to know - no omniscient knowledge
- Configurable expiration durations per memory type

### Thread Safety
- SharedKnowledge is read-only during entity Think() (background thread)
- SharedKnowledge updates happen on main thread only
- PersonalMemory accessed only by owning entity during its Think()
- **All query methods return snapshot copies** for thread-safe background access:
  - `GetBuildingsOfType()`, `GetAllBuildings()` return list copies
  - `GetKnownBuildingTypes()`, `GetAllLandmarkNames()`, `GetAllFactKeys()` return key copies
  - `TryGetLandmark()` returns value type (inherently safe)
  - Direct property access to `Buildings`, `Landmarks`, `Facts` has been removed

## Memory Cleanup Schedule

Memory cleanup is coordinated by `GameController` and runs every 32 ticks (via `DECAY_TICK_INTERVAL`):

1. `GameController.ProcessNextTick()` calls `World.ProcessMemoryCleanup()` every 32 ticks
2. `World.ProcessMemoryCleanup()` iterates all entities:
   - Calls `being.Memory?.CleanupExpiredMemories()` for each Being
   - Calls `village.CleanupInvalidReferences()` for each Village
3. This removes expired personal memories and invalid SharedKnowledge references

At 8 ticks/second, cleanup runs approximately every 4 real seconds (32 ticks / 8 = 4 seconds).

## Future Enhancements (Document Only - Not Implemented)

### Timestamped Knowledge Propagation
SharedKnowledge will support immutable updates with timestamps for realistic information spread:
- Merchant arrives with `KingdomKnowledge` timestamped tick 50000 (newer than village's tick 45000)
- Merchant tells villager -> villager's reference updates
- Villager gossips -> knowledge spreads through social network
- Enables realistic "news travels" mechanics

### Merged Lookup Cache
For performance, a merged view of all SharedKnowledge sources could provide O(1) lookup instead of iterating through sources. Implementation deferred until profiling shows need.

## Files

### SharedKnowledge.cs
Contains the `BuildingReference` class and `SharedKnowledge` base class:

**BuildingReference** - Lightweight reference to a building with thread-safe cached properties:
- `Building` - The referenced building (may become invalid)
- `Position` - Cached entrance/origin position (thread-safe)
- `BuildingType` - Cached type for filtering (thread-safe)
- `BuildingName` - Cached name for display (thread-safe)
- `Area` - The area this building is in (for cross-area routing)
- `IsValid` - Whether building still exists

**SharedKnowledge** - Base class for shared knowledge scopes:
- `Id`, `Name`, `ScopeType` - Identification properties
- Building location registry (scope-appropriate granularity)
- Facility registry (facility type -> buildings)
- Transition point registry (for cross-area routing)
- Landmark storage (named positions like "town_square")
- General facts (key-value for flexibility)
- Query methods (all return thread-safe snapshot copies):
  - `GetBuildingsOfType()`, `GetAllBuildings()` - Building queries
  - `TryGetBuildingOfType()`, `GetNearestBuildingOfType()` - Building lookup
  - `GetKnownBuildingTypes()` - List known building types
  - `GetFacilitiesOfType(facilityType)` - Get all valid facilities of a type
  - `GetNearestFacilityOfType(facilityType, currentArea, fromPosition)` - Find nearest facility with same-area preference
  - `GetAllTransitionPoints()`, `GetTransitionPointsInArea(area)` - Transition point queries
  - `TryGetLandmark()`, `GetAllLandmarkNames()` - Landmark queries
  - `TryGetFact()`, `GetAllFactKeys()` - Fact queries
- Registration methods: `RegisterBuilding()`, `UnregisterBuilding()` (main thread only)
- Cleanup: `CleanupInvalidReferences()` for removing destroyed buildings

### PersonalMemory.cs
Per-entity memory storage with built-in data types:

**Data Types** (defined in same file):
- `ItemSnapshot` - Immutable record of item type/quantity observed
- `StorageObservation` - What entity saw in a storage container with expiration
- `EntitySighting` - Where entity last saw another entity with expiration
- `LocationMemory` - Memory of visiting a location with expiration
- `FacilityObservation` - Personally discovered facility with building, area, position, and expiration

**PersonalMemory Class**:
- Owner reference for context
- Configurable expiration durations: `DefaultMemoryDuration`, `StorageMemoryDuration`, `EntitySightingDuration`

**Storage Observation Methods**:
- `ObserveStorage(building, storage)` - Record observation of storage contents
- `RecallStorageContents(building)` - Get remembered contents for a building
- `RecallStorageWithItem(itemTag)` - Find buildings with remembered item by tag
- `RecallStorageWithItemById(itemDefId)` - Find buildings with remembered item by ID
- `RemembersItemAvailable(itemTag)` - Check if any storage had item by tag
- `RemembersItemAvailableById(itemDefId)` - Check if any storage had item by ID
- `GetAllStorageObservations()` - Get all valid storage observations

**Entity Sighting Methods**:
- `RecordEntitySighting(entity, position)` - Record seeing an entity
- `RecallEntityLocation(entity)` - Get last known location
- `GetAllEntitySightings()` - Get all valid sightings
- `GetSightingsOfType<T>()` - Get sightings filtered by entity type

**Facility Observation Methods**:
- `ObserveFacility(facilityType, building, area, position)` - Record discovering a facility
- `RecallFacilitiesOfType(facilityType)` - Get remembered facilities of type (valid and not expired)

**Location Memory Methods**:
- `RememberLocation(position, description)` - Record visiting a place
- `RemembersLocation(position)` - Check if location is remembered
- `RecallLocation(position)` - Get memory for a location
- `GetAllLocationMemories()` - Get all valid location memories

**Maintenance Methods**:
- `CleanupExpiredMemories()` - Remove expired/invalid memories
- `ClearAllMemories()` - Reset all memories
- `GetMemoryCounts()` - Get counts for debugging
- `GetDebugSummary()` - Human-readable summary

### MemoryStructures.cs
Placeholder file for future memory types. Currently empty as all types are defined in PersonalMemory.cs and SharedKnowledge.cs.

## Integration with Being

Being.cs provides wrapper methods and combined lookup functionality for the memory system:

### Storage ACTION Methods (Require Physical Proximity)
These methods REQUIRE the entity to be adjacent to the building (within 1 tile). They return null/false if the entity is not physically present:
- `AccessStorage(building)` - Get storage and observe contents (returns null if not adjacent)
- `TakeFromStorage(building, itemDefId, quantity)` - Take item and observe (returns null if not adjacent)
- `TakeFromStorageByTag(building, itemTag, quantity)` - Take by tag and observe (returns null if not adjacent)
- `PutInStorage(building, item)` - Put item and observe (returns false if not adjacent)

### Storage CHECK Methods (Memory Only - No Proximity Required)
These methods query MEMORY ONLY and do NOT access real storage. They can be called from anywhere:
- `StorageHasItem(building, itemDefId, quantity)` - Check MEMORY for item
- `StorageHasItemByTag(building, itemTag)` - Check MEMORY for item by tag
- `GetStorageItemCount(building, itemDefId)` - Get REMEMBERED count (may be stale)

### Combined Knowledge Lookup
These methods search both PersonalMemory and SharedKnowledge:
- `FindItemLocations(itemTag)` - Find buildings with item by tag, personal memory first
- `FindItemLocationsById(itemDefId)` - Find buildings with item by ID, personal memory first
- `FindItemInBuildingType(itemTag, buildingType)` - Filter by building type
- `HasIdeaWhereToFind(itemTag)` - Quick check if any leads exist

### SharedKnowledge Helpers on Being
- `AddSharedKnowledge(knowledge)` / `RemoveSharedKnowledge(knowledge)` - Manage sources
- `GetSharedKnowledgeByScope(scopeType)` - Get specific source
- `TryFindBuildingOfType(type, out building)` - Find from any source
- `FindNearestBuildingOfType(type, position)` - Find nearest from any source
- `GetAllBuildingsOfType(type)` - Get all from all sources

## Usage Example

```csharp
// Entity queries combined knowledge for building
if (entity.TryFindBuildingOfType("Bakery", out var bakery))
{
    // Found via SharedKnowledge
}

// Entity observes storage personally
entity.Memory?.ObserveStorage(building, storage);

// Later, entity recalls what they saw
var memories = entity.Memory?.RecallStorageWithItem("food");
```

## Dependencies

### Depends On
- `VeilOfAges.Entities` - Building, Being classes
- `VeilOfAges.Entities.Building` - Building namespace (used in PersonalMemory)
- `VeilOfAges.Entities.Items` - IStorageContainer, Item for storage observation
- `VeilOfAges.Core` - GameController for current tick
- `Godot` - GodotObject, Vector2I

### Depended On By
- `Being` - Holds PersonalMemory and SharedKnowledge references, provides wrapper methods
- `Village` - Creates SharedKnowledge for village scope, distributes to residents
- Traits that need knowledge/memory access (consumption, pathfinding, social)
