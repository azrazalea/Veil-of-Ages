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

### Memory System
- BeingTrait includes a memory system with position-based storage
- Memory has configurable duration (default 3000 ticks, roughly 5 minutes game time)
- Memory is automatically cleaned up each tick

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

**Two-Tier Memory System (Planned)**:

*Individual Memory* (`BeingTrait._memory` - exists, not yet fully used):
- Personal experiences: "I saw entity X at position Y, 30 ticks ago"
- Last known positions of tracked entities
- Discovered locations through exploration

*Shared/Collective Memory* (not yet implemented):
- Common knowledge shared by community/faction
- All villagers know where the well, town hall, and farms are
- Undead in a graveyard know the graveyard bounds
- New members inherit community knowledge on spawn
- Avoids every entity having to "discover" common locations

This architecture creates realistic behavior where entities:
- Use shared knowledge for navigation to known places
- Use individual memory for dynamic/personal information
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
- Godot engine classes (CharacterBody2D, Node, etc.)

### Depended On By
- All subdirectories within `/entities/`
- `/core/` - GameController references EntityThinkingSystem
- `/world/` - World instantiates and manages entities
