# /entities

## Purpose

This directory contains the core entity system for Veil of Ages. It defines the fundamental building blocks for all game entities including beings (living and undead), buildings, and terrain objects. The architecture follows an Entity-Component-Trait pattern where `Being` is the base class and behaviors are composed through modular traits.

## Files

### Being.cs
The central abstract class for all living and undead entities. Extends Godot's `CharacterBody2D` and implements `IEntity<BeingTrait>`. Manages:
- Attribute system (Strength, Dexterity, Constitution, Intelligence, Willpower, Wisdom, Charisma)
- Health/body part system integration
- Trait collection and initialization queue
- Movement delegation to `MovementController`
- AI decision-making via `Think()` method with priority queue
- Dialogue system integration
- Perception and sensory capabilities

### BeingTrait.cs
Specialized trait base class for Being entities. Provides:
- PathFinder integration for movement
- Memory system with timestamps for entity recollection
- Perception helpers for finding entities by type
- Movement helpers (MoveToPosition, MoveNearEntity, MoveToArea, TryToWander)
- Range checking utilities
- Dialogue response generation interface

### EntityAction.cs
Abstract base class for all actions an entity can perform. Uses priority-based execution (lower values = higher priority). Contains:
- Reference to the executing entity
- Priority value for action queue sorting
- Source tracking (which class generated the action)
- Optional callbacks for selection and success events

### EntityThinkingSystem.cs
Multi-threaded AI processing system. A Godot Node that:
- Registers and manages all Being entities
- Processes entity thinking in parallel using semaphore-controlled threads
- Collects pending actions and applies them on the main thread
- Sorts actions by priority before execution
- Handles movement tick processing after action execution

### IEntity.cs
Generic interface for trait-based entities. Provides:
- Trait collection management (`_traits` SortedSet)
- `AddTrait<T>()` and `AddTraitToQueue<T>()` methods for trait registration
- `GetTrait<T>()` and `HasTrait<T>()` for trait queries
- Event broadcasting to all traits via `OnTraitEvent()`

### Trait.cs
Base class for all traits. Implements `IComparable` for priority-based sorting. Contains:
- Initialization state tracking
- Priority property for execution order
- Random number generator for behavioral variety
- Virtual Process() and OnEvent() methods

### SensorySystem.cs
World-level sensory coordination system. Manages:
- Observation data caching per frame
- Spatial partitioning for efficient entity lookup
- ObservationGrid creation for each entity's perception range
- Event system integration for world events

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `Being` | Abstract base for all living/undead entities |
| `BeingTrait` | Specialized trait with Being-specific helpers |
| `EntityAction` | Base class for executable actions |
| `EntityThinkingSystem` | Multi-threaded AI coordinator |
| `IEntity<T>` | Generic interface for trait-based entities |
| `Trait` | Base trait class with priority sorting |
| `SensorySystem` | World-level perception management |
| `BeingAttributes` | Record type for entity attributes |

## Important Notes

### Threading Considerations
- `EntityThinkingSystem.ProcessGameTick()` is async and uses `Task.WhenAll()` for parallel processing
- Entity `Think()` methods run on background threads - avoid direct scene tree manipulation
- Actions are queued and executed on the main thread in `ApplyAllPendingActions()`
- Semaphore limits concurrent processing to `Environment.ProcessorCount - 1`

### Trait Initialization
- Traits use a queue-based initialization system to handle dependencies
- Traits added during initialization are automatically queued for processing
- `IsInitialized` flag prevents double initialization
- Use `AddTraitToQueue<T>()` for cleaner trait registration during initialization

### Action Priority System
- Lower priority values execute first (0 = highest priority)
- Actions are sorted before execution each tick
- Dialogue commands have a specific priority (`TalkCommand.Priority`) that affects behavior

### Memory System Integration in Being

Being.cs provides a complete memory system integration with PersonalMemory and SharedKnowledge.

**Golden Rule**: Entities only know about items in two places:
1. **Inventory** - What they're carrying (immediate, always accurate)
2. **Personal Memory** - What they've personally observed (may be stale)

Entities do NOT omnisciently know what's in any storage container. If memory is empty or stale, the entity must go observe the storage to update their memory (see `CheckHomeStorageActivity`).

### BANNED: Remote Storage Access

**AI AGENTS ARE BANNED FROM ADDING REMOTE STORAGE ACCESS.**

All ACTION methods (AccessStorage, TakeFromStorage, TakeFromStorageByTag, PutInStorage) require physical proximity to the building (within 1 tile). They return null/false if the entity is too far away.

All CHECK methods (StorageHasItem, StorageHasItemByTag, GetStorageItemCount) query MEMORY ONLY. They never access real storage.

This is intentional. Entities must physically travel to storage locations to observe contents. Their memory may become stale. This creates realistic behavior.

**Memory Properties:**
- `Memory` (PersonalMemory?) - Personal observations owned by this entity, created in `Initialize()`
- `SharedKnowledge` (IReadOnlyList<SharedKnowledge>) - Shared knowledge sources by reference

**SharedKnowledge Management:**
- `AddSharedKnowledge(knowledge)` - Add a source (called when joining village/faction)
- `RemoveSharedKnowledge(knowledge)` - Remove a source (called when leaving)
- `GetSharedKnowledgeByScope(scopeType)` - Get specific source by scope

**Building Lookup (from SharedKnowledge):**
- `TryFindBuildingOfType(type, out building)` - Find any building of type
- `FindNearestBuildingOfType(type, position)` - Find nearest building
- `GetAllBuildingsOfType(type)` - Get all buildings of type

**Storage ACTION Methods (Require Physical Proximity):**
- `AccessStorage(building)` - Get storage and observe (returns null if not adjacent)
- `TakeFromStorage(building, itemDefId, quantity)` - Take and observe (returns null if not adjacent)
- `TakeFromStorageByTag(building, itemTag, quantity)` - Take by tag and observe (returns null if not adjacent)
- `PutInStorage(building, item)` - Put and observe (returns false if not adjacent)

**Storage CHECK Methods (Memory Only - No Real Storage Access):**
- `StorageHasItem(building, itemDefId, quantity)` - Check MEMORY for item
- `StorageHasItemByTag(building, itemTag)` - Check MEMORY for item by tag
- `GetStorageItemCount(building, itemDefId)` - Get REMEMBERED count (may be stale)

**Combined Item Search (PersonalMemory + SharedKnowledge):**
- `FindItemLocations(itemTag)` - Find buildings with item, personal memory first
- `FindItemLocationsById(itemDefId)` - Find by item ID
- `FindItemInBuildingType(itemTag, buildingType)` - Filter by building type
- `HasIdeaWhereToFind(itemTag)` - Quick check for any leads

**Usage Example:**
```csharp
// CORRECT: Access storage when physically adjacent (updates memory)
var storage = entity.AccessStorage(building);
if (storage == null)
{
    // Either no storage, or NOT ADJACENT - must travel there first
}

// CORRECT: Check memory for what I remember (no proximity needed)
if (entity.StorageHasItemByTag(building, "food"))
{
    // I REMEMBER seeing food here (may be stale)
}

// CORRECT: Find food combining personal memory and shared knowledge
var foodLocations = entity.FindItemLocations("food");
foreach (var (building, rememberedQty) in foodLocations)
{
    // rememberedQty = -1 means "I know building exists but haven't observed"
    if (rememberedQty > 0)
    {
        // High confidence - I saw food here
    }
}

// CORRECT: Query shared knowledge for buildings (locations, not contents)
if (entity.TryFindBuildingOfType("Farm", out var farm))
{
    // Navigate to farm - SharedKnowledge tells us WHERE, not WHAT's inside
}
```

### Legacy Memory (BeingTrait)
BeingTrait includes an older position-based memory system with timestamps:
- Memory has configurable duration (default 3000 ticks, roughly 5 minutes game time)
- Memory is automatically cleaned up each tick
- This is separate from PersonalMemory and may be deprecated in favor of it

### Entity Debug System
Being includes a per-entity debug logging system for troubleshooting AI behavior:

**Properties and Methods:**
- `DebugEnabled` (bool property): When set to `true`, the entity outputs detailed debug information to the log file
- `DebugLog(string category, string message)` (protected method): Logs a debug message if `DebugEnabled` is true. Internally calls `Log.EntityDebug(Name, category, message)`

**Usage:**
```csharp
// Enable debugging for a specific entity
myBeing.DebugEnabled = true;

// In traits or Being subclasses, use DebugLog to output categorized messages
DebugLog("NEEDS", $"Hunger level: {hungerLevel}");
DebugLog("ACTIVITY", $"Starting activity: {activityName}");
DebugLog("MOVEMENT", $"Path found to {targetPosition}");
```

**Notes:**
- Debug output is per-entity, allowing targeted debugging without flooding the log
- Categories help filter and organize debug output (e.g., "NEEDS", "ACTIVITY", "MOVEMENT")
- The check for `DebugEnabled` happens inside `DebugLog()`, so callers don't need to check it themselves
- Useful for debugging specific villagers or entities without enabling global verbose logging

### Human-Like AI Architecture (BDI Pattern)

The entity AI system intentionally mimics human cognition using a **Belief-Desire-Intention (BDI)** architecture. Entities are NOT omniscient - they act on beliefs that may be wrong.

**Perception Snapshots (Intentional "Stale Data")**:
- `EntityThinkingSystem` captures perception data BEFORE entity thinking begins
- Entities act on what they believed was true at tick start, not real-time state
- Creates emergent behavior: entity moves to where target *was*, must search if target moved
- This is intentional design, not a bug - avoids robotic instant-reaction behavior

**Priority-Based Interruption ("One Track Mind")**:
- Traits suggest actions with priority values (lower number = more urgent)
- Entity continues current behavior unless something more urgent arises
- Priority examples:
  - Idle wandering: priority 1
  - Normal tasks: priority 0
  - Hungry (not critical): priority 0 (won't interrupt)
  - Starving (critical): priority -2 (interrupts most things)
  - Emergency/threat: priority -10 (interrupts almost everything)
- This creates focused behavior - entities don't constantly re-evaluate unless urgent

**Two-Tier Memory System (Implemented)**:

*PersonalMemory* (`Being.Memory`):
- Storage observations: "I saw 5 bread at bakery at tick 12000"
- Entity sightings: "I saw Bob at town square at tick 11500"
- Location memories: Places visited with timestamps
- Must personally observe to know - no omniscient knowledge
- Configurable expiration durations per memory type

*SharedKnowledge* (`Being.SharedKnowledge`):
- Common knowledge shared by community/faction by reference
- All villagers know where the well, town hall, and farms are
- Village scope: exact building coordinates
- Kingdom scope (future): "Village X has a bakery"
- New members inherit community knowledge when added to village
- Does NOT contain storage contents (that's PersonalMemory)

This architecture creates realistic behavior where entities:
- Use shared knowledge for navigation to known buildings
- Use personal memory for observed storage contents and entity sightings
- Act on beliefs that may become outdated
- Stay focused unless urgently interrupted

### Event Queue Pattern (Ready to Implement)

Instead of Godot signals (which execute immediately on emitting thread), use a message queue that processes events during the think cycle. This fits the BDI architecture: events become "new beliefs" processed at think time.

**Step 1: Add event types and queue to Being** (`Being.cs`):
```csharp
public enum EntityEventType
{
    MovementCompleted,
    DamageTaken,
    NeedCritical,
    TargetLost,
    ActionCompleted,
    // Add more as needed
}

public record EntityEvent(EntityEventType Type, object? Data = null);

// Thread-safe queue (written on main thread, read on think thread)
private ConcurrentQueue<EntityEvent> _pendingEvents = new();

public void QueueEvent(EntityEventType type, object? data = null)
{
    _pendingEvents.Enqueue(new EntityEvent(type, data));
}

public List<EntityEvent> ConsumePendingEvents()
{
    var events = new List<EntityEvent>();
    while (_pendingEvents.TryDequeue(out var evt))
    {
        events.Add(evt);
    }
    return events;
}
```

**Step 2: Queue events from main thread** (during action execution):
```csharp
// In MovementController when movement completes:
_owner.QueueEvent(EntityEventType.MovementCompleted, finalGridPos);

// In health system when damage taken:
being.QueueEvent(EntityEventType.DamageTaken, damageAmount);

// In needs system when need becomes critical:
being.QueueEvent(EntityEventType.NeedCritical, needId);
```

**Step 3: Process events at start of Think cycle**:
```csharp
// In Being.Think() - at start of thinking
var pendingEvents = ConsumePendingEvents();
foreach (var evt in pendingEvents)
{
    OnTraitEvent(evt);  // Notify all traits
}
```

**Step 4: Traits react to events**:
```csharp
public override void OnEvent(EntityEvent evt)
{
    switch (evt.Type)
    {
        case EntityEventType.MovementCompleted:
            _arrivedAtDestination = true;
            break;
        case EntityEventType.NeedCritical:
            _urgentNeed = (string)evt.Data!;
            break;
    }
}
```

**Benefits over Godot signals:**
- Events processed during think cycle (not immediately)
- Fits BDI pattern: events become "new beliefs"
- Thread-safe by design (ConcurrentQueue)
- Centralized processing in Think() flow
- No callback complexity or threading issues

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Grid system and pathfinding
- `VeilOfAges.Core.Lib` - Utilities and time system
- `VeilOfAges.UI` - Dialogue system and commands
- `VeilOfAges.Entities.Memory` - PersonalMemory and SharedKnowledge
- Godot engine classes (CharacterBody2D, Node, etc.)

### Depended On By
- All subdirectories within `/entities/`
- `/core/` - GameController references EntityThinkingSystem
- `/world/` - World instantiates and manages entities
