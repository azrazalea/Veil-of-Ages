# /entities/being_services

## Purpose

This directory contains service classes that provide specific functionality to Being entities. These services are composed into Being instances and handle discrete subsystems like perception processing, needs management, and movement control. This follows a composition-over-inheritance pattern.

## Files

### BeingPerceptionSystem.cs
Processes raw sensory data into meaningful perception for an entity.

**Responsibilities:**
- Converts `ObservationData` to filtered `Perception`
- Implements line-of-sight using Bresenham's algorithm
- Manages position-based memory with timestamps
- Handles detection based on sense types (Sight, Hearing, Smell)
- Filters sensables based on detection difficulty vs perception level

**Key Methods:**
- `ProcessPerception(ObservationData)` - Main processing entry point
- `HasLineOfSight(Vector2I)` - Bresenham's line algorithm for sight checks
- `GetMemoryAt(Vector2I)` - Retrieve stored memories about a position
- `HasMemoryOfEntityType<T>()` - Check if entity remembers seeing a type

### BeingNeedsSystem.cs
Manages the collection of needs for a Being entity.

**Responsibilities:**
- Stores needs in a dictionary by ID
- Provides need lookup and modification
- Updates all needs each tick via decay

**Key Methods:**
- `AddNeed(Need)` - Register a new need
- `GetNeed(string id)` - Retrieve need by ID
- `UpdateNeeds()` - Decay all needs (called each tick)
- `HasNeed(string id)` - Check if need exists
- `GetAllNeeds()` - Enumerate all registered needs

### MovementController.cs
Handles all movement logic for a Being, including animation.

**Responsibilities:**
- Grid-based movement with movement points system
- Smooth visual interpolation between grid positions
- Animation state management (idle/walk, flip direction)
- Terrain difficulty integration
- Grid cell registration/deregistration

**Key Features:**
- Movement points accumulate per tick (`_movementPointsPerTick`)
- Cardinal moves cost 1.0, diagonal moves cost ~1.414
- Terrain difficulty multiplies movement cost
- Grid position updates immediately; visual catches up over time

**Key Methods:**
- `TryMoveToGridPosition(Vector2I)` - Initiate movement to adjacent cell
- `ProcessMovementTick()` - Update visual position and check completion
- `IsMoving()` - Check if movement is in progress
- `GetCurrentGridPosition()` - Get entity's grid position
- `GetFacingDirection()` - Get direction entity is facing

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `BeingPerceptionSystem` | Perception processing and memory management |
| `BeingNeedsSystem` | Need collection and update management |
| `MovementController` | Grid movement and animation control |

## Important Notes

### Perception Memory Duration
- Default memory duration is 3,000 ticks (roughly 5 minutes game time)
- Memory is position-based with key-value storage
- Memories are cleaned up automatically each perception cycle
- Entity types are stored with `entity_{TypeName}` keys

### Movement Point System
- Entities accumulate movement points each tick
- Move begins when `TryMoveToGridPosition()` is called
- Grid position updates immediately (for collision/pathfinding)
- Visual position interpolates over time until points >= cost
- Leftover points carry forward to next move

### Line of Sight Algorithm
- Uses Bresenham's line algorithm for efficiency
- `IsLOSBlocking()` is virtual and returns false by default
- Can be overridden for entities with different blocking rules
- Checks all cells between entity and target

### Animation Integration
- Uses `AnimatedSprite2D` child node named "AnimatedSprite2D"
- Automatically plays "walk" when moving, "idle" when stopped
- `FlipH` property set based on movement direction
- Uses `CallDeferred("play", ...)` for thread safety

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner entity reference
- `VeilOfAges.Entities.Sensory` - Perception data types
- `VeilOfAges.Entities.Beings.Health` - Health system integration
- `VeilOfAges.Entities.Needs` - Need class
- `VeilOfAges.Core.Lib` - PathFinder, Utils
- `VeilOfAges.Grid` - Area and Tile classes

### Depended On By
- `VeilOfAges.Entities.Being` - Creates and owns these services
- All trait implementations that use perception or movement
