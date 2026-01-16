# World Generation Module

## Purpose

The `/world/generation` directory contains procedural generation systems for creating the game world. It handles terrain generation, village layout, building placement, and entity spawning. This module creates the initial world state when a new game begins.

## Files

### GridGenerator.cs
Main world generation orchestrator. Godot node that coordinates all generation phases.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `GridGenerator` (extends `Node`)
- **Exported Properties**:
  - `TreeScene`, `BuildingScene`: PackedScene references for instancing
  - `SkeletonScene`, `ZombieScene`, `TownsfolkScene`: Entity scene references
  - `NumberOfTrees`: Tree count target (default: 20)
- **Generation Phases**:
  1. `GenerateTerrain()`: Fills world with grass, adds dirt patches, creates water pond
  2. `VillageGenerator.GenerateVillage()`: Places buildings and spawns villagers
  3. `GenerateTrees()`: Places trees in unoccupied walkable cells
  4. `GenerateDecorations()`: (Currently commented out) Placeholder for decorative elements

### VillageGenerator.cs
Handles village layout generation including buildings, characters, and connecting paths.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `VillageGenerator` (non-Godot, plain C# class)
- **Constructor Requirements**:
  - `Area gridArea`: Target grid area
  - `Node entitiesContainer`: Parent node for spawned entities
  - `PackedScene` references for buildings and entity types
  - `EntityThinkingSystem`: For registering spawned beings
  - Optional `seed` for deterministic generation
- **Key Fields**:
  - `_placedFarms`: Tracks placed farms for assigning farmer jobs
  - `_placedHouses`: Tracks placed houses for villager assignment
  - `_spawnedVillagers`: List of all spawned villager `Being` instances for debug selection
- **Key Features**:
  - Circular building placement around village center
  - Directional building spawning (supports 8 directions + diagonals)
  - Character spawning near buildings with entrance prioritization
  - Simplified pathfinding for creating dirt paths
  - Related building connections (e.g., church to graveyard)
  - Random debug villager selection via `EnableDebugOnRandomVillager()`

## Key Classes/Interfaces

| Class | Namespace | Description |
|-------|-----------|-------------|
| `GridGenerator` | `VeilOfAges.WorldGeneration` | Godot node, main generation entry point |
| `VillageGenerator` | `VeilOfAges.WorldGeneration` | Village layout and population generator |

## Important Notes

### Generation Flow
```
World._Ready()
    -> GridGenerator.Generate() [deferred call]
        -> GenerateTerrain()
        -> VillageGenerator.GenerateVillage()
            -> CreateVillageSquare()
            -> PlaceVillageBuildings()
            -> CreateVillagePaths()
            -> EnableDebugOnRandomVillager()
        -> GenerateTrees()
```

### Building Placement Algorithm
1. Buildings are placed in a circle around village center (radius ~15 tiles)
2. Uses `BuildingManager.GetTemplate()` to get building sizes
3. Validates placement area is walkable and not in village square
4. Up to 10 placement attempts with increasing distance
5. Supports special handling per building type (e.g., Graveyard spawns Church nearby)

### Entity Spawning
- Characters spawn near their associated buildings
- Uses `FindPositionInFrontOfBuilding()` which prioritizes:
  1. Bottom (entrance) of building
  2. Right, Top, Left sides
  3. Expanding perimeter search (radius 2-3)
- All spawned `Being` entities are registered with `EntityThinkingSystem`

### Path Generation
- Uses simplified pathfinding (not true A*) for visual paths
- Prefers horizontal movement, then vertical
- Handles obstacle avoidance with limited backtracking
- Converts path cells to `Area.PathTile` (dirt)
- Connects:
  - All buildings to village center
  - Related buildings (Church <-> Graveyard)

### Building Types Currently Supported
- `"Simple Farm"`
- `"Graveyard"` (spawns Church nearby, undead entities)
- `"Simple House"` (spawns townsfolk)
- `"Church"`

### Random Number Generation
- `GridGenerator` uses `RandomNumberGenerator.Randomize()` for non-deterministic seeds
- `VillageGenerator` supports optional seed parameter for reproducible generation

### Performance Considerations
- Tree placement uses up to 3x `NumberOfTrees` attempts to handle collisions
- `FindPositionForBuildingNear()` has nested loops - can be slow with high `wiggleRoom`
- Path generation is O(distance) but may loop on blocked paths

### Debug Villager Selection
Debug mode is enabled for one randomly selected villager during village generation:
- Before spawning, `_debugVillagerIndex` is pre-determined using the RNG based on expected villager count
- During `SpawnVillagerNearBuilding()`, each villager checks if its spawn index matches `_debugVillagerIndex`
- The matching villager has `debugEnabled: true` passed to its `Initialize()` call
- After all spawning, `LogDebugVillagerSelection()` logs which villager was selected
- The `_spawnedVillagers` list tracks all spawned villagers for this purpose

This helps with debugging AI behavior by providing detailed logs from one villager's perspective without flooding the console with output from all entities.

### Known Quirks
- Debug print statements remain in code (`"Hello my baby"`, `"Hello my darling"`)
- `GenerateDecorations()` method exists but is commented out
- Water pond position uses magic numbers for bounds (15, 25)

## Dependencies

### This Module Depends On
- `VeilOfAges.Grid` - `Area`, `Utils` for grid operations
- `VeilOfAges.Entities` - `Being`, `Tree` for entity instantiation
- `VeilOfAges.Core` - `EntityThinkingSystem`, `BuildingManager`
- Godot types: `Node`, `PackedScene`, `RandomNumberGenerator`

### What Depends On This Module
- `VeilOfAges.World` - Calls `GridGenerator.Generate()` from `_Ready()`
