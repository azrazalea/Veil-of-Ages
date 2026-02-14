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
Handles all movement logic for a Being, including sprite direction.

**Responsibilities:**
- Grid-based movement with movement points system
- Smooth visual interpolation between grid positions
- Sprite direction management (FlipH based on movement direction)
- Terrain difficulty integration
- Grid cell registration/deregistration

**Key Features:**
- Movement points accumulate per tick (`_movementPointsPerTick`)
- Cardinal moves cost 1.0, diagonal moves cost ~1.414
- Terrain difficulty multiplies movement cost
- Grid position updates immediately; visual catches up over time
- Sprite flipping handled automatically based on movement direction

**Key Methods:**
- `TryMoveToGridPosition(Vector2I)` - Initiate movement to adjacent cell
- `ProcessMovementTick()` - Update visual position and check completion
- `IsMoving()` - Check if movement is in progress
- `GetCurrentGridPosition()` - Get entity's grid position
- `GetFacingDirection()` - Get direction entity is facing
- `UpdateSpriteDirection()` - Update sprite flip based on movement
- `FlipSprite(Sprite2D)` - Apply horizontal flip to sprite

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
- `IsLOSBlocking()` is virtual and returns false by default (placeholder)
- Can be overridden for entities with different blocking rules
- Checks all cells between entity and target

### LOS Implementation Plan (Ready to Implement)

The Bresenham algorithm exists in `HasLineOfSight()`. The placeholder `IsLOSBlocking()` needs implementation:

**Step 1: Add `BlocksLOS` to Tile** (`world/Tile.cs`):
```csharp
public class Tile(
    int sourceId,
    Vector2I atlasCoords,
    bool isWalkable,
    float walkDifficulty = 0,
    bool blocksLOS = false  // NEW
)
```

**Step 2: Update static tiles** (`world/GridArea.cs`):
```csharp
public static Tile WaterTile = new(1, new(3, 16), false, 0, false);
public static Tile GrassTile = new(0, new(1, 3), true, 1.0f, false);
// Wall tiles would have blocksLOS = true
```

**Step 3: Add public accessor to GridArea** for ground tile lookup:
```csharp
public Tile? GetGroundTile(Vector2I pos) => _groundGridSystem.GetCell(pos);
```

**Step 4: Implement IsLOSBlocking** (`BeingPerceptionSystem.cs`):
```csharp
protected bool IsLOSBlocking(Vector2I position)
{
    var gridArea = _owner.GridArea;
    if (gridArea == null) return false;

    // Check ground tile
    var groundTile = gridArea.GetGroundTile(position);
    if (groundTile?.BlocksLOS == true)
        return true;

    // Check for blocking entities (buildings)
    var entity = gridArea.EntitiesGridSystem.GetCell(position);
    if (entity is Building building && building.BlocksLOS)
        return true;

    return false;
}
```

**Step 5: Add BlocksLOS to Building** (if not present):
```csharp
public bool BlocksLOS { get; set; } = true;
```

**Design decisions needed when implementing:**
- Trees/vegetation: block LOS or not?
- Some buildings transparent (fences)?

### Sprite Integration
- Uses `Sprite2D` child nodes (single or multiple layers)
- `FlipH` property set based on movement direction
- No animation playback - entities use static sprites
- Sprite flipping uses `CallDeferred()` for thread safety

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
