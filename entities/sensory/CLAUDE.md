# /entities/sensory

## Purpose

This directory implements the sensory perception system for entities. It provides the data structures and algorithms for how entities detect and perceive the world around them, including other entities and world events.

## Files

### ISensable.cs
Interface for anything that can be sensed by entities.

**SensableType Enum:**
- `Being` - Living/undead entities
- `Building` - Structures

**SenseType Enum:**
- `Hearing` - Audio perception
- `Sight` - Visual perception
- `Smell` - Olfactory perception

**ISensable Interface:**
- `DetectionDifficulties` - Dictionary of per-sense difficulty values
- `GetCurrentGridPosition()` - Location
- `GetSensableType()` - Category of sensable
- `GetDetectionDifficulty(SenseType)` - Default implementation returns 1.0 if not specified

### ObservationData.cs
Container for all raw sensory data available to an entity.

**Properties:**
- `Grid` - ObservationGrid with nearby sensables
- `Events` - List of WorldEvents in range

Passed to entity's `Think()` method each tick.

### ObservationGrid.cs
Grid-based spatial storage for sensables in perception range.

**Key Features:**
- Centered on observer position with range
- Dictionary of positions to sensable lists
- Efficient position-based lookup

**Key Methods:**
- `AddSensable(position, sensable)` - Add to grid
- `GetAtPosition(position)` - Get sensables at position
- `GetCoveredPositions()` - Iterate all positions in range
- `IsInRange(position)` - Check if position is within grid

### Perception.cs
Processed perception data after filtering.

**Storage:**
- `_detectedSensables` - Position-keyed sensable lists
- `_perceivedEvents` - Filtered world events
- `_threatLevels` - Being-keyed threat assessment

**Key Methods:**
- `AddDetectedSensable(sensable, position)` - Store detected entity
- `AddPerceivedEvent(event)` - Store perceived event
- `GetEntitiesOfType<T>()` - Find all entities of specific type

### SpatialPartitioning.cs
Efficient spatial lookup system using grid hashing.

**Key Methods:**
- `Add(sensable)` - Register sensable at its position
- `Clear()` - Reset all data
- `GetAtPosition(position)` - Get sensables at exact position
- `GetInArea(center, range)` - Get all sensables in square area

Used by SensorySystem to build observation data efficiently.

### WorldEvent.cs
Events that occur in the world and can be perceived.

**EventType Enum:**
- `Sound` - Audio events (no LOS needed)
- `Visual` - Visual events (requires LOS)
- `Smell` - Olfactory events
- `Environmental` - Multi-sense events

**WorldEvent Class (abstract):**
- `Position` - Grid location
- `Radius` - Effect radius
- `Intensity` - Detection strength
- `Type` - Event category

**EventSystem Class:**
- `AddEvent(event)` - Register new event
- `GetEventsInRange(center, range)` - Query events
- `ClearEvents()` - Reset each tick

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `ISensable` | Interface for detectable objects |
| `SenseType` | Enum of perception types |
| `ObservationData` | Raw sensory data container |
| `ObservationGrid` | Position-indexed sensable grid |
| `Perception` | Filtered perception results |
| `SpatialPartitioning` | Efficient spatial hashing |
| `WorldEvent` | Base class for world events |
| `EventSystem` | Event management |

## Important Notes

### Perception Processing Flow
1. `SensorySystem.PrepareForTick()` rebuilds spatial partitioning
2. `GetObservationFor(entity)` creates ObservationData
3. `BeingPerceptionSystem.ProcessPerception()` filters to Perception
4. Traits receive Perception in `SuggestAction()`

### Detection Difficulty System
- Difficulty values range 0.0 to 1.0+
- Lower = easier to detect
- Compared against entity's perception level
- Per-sense-type granularity

### Line of Sight
Visual detection requires line of sight (in BeingPerceptionSystem):
```csharp
if (distance <= sightRange && HasLineOfSight(position))
{
    if (perceptionLevel >= detectionDifficulty)
        return true;
}
```

### Event Detection
Events use probabilistic detection:
- Base chance = intensity * perception level
- Distance falloff applied
- Sound uses squared falloff
- Random roll determines success

### Spatial Partitioning Efficiency
The system uses two-level spatial indexing:
1. `SpatialPartitioning` - Global position hash
2. `ObservationGrid` - Per-entity local grid

This avoids O(n^2) entity-to-entity checks.

### Memory vs Perception
- `Perception` - What entity currently sees
- `BeingPerceptionSystem._memory` - What entity remembers seeing

Memory persists across ticks (with decay), perception is rebuilt each tick.

## Dependencies

### Depends On
- Godot Vector2I
- Standard .NET collections
- `VeilOfAges.Core.Lib` - WorldEvent references

### Depended On By
- `VeilOfAges.Entities.SensorySystem` - World-level coordination
- `VeilOfAges.Entities.BeingServices.BeingPerceptionSystem` - Per-entity processing
- All trait implementations that use perception
