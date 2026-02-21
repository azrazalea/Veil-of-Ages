# World Generation Module

## Purpose

The `/world/generation` directory contains procedural generation systems for creating the game world. It handles terrain generation, village layout, building placement, and entity spawning. This module creates the initial world state when a new game begins.

## Files

### CellarGenerator.cs
Generates cellar areas beneath buildings and links them via transition points.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `CellarGenerator` (static)
- **Key Methods**:
  - `CreateCellar(world, overworldStampResult)`: Creates a cellar area beneath a building with trapdoor transition. Takes the `StampResult` of the overworld building (not a `Building` reference).
  - `RegisterCellarWithPlayer(world, cellar, trapdoor, ladder, cellarStampResult)` (private): Registers cellar knowledge via Room secrecy system
- **Key Features**:
  - Accepts `StampResult` (not `Building`) for the overworld building — finds the trapdoor decoration from `overworldStampResult.Decorations`
  - Stamps the cellar layout from `"Scholar's Cellar"` JSON template via `BuildingManager.Instance.StampBuilding()` (no programmatic tile placement)
  - Creates bidirectional transition points (trapdoor ↔ ladder)
  - Uses Room-based secrecy: gets the cellar `StampResult`'s default room, calls `Room.InitializeSecrecy()` to create a SharedKnowledge scope, then registers transitions/facilities in `Room.RoomKnowledge`
  - Player receives the room's SharedKnowledge via `Being.AddSharedKnowledge()` (permanent, not village knowledge — cellar is SECRET)
  - Sets up necromancy altar facility registration programmatically via `roomKnowledge.RegisterFacility()`
  - Handles non-walkable trapdoor positions by finding nearest walkable interior tile from `overworldStampResult.StructuralEntities`

### GridGenerator.cs
Main world generation orchestrator. Godot node that coordinates all generation phases.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `GridGenerator` (extends `Node`)
- **Exported Properties**:
  - `GenericBeingScene`: PackedScene reference for entity instancing
  - `NumberOfTrees`: Tree count target (default: 20)
  - **Note**: There is NO `BuildingScene` export. Buildings are instantiated entirely through `BuildingManager`/`TemplateStamper`, not via PackedScene.
- **Key Properties**:
  - `PlayerHouseStampResult`: The `StampResult` of the player's house, captured from `VillageGenerator` after generation and passed to `CellarGenerator`
- **Generation Phases**:
  1. `GenerateTerrain()`: Fills world with grass, adds dirt patches
  2. `_activeGridArea.PopulateLayersFromGrid()`: Populates visual tile layers before building placement
  3. `VillageGenerator.GenerateVillage()`: Places buildings via `TemplateStamper.Stamp()` and spawns villagers
  4. `CellarGenerator.CreateCellar(world, PlayerHouseStampResult)`: Creates cellar beneath player's house using the `StampResult`
  5. `GenerateTrees()`: Places trees in unoccupied walkable cells

### VillageGenerator.cs
Handles village layout generation using a lot-based system with road networks.

- **Namespace**: `VeilOfAges.WorldGeneration`
- **Class**: `VillageGenerator` (non-Godot, plain C# class)
- **Constructor Requirements**:
  - `Area gridArea`: Target grid area
  - `Node entitiesContainer`: Parent node for spawned entities
  - `EntityThinkingSystem`: For registering spawned beings
  - `Player? player`: Optional player reference for home assignment
  - `int? seed`: Optional seed for deterministic generation
  - **Note**: No `PackedScene` parameters. All buildings are placed via `BuildingManager.StampBuilding()`.
- **Key Fields**:
  - `_roadNetwork`: `RoadNetwork` instance managing lots and roads
  - `_placedFarms`: `List<Room>` tracking default rooms of placed farms (for farmer job assignment)
  - `_placedHouses`: `List<Room>` tracking default rooms of placed houses (for villager assignment)
  - `_placedGranaryResult`: `StampResult?` for the granary (used to attach `GranaryTrait` and spawn distributor)
  - `_placedGraveyard`: `Room?` tracking the graveyard's default room (for player house placement)
  - `_playerHouseRoom`: `Room?` the player's house room
  - `_lastStampResult`: Temporary `StampResult?` capture from the most recent `PlaceBuildingInLot()` call
  - `_spawnedVillagers`: `List<Being>` of all spawned villager entities for debug selection
  - `_currentVillage`: The `Village` being generated (tracks rooms and residents)
- **Key Public Properties**:
  - `PlayerHouseRoom`: The player's house `Room`, set during generation
  - `PlayerHouseStampResult`: The player's house `StampResult`, needed by `CellarGenerator`
- **Key Features**:
  - All building placement returns `StampResult` (not `Building`) via `BuildingManager.StampBuilding()`
  - Rooms from each `StampResult` are registered with the village via `Village.AddRoom()`
  - `GranaryTrait` is attached to the granary's storage `Facility` (not the room or building)
  - `VillageLot.OccupyingRoom` stores the default room of the building placed in that lot (replaces old `OccupyingBuilding`)
  - `RoadNetwork.MarkLotOccupied(lot, room)` takes a `Room?` (not a `Building`)
  - Village tracks `Room` instances via `_currentVillage.Rooms` (not `Building` instances)
  - Job assignment passes `Room` references for home/workplace (farm room, house room, granary room)
  - Debug selection is per-job-type: first villager of each job type gets debug enabled

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
  - `AdjacentRoad`: Reference to the `RoadSegment` this lot borders (null for corner lots)
  - `RoadSide`: Which side of the lot faces the road (`CardinalDirection`)
  - `Setback`: Distance from road edge in tiles (default 1)
  - `OccupyingRoom`: `Room?` — the default room of the building placed in this lot (replaces old `OccupyingBuilding`)
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
  - `MarkLotOccupied(lot, room)`: Mark lot as occupied, sets `lot.OccupyingRoom` to the provided `Room?`
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
- **Central Square**: 11x11 tiles (radius 5 from center)
- **Roads**: 3 tiles wide, extending from square edge
- **Lots**: Dynamically sized based on largest building template + 2 tiles (default 10x10)
- **Lot Spacing**: 1 tile gap between consecutive lots to prevent merging
- **Lots Per Road**: 4 per side
- **Total Lots**: Varies (4 roads x lots per side + corner lots)
- **Building Setback**: 1 tile from road edge (buildings centered in lot)

### Building Placement Priority
1. **Granary**: Placed in the corner lot nearest the center (uses `GetCornerLot()`)
2. **Farms**: Multiple farms (formula: availableLots / 5, minimum 2) in random available lots
3. **Graveyard**: Placed in the edge lot furthest from center (uses `GetEdgeLot()`)
4. **Player's House** (`"Scholar's House"`): Placed in the lot nearest the graveyard, no entities spawned
5. **Pond**: One oval water feature placed in a random available lot
6. **Well**: Placed at village center before lots are allocated (not in a lot)
7. **Houses**: Fill remaining lots with Simple Houses

### Farm Layout
- **Simple Farm**: Has two entrance gates (north and south) for multi-directional access
- **Farmer Distribution**: Farmers are distributed round-robin across all available farms

## Key Classes/Interfaces

| Class | Namespace | Description |
|-------|-----------|-------------|
| `GridGenerator` | `VeilOfAges.WorldGeneration` | Godot node, main generation entry point |
| `VillageGenerator` | `VeilOfAges.WorldGeneration` | Village layout and population generator |
| `VillageLot` | `VeilOfAges.WorldGeneration` | Buildable lot with state and Room reference |
| `RoadSegment` | `VeilOfAges.WorldGeneration` | Road with lots on both sides |
| `RoadNetwork` | `VeilOfAges.WorldGeneration` | Cross pattern road and lot manager |

## Important Notes

### Generation Flow
```
World._Ready()
    -> GridGenerator.Generate() [deferred call]
        -> GenerateTerrain()
        -> _activeGridArea.PopulateLayersFromGrid()
        -> VillageGenerator.GenerateVillage()
            -> RoadNetwork.GenerateLayout()
            -> PlaceVillageSquare()
            -> PlaceRoads()
            -> PlaceWellInVillageCenter()           [places Well via TemplateStamper.Stamp()]
            -> PlaceBuildingsInLots()
                -> PlaceBuildingInLot("Granary", cornerLot)   [StampResult returned]
                -> PlaceBuildingInAvailableLot("Simple Farm") [repeated for farmCount]
                -> PlaceBuildingInLot("Graveyard", edgeLot)   [StampResult returned]
                -> PlacePlayerHouseNearGraveyard()             [StampResult → PlayerHouseStampResult]
                -> PlacePondInAvailableLot()
                -> PlaceBuildingInLot("Simple House", lot)    [fill remaining]
                -> InitializeGranaryOrders()                  [GranaryTrait on storage Facility]
            -> LogDebugVillagerSelection()
        -> GridGenerator stores PlayerHouseStampResult from VillageGenerator
        -> CellarGenerator.CreateCellar(world, PlayerHouseStampResult)
            -> TemplateStamper.Stamp("Scholar's Cellar", ...)  [StampResult for cellar]
            -> Room.InitializeSecrecy()
            -> roomKnowledge.RegisterTransitionPoint() x2
            -> roomKnowledge.RegisterRoom()
            -> roomKnowledge.RegisterFacility("necromancy_altar", ...)
            -> player.AddSharedKnowledge(roomKnowledge)
        -> GenerateTrees()
```

### Building Placement (StampResult-Based)
1. All buildings are placed via `BuildingManager.StampBuilding(templateName, position, gridArea)` which internally calls `TemplateStamper.Stamp()`
2. `StampBuilding` returns a `StampResult` containing: `Rooms`, `StructuralEntities`, `Decorations`, `Origin`, `Size`, `TemplateName`, `GridArea`
3. The default room is retrieved via `stampResult.GetDefaultRoom()`
4. `RoadNetwork.MarkLotOccupied(lot, defaultRoom)` sets `lot.OccupyingRoom`
5. All rooms from the stamp result are registered with the village: `_currentVillage.AddRoom(room, _gridArea)`
6. Each storage facility is individually registered via `Knowledge.RegisterFacility()` for tag-based facility lookup
7. `GranaryTrait` is attached directly to the granary's storage `Facility` (retrieved via `granaryRoom.GetStorageFacility()`)

### Entity Spawning
- Characters spawn near their associated buildings using position derived from `StampResult.Origin` and `StampResult.Size`
- Uses `FindPositionInFrontOfBuilding()` which prioritizes:
  1. Bottom (entrance) of building
  2. Right, Top, Left sides
  3. Expanding perimeter search (radius 2-3)
- All spawned `Being` entities are registered with `EntityThinkingSystem`
- Villagers are registered as village residents (receive shared knowledge) via `Village.AddRoom()`
- Entities are created via `GenericBeing.CreateFromDefinition(definitionId, runtimeParams)` — no PackedScene instantiation
- Runtime parameters pass `Room` references: `"home"` and `"workplace"` (both are `Room?`)

### Job Assignment
- **Farmers**: Distributed round-robin across all available farm `Room` references, live in houses
- **Bakers**: Work at their home room (house serves as bakery)
- **Distributor**: Spawned near granary after all houses are placed; lives in first available house with capacity
- Jobs assigned via traits (`FarmerJobTrait`, `BakerJobTrait`, etc.) — configured with `Room` references at creation time

### Building Types Currently Supported
- `"Well"` - Placed at village center before lot allocation
- `"Granary"` - Placed in corner lot; has GranaryTrait attached to storage Facility; spawns distributor
- `"Simple Farm"` - Workplace for farmers with dual entrance (north and south gates)
- `"Graveyard"` - Stocked with corpses (no entities auto-spawned)
- `"Scholar's House"` - Player's home (no villagers spawned)
- `"Simple House"` - Home for villagers, spawns farmer + baker
- `"Scholar's Cellar"` - Generated by `CellarGenerator` from JSON template; not placed in a lot

### Random Number Generation
- `GridGenerator` uses `RandomNumberGenerator.Randomize()` for non-deterministic seeds
- `VillageGenerator` supports optional seed parameter for reproducible generation

### Performance Considerations
- Tree placement uses up to 3x `NumberOfTrees` attempts to handle collisions
- Lot system provides O(1) building placement (no collision search needed)
- `FindPositionForBuildingNear()` still exists for special cases (wiggle room search)

### Debug Villager Selection
Debug mode is enabled for one villager per job type during generation:
- First villager of each job type (farmer, baker, distributor, none) gets debug enabled
- Tracked via `_debugEnabledJobTypes` HashSet
- `LogDebugVillagerSelection()` logs which villager was selected
- The `_spawnedVillagers` list tracks all spawned villagers
- Distributor always gets `DebugEnabled = true` (hardcoded for behavior verification)

### Known Quirks
- `GenerateDecorations()` method exists but is commented out
- `CardinalDirection` enum defined in `VillageLot.cs` (not a separate file)
- `_lastStampResult` is a temporary field used to capture `PlayerHouseStampResult` from `PlaceBuildingInLot()` — it always holds the result of the most recent call

## Dependencies

### This Module Depends On
- `VeilOfAges.Grid` - `Area`, `Utils` for grid operations
- `VeilOfAges.Entities` - `Being`, `Tree`, `Room`, `StampResult` for entity and structure instantiation
- `VeilOfAges.Entities.Items` - `Item`, `ItemResourceManager` for stocking buildings
- `VeilOfAges.Entities.Traits` - `FarmerJobTrait`, `BakerJobTrait`, `GranaryTrait`, `HomeTrait`
- `VeilOfAges.Core` - `EntityThinkingSystem`, `BuildingManager`, `GameController`
- `VeilOfAges.Core.Lib` - `Log` for logging
- Godot types: `Node`, `PackedScene`, `RandomNumberGenerator`, `Vector2I`

### What Depends On This Module
- `VeilOfAges.World` - Calls `GridGenerator.Generate()` from `_Ready()`
