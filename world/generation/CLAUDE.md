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
Handles village layout generation using a lot-based system with road networks.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `VillageGenerator` (non-Godot, plain C# class)
- **Constructor Requirements**:
  - `Area gridArea`: Target grid area
  - `Node entitiesContainer`: Parent node for spawned entities
  - `PackedScene` references for buildings and entity types
  - `EntityThinkingSystem`: For registering spawned beings
  - Optional `seed` for deterministic generation
- **Key Fields**:
  - `_roadNetwork`: `RoadNetwork` instance managing lots and roads
  - `_placedFarms`: Tracks placed farms for assigning farmer jobs
  - `_placedHouses`: Tracks placed houses for villager assignment
  - `_spawnedVillagers`: List of all spawned villager `Being` instances for debug selection
  - `_currentVillage`: The `Village` being generated (tracks buildings and residents)
- **Key Features**:
  - Lot-based building placement via `RoadNetwork`
  - Priority-based building placement (required buildings first, then houses)
  - Character spawning near buildings with entrance prioritization
  - Job assignment: farmers work at farms, bakers work at home
  - Debug villager selection (first villager or targeted job)

### VillageLot.cs
Represents a buildable lot section along a road in the village.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `VillageLot`
- **Enum**: `LotState` (Available, Occupied, Reserved)
- **Key Properties**:
  - `Id`: Unique lot identifier (auto-incremented)
  - `Position`: Top-left corner in grid coordinates
  - `Size`: Lot dimensions (default 10x10)
  - `State`: Current lot state (Available, Occupied, Reserved)
  - `AdjacentRoad`: Reference to the `RoadSegment` this lot borders
  - `RoadSide`: Which side of the lot faces the road (`CardinalDirection`)
  - `Setback`: Distance from road edge in tiles (default 1)
  - `OccupyingBuilding`: Building placed in this lot, if any
- **Key Methods**:
  - `GetBuildingPlacementPosition(buildingSize)`: Calculate centered position with setback
  - `CanFitBuilding(buildingSize)`: Check if building fits accounting for setback

### RoadSegment.cs
Represents a road segment extending from the village center.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `RoadSegment`
- **Enum**: `RoadDirection` (NorthSouth, EastWest)
- **Key Properties**:
  - `Start`: Starting point near village center
  - `End`: Ending point away from center
  - `Width`: Road width in tiles (default 2)
  - `Direction`: Computed from start/end positions
  - `LeftLots` / `RightLots`: Lots on each side of the road
  - `AllLots`: Combined enumerable of all adjacent lots
- **Key Methods**:
  - `GetRoadTiles()`: Yields all grid positions the road occupies
  - `Length`: Computed length in tiles

### RoadNetwork.cs
Manages the village road network and lot system. Creates a cross pattern of roads with dynamic lot sizing.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `RoadNetwork`
- **Constructor Parameters**:
  - `villageCenter`: Center point of the village
  - `villageSquareRadius`: Radius of central square (default 3 = 7x7 square)
  - `roadWidth`: Width of roads in tiles (default 2)
  - `lotSize`: Size of each lot (default 10)
  - `lotsPerSide`: Lots per side of each road arm (default 3)
- **Key Properties**:
  - `Roads`: List of all `RoadSegment` instances (4 total: N, S, E, W)
  - `AllLots`: Flattened list of all lots in the village
  - `LotSpacing`: Gap between consecutive lots along a road (default 1)
- **Key Methods**:
  - `GenerateLayout()`: Creates the cross pattern roads with lots (corner lots skipped)
  - `CalculateOptimalLotSize()`: Calculates lot size from largest building template + 2 tiles
  - `GetAvailableLot(randomize)`: Get next available lot
  - `GetAvailableLots()`: Get all available lots
  - `MarkLotOccupied(lot, building)`: Mark lot as occupied
  - `GetAllRoadTiles()`: Get all road tile positions
  - `GetVillageSquareTiles()`: Get village square tile positions

## Village Layout

The village uses a cross pattern road network extending from a central square:

```
              [Lot][Lot][Lot]
                   |N|
              [Lot]|o|[Lot]
                   |r|
              [Lot]|t|[Lot]
                   |h|
[Lot][Lot][Lot]====###====[Lot][Lot][Lot]
   West Road      #   #      East Road
[Lot][Lot][Lot]====###====[Lot][Lot][Lot]
                   |S|
              [Lot]|o|[Lot]
                   |u|
              [Lot]|t|[Lot]
                   |h|
              [Lot][Lot][Lot]
```

### Layout Specifications
- **Central Square**: 7x7 tiles (radius 3 from center)
- **Roads**: 2 tiles wide, extending from square edge
- **Lots**: Dynamically sized based on largest building template + 2 tiles (default 10x10)
- **Lot Spacing**: 1 tile gap between consecutive lots to prevent merging
- **Lots Per Road**: 3 per side, but first lot (i=0) on each side is skipped to prevent corner overlap
- **Total Lots**: 16 (4 roads x 4 lots each, minus corner lots = 16 net)
- **Building Setback**: 1 tile from road edge (buildings centered in lot)

### Building Placement Priority
1. **Required Buildings**: Multiple farms (formula: availableLots / 5, minimum 2), Graveyard (placed first in random lots)
2. **Houses**: Fill remaining lots with Simple Houses

### Farm Layout
- **Simple Farm**: Has two entrance gates (north and south) for multi-directional access
- **Farmer Distribution**: Farmers are distributed round-robin across all available farms

## Key Classes/Interfaces

| Class | Namespace | Description |
|-------|-----------|-------------|
| `GridGenerator` | `VeilOfAges.WorldGeneration` | Godot node, main generation entry point |
| `VillageGenerator` | `VeilOfAges.WorldGeneration` | Village layout and population generator |
| `VillageLot` | `VeilOfAges.WorldGeneration` | Buildable lot with state and building reference |
| `RoadSegment` | `VeilOfAges.WorldGeneration` | Road with lots on both sides |
| `RoadNetwork` | `VeilOfAges.WorldGeneration` | Cross pattern road and lot manager |

## Important Notes

### Generation Flow
```
World._Ready()
    -> GridGenerator.Generate() [deferred call]
        -> GenerateTerrain()
        -> VillageGenerator.GenerateVillage()
            -> RoadNetwork.GenerateLayout()
            -> PlaceVillageSquare()
            -> PlaceRoads()
            -> PlaceBuildingsInLots()
                -> PlaceBuildingInAvailableLot() [for required buildings]
                -> PlaceBuildingInLot() [fill remaining with houses]
            -> LogDebugVillagerSelection()
        -> GenerateTrees()
```

### Lot-Based Building Placement
1. `RoadNetwork` generates 16 lots in cross pattern around village center (corner lots skipped to prevent overlap)
2. Lot spacing of 1 tile is applied between consecutive lots to add buffer space
3. Lot size is dynamically calculated from the largest building template + 2 tiles
4. Required buildings placed first: Graveyard (1x), and multiple farms (qty = available lots / 5, minimum 2) in random available lots
5. Farmers are distributed round-robin across all available farms
6. Remaining lots filled with Simple Houses
7. Each building is centered in its lot with 1-tile setback from the road
8. Buildings that don't fit mark the lot as Reserved (skipped)
9. `VillageLot.GetBuildingPlacementPosition()` handles centering and setback

### Entity Spawning
- Characters spawn near their associated buildings
- Uses `FindPositionInFrontOfBuilding()` which prioritizes:
  1. Bottom (entrance) of building
  2. Right, Top, Left sides
  3. Expanding perimeter search (radius 2-3)
- All spawned `Being` entities are registered with `EntityThinkingSystem`
- Villagers are registered as village residents (receive shared knowledge)

### Job Assignment
- **Farmers**: Distributed round-robin across all available farms (multiple farms per village), live in houses
- **Bakers**: Work at their home (house serves as bakery)
- Jobs assigned via traits (`FarmerJobTrait`, `BakerJobTrait`)

### Building Types Currently Supported
- `"Simple Farm"` - Workplace for farmers with dual entrance (north and south gates)
- `"Graveyard"` - Home for undead, stocked with corpses
- `"Simple House"` - Home for villagers, spawns farmer + baker

### Random Number Generation
- `GridGenerator` uses `RandomNumberGenerator.Randomize()` for non-deterministic seeds
- `VillageGenerator` supports optional seed parameter for reproducible generation

### Performance Considerations
- Tree placement uses up to 3x `NumberOfTrees` attempts to handle collisions
- Lot system provides O(1) building placement (no collision search needed)
- `FindPositionForBuildingNear()` still exists for special cases (wiggle room search)

### Debug Villager Selection
Debug mode is enabled for one villager during generation:
- By default, the first spawned villager gets debug enabled
- Set `_debugTargetJob` to a job name (e.g., "baker") to target a specific job
- `LogDebugVillagerSelection()` logs which villager was selected
- The `_spawnedVillagers` list tracks all spawned villagers

### Known Quirks
- `GenerateDecorations()` method exists but is commented out
- Water pond position uses magic numbers for bounds (15, 25)
- `CardinalDirection` enum defined in `VillageLot.cs` (not a separate file)

## Dependencies

### This Module Depends On
- `VeilOfAges.Grid` - `Area`, `Utils` for grid operations
- `VeilOfAges.Entities` - `Being`, `Tree`, `Building` for entity instantiation
- `VeilOfAges.Entities.Items` - `Item`, `ItemResourceManager` for stocking buildings
- `VeilOfAges.Entities.Traits` - `FarmerJobTrait`, `BakerJobTrait`, `ZombieTrait`
- `VeilOfAges.Core` - `EntityThinkingSystem`, `BuildingManager`, `GameController`
- `VeilOfAges.Core.Lib` - `Log` for logging
- Godot types: `Node`, `PackedScene`, `RandomNumberGenerator`, `Vector2I`

### What Depends On This Module
- `VeilOfAges.World` - Calls `GridGenerator.Generate()` from `_Ready()`
