# /entities/building

## Purpose

This directory contains the building system for Veil of Ages. Buildings are complex structures composed of individual tiles, loaded from JSON templates, and integrated with the grid system. The system supports various building types (houses, farms, graveyards) with data-driven configuration.

## Files

### Building.cs
Main building entity class that implements `IEntity<Trait>`.

**Key Features:**
- TileMapLayer-based rendering with separate ground and structure layers
- Template-based initialization from JSON
- Grid system integration (walkable/blocked cells)
- Entrance position tracking
- Occupancy management (capacity, occupants)
- Damage system per tile

**Key Properties:**
- `BuildingType` - Category (House, Farm, Graveyard)
- `BuildingName` - Instance name
- `GridSize` - Size in tiles
- `CanEnter` - Whether entities can enter

**Interior Position Methods:**
- `GetInteriorPositions()` - Returns ALL tile positions from both `_tiles` (walls, doors, furniture) AND `_groundTiles` (floors), excluding entrance positions. Used for goal checking (e.g., `IsGoalReached`) to determine if an entity is inside the building bounds. Does NOT check current walkability.
- `GetWalkableInteriorPositions()` - Returns positions within the building bounds that are walkable according to the A* grid (`!IsPointSolid()`), excluding entrance positions. Checks terrain/building walkability only, NOT entity occupancy. This is intentional - entities should not have "god knowledge" of where other entities are standing. Dynamic entity collisions are handled by the blocking response system at runtime.
- `GetWalkableTiles()` - Legacy method returning tiles marked as inherently walkable in their definition. Prefer `GetWalkableInteriorPositions()` for pathfinding.

**Facility Methods:**
- `AddFacility(facility)` - Programmatically add a facility to this building (used for runtime facility addition)
- `GetFacilities()` - Returns all facility IDs available in this building (from `_facilityPositions.Keys`). Populated from the building template's `Facilities` array during initialization.
- `HasFacility(facilityId)` - Check if building has at least one instance of the specified facility.
- `GetFacilityPositions(facilityId)` - Get all relative positions for a given facility type.
- `GetAdjacentWalkablePosition(facilityPosition)` - Get a walkable position adjacent to a facility for entity positioning.
- `ContainsPosition(absolutePos)` - Check if given absolute grid position falls within building bounds
- `GetInteractableFacilityAt(absolutePos)` - Find interactable facility at given absolute position, returns `IFacilityInteractable` or null

**Storage Methods:**
- `GetStorage()` - Returns the `StorageTrait` for this building if it has one.
- `GetStorageAccessPosition()` - Returns the absolute grid position an entity should navigate to for storage access. If `RequireAdjacentToFacility` is true, returns a walkable position adjacent to the storage facility; otherwise returns the building entrance.
- `RequiresStorageFacilityNavigation()` - Returns true if navigation should target the storage facility position (i.e., storage has `RequireAdjacentToFacility = true` and a storage facility is defined).
- `IsAdjacentToStorageFacility(entityPosition)` - Check if an entity at the given position is adjacent to the storage facility.

### Facility.cs
Represents a named facility within a building (e.g., "oven", "storage", "altar").

**Key Properties:**
- `Id` - Facility type identifier (e.g., "oven", "corpse_pit")
- `Positions` - List of relative positions within the building
- `RequireAdjacent` - Whether entities must be adjacent to use this facility
- `Owner` - Reference to the building containing this facility
- `Traits` - List of traits attached to this facility
- `Interactable` - Optional `IFacilityInteractable` for player interaction
- `ActiveWorkOrder` - Currently active work order on this facility (if any)

**Key Methods:**
- `GetWorldPositions()` - Get absolute grid positions for all facility tiles
- `StartWorkOrder(order)` - Start a work order on this facility
- `CompleteWorkOrder()` - Complete and clear the active work order
- `CancelWorkOrder()` - Cancel the active work order (progress lost)

### IFacilityInteractable.cs
Interface for facilities that can be interacted with through dialogue.

**Key Types:**
- `FacilityDialogueOption`: Single interaction option with label, command, enabled state, and disabled reason
- `IFacilityInteractable`: Interface with `GetInteractionOptions(Being interactor)` and `FacilityDisplayName`

**Purpose:**
Facilities implement this to provide context-sensitive dialogue options to the player. Options can be disabled with explanatory tooltips.

**Implementors:**
- `NecromancyAltarInteraction` - Provides "Get Corpse" and "Raise Zombie" options with smart enabled/disabled logic

### facilities/ subdirectory
Contains facility interaction implementations.

**Files:**
- `NecromancyAltarInteraction.cs` - Interaction handler for necromancy_altar facility. Provides context-sensitive "Get Corpse" (checks night phase, altar storage, graveyard memory) and "Raise Zombie" (checks night phase, corpse presence, necromancy skill level, active work orders) options with detailed disabled reasons.

### BuildingManager.cs
Singleton manager for building templates and placement.

**Key Features:**
- Loads all templates from `res://resources/buildings/templates/`
- Template lookup by name
- Building placement with validation
- Space availability checking

**Key Methods:**
- `LoadAllTemplates()` - Load JSON templates
- `PlaceBuilding(templateName, position, area)` - Instantiate building
- `CanPlaceBuildingAt(template, position, area)` - Validate placement

### BuildingPlacementTool.cs
Interactive tool for placing buildings (marked for rewrite).

**Key Features:**
- Preview rendering with valid/invalid coloring
- Mouse-based position selection
- Escape key cancellation
- Placement callback delegate

### BuildingTemplate.cs
Data structure for JSON-serializable building templates.

**Key Properties:**
- `Name`, `Description`, `BuildingType`
- `Size` - Grid dimensions
- `Tiles` - List of `BuildingTileData`
- `Rooms` - Optional room definitions
- `EntrancePositions` - Door/gate locations
- `Capacity` - Occupant limit

**Includes:**
- `Vector2IConverter` for JSON serialization
- Validation logic for template integrity

### BuildingTile.cs
Individual tile within a building structure.

**Key Properties:**
- `Type` - TileType enum (Wall, Floor, Door, etc.)
- `Material` - Material name (wood, stone, metal)
- `Variant` - Visual variant name
- `IsWalkable`, `Durability`, `MaxDurability`
- `DetectionDifficulties` - Per-sense-type blocking values

**TileType Enum:**
Wall, Crop, Floor, Door, Window, Stairs, Roof, Column, Fence, Gate, Foundation, Furniture, Decoration, Well

### RoofSystem.cs
Handles roof visibility and fading based on player position.

**Key Features:**
- Layer-based roof modulation
- Visibility states (visible, fade, invisible)
- Template initialization support
- Placeholder for line-of-sight roof hiding

### TileAtlasSourceDefinition.cs
JSON-serializable atlas source configuration.

**Key Properties:**
- `Id`, `Name`, `Description`
- `TexturePath` - Path to texture file
- `TileSize`, `Separation`, `Margin`

**Includes:**
- `Rect2IJsonConverter` for JSON serialization

### TileDefinition.cs
JSON-serializable tile type definition.

**Key Properties:**
- `Id`, `Name`, `Type`, `Category`
- `DefaultMaterial`, `IsWalkable`, `BaseDurability`
- `AtlasSource`, `AtlasCoords` (legacy/fallback)
- `Categories` - Nested variant system

**Variant System:**
- Categories contain material-keyed variant dictionaries
- Variants specify atlas source and coordinates
- Supports inheritance/merging from base definitions

### TileMaterialDefinition.cs
JSON-serializable material definition.

**Key Properties:**
- `Id`, `Name`, `Description`
- `DurabilityModifier` - Multiplier for base durability
- `SensoryModifiers` - Per-sense detection modifiers

### TileResourceManager.cs
Singleton manager for all tile-related resources. Registered as a Godot autoload in `project.godot`.

**Key Features:**
- Godot Node autoload pattern (extends `Node`)
- Loads materials, atlases, and tile definitions from JSON on `_Ready()`
- Supports variant system with category/material/variant hierarchy
- TileSet setup for TileMapLayer nodes
- BuildingTile creation with full property merging

**Resource Paths:**
- Materials: `res://resources/tiles/materials/*.json`
- Atlases: `res://resources/tiles/atlases/*.json`
- Definitions: `res://resources/tiles/definitions/*.json`

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `Building` | Main building entity |
| `BuildingManager` | Template loading and placement |
| `BuildingPlacementTool` | Interactive placement (WIP) |
| `BuildingTemplate` | JSON template data structure |
| `BuildingTileData` | Template tile data |
| `BuildingTile` | Runtime tile instance |
| `RoofSystem` | Roof visibility management |
| `TileResourceManager` | Resource loading singleton |
| `TileDefinition` | Tile type definitions |
| `TileMaterialDefinition` | Material definitions |
| `TileAtlasSourceDefinition` | Atlas source definitions |
| `IFacilityInteractable` | Interface for facility dialogue interactions |
| `FacilityDialogueOption` | Single interaction option with enabled/disabled state |
| `NecromancyAltarInteraction` | Necromancy altar interaction handler |

## Important Notes

### Tile Resource System
Three-layer resource system:
1. **Atlas Sources** - Texture atlases with tile grids
2. **Materials** - Durability and sensory modifiers
3. **Tile Definitions** - Type with variant categories

Variant resolution order:
1. Base tile definition defaults
2. Category defaults
3. Material defaults
4. Specific variant overrides

### Building Initialization Flow
1. `BuildingManager.PlaceBuilding()` instantiates scene
2. `Building.Initialize()` receives template
3. `InitializeTileMaps()` sets up TileMapLayers
4. `CreateTilesFromTemplate()` creates BuildingTile instances
5. Each tile registered with grid system

### Detection Difficulties
Tiles affect perception based on type:
- Walls: Block sight (1.0), reduce hearing (0.5)
- Doors: Nearly block sight (0.9), reduce smell (0.5)
- Windows: Minor sight block (0.2), reduce hearing (0.6)
- Wells: Nearly block sight (0.9), reduce hearing (0.4), reduce smell (0.6)
- Floors: No blocking

Materials modify these values (stone blocks more sound, metal less smell).

### Pixel Offsets
Building tiles use constant offsets for alignment (zeroed out for 32x32 tiles):
```csharp
const int HORIZONTAL_PIXEL_OFFSET = 0;
const int VERTICAL_PIXEL_OFFSET = 0;
```

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Area and Utils
- `VeilOfAges.Entities.Sensory` - SenseType, ISensable
- Godot TileMap system
- System.Text.Json for serialization

### Depended On By
- Village generation systems
- Entity consumption behaviors (Farm, Graveyard lookup)
- Pathfinding (walkability checks)
