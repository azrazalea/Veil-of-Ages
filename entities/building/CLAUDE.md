# /entities/building

## Purpose

This directory contains the building system for Veil of Ages. Buildings are complex structures composed of individual tile entities, facilities, and decorations, loaded from JSON templates and stamped into a GridArea. The system uses a flat entity hierarchy (no Building parent node) — each structural tile is its own Sprite2D node, and all nodes are children of GridArea.

## Files

### TileType.cs
Enum defining the functional type of a structural tile. Namespace `VeilOfAges.Entities`.

**Enum Values:**
Wall, Crop, Floor, Door, Window, Stairs, Roof, Column, Fence, Gate, Foundation, Furniture, Decoration, Well

Used by `StructuralEntity` to determine walkability, room boundary behavior, and rendering Z-index.

### StructuralEntity.cs
An individual structural tile element (wall, floor, door, fence, etc.) rendered as a `Sprite2D`. Implements `IEntity<Trait>`. Replaces the TileMapLayer-based rendering from the old Building system. Each structural tile is its own scene tree node, enabling per-tile damage, removal, and future modifications.

**Key Properties:**
- `Type` (TileType) - Functional type of this tile
- `Material` (string) - Material name (e.g., "wood", "stone")
- `Variant` (string) - Visual variant name
- `GridPosition` (Vector2I) - Absolute grid position
- `GridArea` (Grid.Area?) - Reference to the containing grid area
- `IsWalkable` (bool) - Whether entities can walk through this tile. True for floors, doors, gates. False for walls, fences, columns.
- `IsRoomBoundary` (bool) - Whether this tile blocks flood fill for room detection. True for Wall, Fence, Window, Column.
- `IsRoomDivider` (bool) - Whether this tile divides rooms. True for Door, Gate. Also blocks flood fill, but the tile is assigned to adjacent rooms rather than a single room.
- `Durability` / `MaxDurability` (int) - Current and maximum durability
- `DetectionDifficulties` (Dictionary<SenseType, float>) - Per-sense detection difficulty values
- `AtlasCoords` / `SourceId` - Visual data for sprite reconstruction
- `TintColor` (Color?) - Optional tint applied via Modulate

**Key Methods:**
- `InitializeVisual(texture)` - Set up sprite, Z-index, and tint. Must be called before adding to scene tree.
- `TakeDamage(amount)` - Apply damage; returns true if destroyed (durability reached 0)
- `Repair(amount)` - Restore durability up to MaxDurability
- `GetConditionPercentage()` - Current durability as 0.0–1.0 fraction
- `GetCurrentGridPosition()` - ISensable implementation; returns GridPosition
- `GetSensableType()` - Returns `SensableType.WorldObject`
- `_ExitTree()` - Auto-unregisters from GridArea when removed from scene tree (for non-walkable entities)

**Z-Index:**
- Floor tiles: ZIndex = 2
- Wall/door/other tiles: ZIndex = 3
- Facilities and decorations: ZIndex = 4 (above structural)

### StructuralEntityFactory.cs
Static factory that creates `StructuralEntity` instances from tile definition data. Wraps `TileResourceManager` lookup to resolve definitions, materials, variants, atlas coordinates, and tint.

**Key Methods:**
- `Create(tileData, buildingOrigin)` - Create a StructuralEntity from a `BuildingTileData` template entry. Uses `Category` as definition ID when available, falls back to `Type`. Computes absolute grid position from `buildingOrigin + tileData.Position`.
- `CreateFromDefinition(tileDefId, materialId, variantName, absoluteGridPosition, tintOverride)` - Create from raw parameters. Performs full variant resolution, durability calculation, detection difficulty merge, and tint cascade (per-tile override > variant tint > definition default tint). Calls `entity.InitializeVisual()` before returning.

Created entities are not yet added to the scene tree — the caller (`TemplateStamper`) adds them as children of GridArea.

### TemplateStamper.cs
Static class that stamps a `BuildingTemplate` into a `GridArea`, creating all structural entities, facilities, and decorations as a flat node hierarchy (all children of GridArea). Replaces `Building.Initialize()`.

**Key Method:**
- `Stamp(template, gridPosition, area)` - Creates all entities and returns a `StampResult`. Does NOT run room detection — the caller must invoke `RoomSystem.DetectRoomsInRegion()` after stamping.

**Stamping Order:**
1. Compute entrance positions (absolute = origin + relative)
2. Create facilities: absolute positions, StorageTrait if configured, interaction handler via reflection, visual sprite if `DecorationId` is set. Track facility-owned decoration IDs to avoid duplicates.
3. Create structural entities via `StructuralEntityFactory.Create()`. Track door/gate positions in `StampResult.DoorPositions`.
4. Register structural entities with GridArea: walkable tiles get `SetGroundWalkability(true)`, non-walkable tiles get `AddEntity()` to mark them solid. All added as children via `AddChild()`.
5. Register facilities with GridArea: `AddChild()` then `AddEntity()` for each position. Done after structural so walkability is not overwritten.
6. Create decorations, skipping facility-owned ones. Override pixel position to absolute (parent is GridArea at 0,0). `AddChild()` then `AddEntity()` for each position.

**Interactable Creation:**
- `CreateFacilityInteractable(typeName, facility)` (private) - Finds type by name via reflection across all assemblies, instantiates with `(Facility facility)` constructor. Same pattern as the old `Building.CreateFacilityInteractable()`.

**Note on Transition Points:**
Templates may define transition points (e.g., cellar stairs) in metadata. These are NOT wired up by `TemplateStamper` — the caller (generator) handles them using the data in `StampResult`.

### StampResult.cs
Return value from `TemplateStamper.Stamp()`. Plain C# class. Contains all entities created by stamping, plus room data populated after room detection.

**Key Properties:**
- `TemplateName` (string) - Template name that was stamped
- `BuildingType` (string) - Building type from template (e.g., "House", "Farm")
- `Capacity` (int) - Capacity from template
- `Origin` (Vector2I) - Absolute grid top-left where template was stamped
- `Size` (Vector2I) - Template size in tiles
- `GridArea` (Grid.Area) - Grid area this stamp was placed in
- `StructuralEntities` (List<StructuralEntity>) - All wall/floor/door/etc. entities
- `Facilities` (List<Facility>) - All facility entities
- `Decorations` (List<Decoration>) - All decoration entities
- `Rooms` (List<Room>) - Populated by RoomSystem after stamping
- `DoorPositions` (List<Vector2I>) - Absolute positions of door/gate entities
- `EntrancePositions` (List<Vector2I>) - Entrance positions from template (absolute)

**Key Methods:**
- `GetDefaultRoom()` - Returns the first room, or null if no rooms detected
- `GetRoomAtPosition(absolutePos)` - Returns the room containing the given absolute grid position, or null

### Room.cs
Lightweight organizational class grouping tile positions, facilities, decorations, and residents within a building. Plain C# class — NOT a Godot node. Created by `RoomSystem.DetectRoomsInRegion()` via flood fill of walkable interior positions. Template `RoomData` provides optional hints (Name, Purpose, IsSecret) matched by bounding box overlap.

Tiles are stored as absolute positions.

**Key Properties:**
- `Id` - Unique identifier within the building (auto-generated)
- `Name` - Human-readable name (from template hint or auto-generated)
- `Purpose` - Room purpose string (e.g., "Living", "Workshop", "Storage")
- `IsSecret` - Whether this room is secret. Secret rooms create their own `SharedKnowledge` scope; facilities are registered there instead of village knowledge.
- `Type` - Room type string (from template BuildingType — "House", "Farm", etc.)
- `IsDestroyed` (bool) - Validity flag. Set to true when the room is destroyed (e.g., walls removed). Used since Room is a plain C# class, not a GodotObject.
- `IsEnclosed` (bool) - Whether this room is fully enclosed by walls/fences/doors. False for outdoor areas or rooms with missing walls.
- `Walls` (List<StructuralEntity>) - Wall/fence/window/column entities forming the boundary
- `Floors` (List<StructuralEntity>) - Floor entities inside the room
- `Doors` (List<StructuralEntity>) - Door/gate entities on the boundary
- `GridArea` (Grid.Area?) - Reference to the grid area
- `Tiles` (IReadOnlySet<Vector2I>) - Interior tile positions (absolute)
- `Facilities` (IReadOnlyList<Facility>) - Facilities in this room
- `Decorations` (IReadOnlyList<Decoration>) - Decorations in this room
- `Residents` (IReadOnlyList<Being>) - Beings assigned to this room
- `RoomKnowledge` (SharedKnowledge?) - Non-null only for secret rooms
- `Capacity` - Max residents (0 = unlimited)

**Resident Methods:**
- `AddResident(being)` - Add a resident (respects capacity)
- `RemoveResident(being)` - Remove a resident
- `HasResident(being)` - Check if a being is a resident

**Facility Lookup Methods:**
- `GetFacility(facilityId)` - Get the first facility matching the given ID, or null
- `GetFacilities(facilityId)` - Get all facilities matching the given ID (returns list, may be empty)
- `HasFacility(facilityId)` - Check if this room has at least one facility with the given ID
- `GetStorageFacility()` - Get the primary storage facility: first tries facility with id "storage", then any facility with `StorageTrait`
- `GetStorage()` - Convenience: returns `StorageTrait` from the storage facility, or null
- `GetInteractableFacilityAt(absolutePos)` - Find an interactable facility at the given absolute grid position, returns `IFacilityInteractable` or null

**Internal Methods:**
- `AddFacility(facility)` - Register a facility in this room and set `facility.ContainingRoom = this`
- `AddDecoration(decoration)` - Register a decoration in this room
- `ContainsAbsolutePosition(absolutePos)` - Check if absolute grid position is in this room
- `InitializeSecrecy(knowledgeId, knowledgeName)` - Initialize as a secret room with its own SharedKnowledge scope

**Secret Room Pattern:**
Secret rooms create their own `SharedKnowledge` scope via `InitializeSecrecy()`. Facilities in secret rooms are registered in this scope instead of village knowledge. Authorized entities receive this knowledge via `Being.AddSharedKnowledge()`. Used by the cellar system to keep necromancy facilities hidden from villagers.

### Facility.cs
Extends `Sprite2D` and implements `IEntity<Trait>`. Represents a functional facility within a building (e.g., "oven", "storage", "altar"). Owns its own sprite and can block walkability. All positions are absolute grid coordinates. Registered as a grid entity via `GridArea.AddEntity()`.

**Key Properties:**
- `Id` - Facility type identifier (e.g., "oven", "corpse_pit")
- `Positions` (List<Vector2I>) - Absolute grid positions this facility occupies
- `RequireAdjacent` - Whether entities must be adjacent to use this facility
- `ContainingRoom` (Room?) - The room this facility belongs to. Set when `Room.AddFacility()` is called. Replaces the old `Owner` (Building?) property.
- `GridPosition` (Vector2I) - Absolute grid position of the primary tile
- `IsWalkable` - Whether entities can walk through this facility's tiles (default true)
- `GridArea` - Reference to grid area
- `Traits` - SortedSet of traits attached to this facility
- `DetectionDifficulties` - Per-sense detection difficulty values
- `Interactable` (IFacilityInteractable?) - Optional interaction handler for player dialogue
- `ActiveWorkOrder` (WorkOrder?) - Currently active work order on this facility

**Visual Methods:**
- `InitializeVisual(definition, gridPosition, pixelOffset)` - Set up sprite from a DecorationDefinition (atlas or animated)

**Other Methods:**
- `GetAbsolutePositions()` - Returns all absolute positions this facility occupies
- `GetCurrentGridPosition()` - Returns GridPosition (ISensable implementation)
- `GetSensableType()` - Returns `SensableType.WorldObject` (ISensable implementation)
- `SelfAsEntity()` - Returns this as `IEntity<Trait>` for default interface method access
- `SetGridPosition(absolutePosition)` (internal) - Called during stamping to set the primary tile position
- `StartWorkOrder(order)` - Start a work order on this facility
- `CompleteWorkOrder()` - Complete and clear the active work order
- `CancelWorkOrder()` - Cancel the active work order (progress lost)

### Decoration.cs
Extends `Sprite2D` and implements `IEntity<Trait>`. A decoration sprite placed in a building. Can optionally block walkability for non-interactive props like tombstones. Registered as a grid entity via `GridArea.AddEntity()`.

**Key Properties:**
- `DecorationId` - The decoration definition ID
- `AbsoluteGridPosition` - Absolute grid position of this decoration
- `GridPosition` - Relative position within the building
- `IsWalkable` - Whether entities can walk through (default true)
- `AllPositions` - All relative positions this decoration occupies (primary + additional)
- `GridArea` - Reference to grid area
- `Traits` - SortedSet of traits attached to this decoration
- `DetectionDifficulties` - Per-sense detection difficulty values

**Key Methods:**
- `Initialize(definition, gridPosition, pixelOffset, isWalkable, additionalPositions)` - Set up sprite and position
- `GetCurrentGridPosition()` - Returns the absolute grid position (ISensable implementation)
- `GetSensableType()` - Returns `SensableType.WorldObject` (ISensable implementation)

### DecorationDefinition.cs
Data class for decoration sprite definitions loaded from JSON.

**Key Properties:**
- `Id` - Unique identifier
- `AtlasSource` - Atlas source ID for static decorations
- `AtlasCoords` - Column/row in atlas grid (X=col, Y=row)
- `TileSize` - Size in tiles (default 1x1)
- `AnimationId` - References SpriteAnimationDefinition for animated decorations

### IFacilityInteractable.cs
Interface for facilities that can be interacted with through dialogue.

**Key Types:**
- `FacilityDialogueOption`: Single interaction option with label, command, enabled state, and disabled reason
- `IFacilityInteractable`: Interface with `GetInteractionOptions(Being interactor)` and `FacilityDisplayName`

**Purpose:**
Facilities implement this to provide context-sensitive dialogue options to the player. Options can be disabled with explanatory tooltips.

**Implementors:**
- `NecromancyAltarInteraction` - Provides "Get Corpse" and "Raise Zombie" options with smart enabled/disabled logic. Constructor takes `(Facility facility)`. Uses `_facility.SelfAsEntity().GetTrait<StorageTrait>()` for storage access.

### facilities/ subdirectory
Contains facility interaction implementations.

**Files:**
- `NecromancyAltarInteraction.cs` - Interaction handler for necromancy_altar facility. Constructor takes `(Facility facility)`. Provides context-sensitive "Get Corpse" (checks night phase, altar storage via `_facility.SelfAsEntity().GetTrait<StorageTrait>()`, graveyard memory) and "Raise Zombie" (checks night phase, corpse presence, necromancy skill level, active work orders) options with detailed disabled reasons.

### GridBuildingTemplateLoader.cs
Static class that loads building templates from the directory-based **GridFab** format. Converts visual grid files into standard `BuildingTemplate` objects consumed by `BuildingManager`.

**Key Method:**
- `LoadFromDirectory(dirPath, palettesBasePath)` - Reads `building.json`, resolves the palette chain, parses all `*.grid` files, and returns a fully assembled `BuildingTemplate`.

**How It Works:**
1. Loads `building.json` for metadata (Name, Description, BuildingType, Size, EntrancePositions, etc.)
2. Loads `palette.json` from the template directory; if it declares `"Inherits"`, recursively loads the named shared palette from `palettesBasePath/` first, then overlays template-local aliases on top
3. Parses each `*.grid` file: each row of space-separated aliases maps to a tile row (row 0 = top of building); the grid filename (without extension) becomes the layer name (e.g., `structure`, `floor`, `ground`)
4. Alias `.` is always treated as empty (no tile placed); all other aliases resolve through the palette to `(TileType, Material)` pairs
5. Assembles all parsed tiles across all layers into the `BuildingTemplate.Tiles` list

**Private Helper Types:**
- `PaletteFile` — JSON-deserialized shape of a `palette.json` file (`Inherits` string, `Tiles` dictionary)
- `PaletteEntry` — Single palette mapping (`Type` string, `Material` string)

### BuildingManager.cs
Singleton manager for building templates and placement.

**Key Features:**
- Loads all templates from `res://resources/buildings/templates/`
- Scans for both subdirectory (GridFab format) and legacy `.json` files; GridFab directories take priority when a name collision occurs
- Template lookup by name
- Building placement via `TemplateStamper.Stamp()` with validation
- Space availability checking

**Key Fields:**
- `_palettesPath` - Path to the shared palettes directory (`res://resources/buildings/palettes/`)

**Key Methods:**
- `LoadAllTemplates()` - Scans for GridFab subdirectories first, then legacy `.json` files; delegates GridFab loading to `GridBuildingTemplateLoader`
- `PlaceBuilding(templateName, position, area)` - Stamp template into area via `TemplateStamper`
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
- `Rooms` - Optional room definitions (list of `RoomData` with Name, Purpose, TopLeft, Size, IsSecret, Properties)
- `EntrancePositions` - Door/gate locations
- `Capacity` - Occupant limit

**Includes:**
- `Vector2IConverter` for JSON serialization
- Validation logic for template integrity

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
- `DefaultTint` - Optional hex color applied to all tiles of this definition
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
- Caches `AtlasTexture` instances for reuse across `StructuralEntityFactory`
- Provides `GetProcessedVariant()` static method for variant resolution

**Resource Paths:**
- Materials: `res://resources/tiles/materials/*.json`
- Atlases: `res://resources/tiles/atlases/*.json`
- Definitions: `res://resources/tiles/definitions/*.json`

**Key Methods (used by StructuralEntityFactory):**
- `GetTileDefinition(id)` - Look up a tile definition by ID
- `GetMaterial(id)` - Look up a material definition by ID
- `GetTileSetSourceId(atlasSourceName)` - Resolve atlas source name to integer ID
- `GetCachedAtlasTexture(atlasSource, row, col)` - Get or create a cached AtlasTexture
- `GetProcessedVariant(tileDef, materialId, variantName)` (static) - Resolve the full variant hierarchy, returns dictionary with AtlasSource, AtlasCoords, and optional Tint keys

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `TileType` | Enum of structural tile functional types |
| `StructuralEntity` | Individual tile entity (Sprite2D + IEntity) |
| `StructuralEntityFactory` | Creates StructuralEntity from template tile data |
| `TemplateStamper` | Stamps BuildingTemplate into GridArea (replaces Building.Initialize) |
| `StampResult` | Return value from stamping: entities, rooms, door positions |
| `Room` | Lightweight organizational unit for tiles/facilities/residents |
| `Facility` | Functional facility within a building (Sprite2D + IEntity) |
| `Decoration` | Decorative sprite entity (Sprite2D + IEntity) |
| `BuildingManager` | Template loading and placement |
| `BuildingPlacementTool` | Interactive placement (WIP) |
| `BuildingTemplate` | JSON template data structure |
| `GridBuildingTemplateLoader` | Loads GridFab format templates |
| `TileResourceManager` | Resource loading singleton |
| `TileDefinition` | Tile type definitions |
| `TileMaterialDefinition` | Material definitions |
| `TileAtlasSourceDefinition` | Atlas source definitions |
| `DecorationDefinition` | Decoration sprite definitions |
| `IFacilityInteractable` | Interface for facility dialogue interactions |
| `FacilityDialogueOption` | Single interaction option with enabled/disabled state |
| `NecromancyAltarInteraction` | Necromancy altar interaction handler |

## Important Notes

### Architecture: Flat Entity Hierarchy (Phase 5C)
The old `Building` class and `TileMapLayer`-based rendering have been removed. The new architecture is:
- `TemplateStamper.Stamp()` creates all entities as direct children of `GridArea`
- `StructuralEntity` — each tile is an independent `Sprite2D` node
- `Facility` — functional entities with their own `Sprite2D`
- `Decoration` — decorative sprites with optional walkability blocking
- `Room` — plain C# organizational grouping (no Godot node)
- `StampResult` — flat data bag returned to the caller

There is no Building node in the scene tree. Buildings are implicit: a group of structural entities, facilities, and decorations that share an origin and belong to rooms in a `StampResult`.

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

### Stamping and Room Detection Flow
1. `BuildingManager.PlaceBuilding()` calls `TemplateStamper.Stamp()`
2. `TemplateStamper.Stamp()` creates all entities and registers them with GridArea
3. Caller invokes `RoomSystem.DetectRoomsInRegion()` to populate `StampResult.Rooms`
4. Room detection flood-fills walkable interior positions to create `Room` objects
5. Template `RoomData` hints are matched by bounding box overlap for naming/purpose/secrecy
6. Facilities and decorations are assigned to rooms via `Room.AddFacility()` / `Room.AddDecoration()`

### Detection Difficulties
Tiles affect perception based on type:
- Walls: Block sight (1.0), reduce hearing (0.5)
- Doors: Nearly block sight (0.9), reduce smell (0.5)
- Windows: Minor sight block (0.2), reduce hearing (0.6)
- Wells: Nearly block sight (0.9), reduce hearing (0.4), reduce smell (0.6)
- Floors: No blocking

Materials modify these values (stone blocks more sound, metal less smell).

### Facility Positions
After Phase 5C, all facility positions are absolute grid coordinates. There are no relative positions stored on Facility. The `GetAbsolutePositions()` method returns `Positions` directly (no offset calculation needed).

### Room.ContainsAbsolutePosition
`Room.Tiles` stores absolute positions. `ContainsAbsolutePosition()` does a direct HashSet lookup. There is no `ContainsRelativePosition()` — all callers must use absolute coordinates.

### GranaryTrait
`GranaryTrait` is attached to a `Facility`, not to any Building class. This follows the general pattern that behavioral traits live on `Facility` or `Being`, not on structural containers.

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Area, Utils (TileSize), pathfinding integration
- `VeilOfAges.Entities.Sensory` - SenseType, ISensable, SensableType
- `VeilOfAges.Entities.Memory` - SharedKnowledge (for secret rooms)
- `VeilOfAges.Entities.Traits` - StorageTrait, GranaryTrait, etc.
- `VeilOfAges.Entities.Items` - ItemResourceManager, Item (for initial storage contents)
- Godot Sprite2D / AnimatedSprite2D / AtlasTexture
- System.Text.Json for serialization

### Depended On By
- Village generation systems (stamp templates, detect rooms, wire transitions)
- Entity AI behaviors (Room facility lookup, storage access)
- Pathfinding (walkability via StructuralEntity.IsWalkable and GridArea registration)
- Knowledge systems (Room.RoomKnowledge for secret room secrecy)
