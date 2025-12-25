# World Module

## Purpose

The `/world` directory contains the core world management systems for Veil of Ages. It handles the game world structure, grid-based tile system, area management, pathfinding integration, and coordinate conversion utilities. This module serves as the foundation for all spatial operations in the game.

## Files

### World.cs
The main world container and entry point for world-related operations. Extends Godot's `Node2D`.

- **Namespace**: `VeilOfAges`
- **Key Responsibilities**:
  - Manages global world properties (seed, time scale, world size)
  - Contains the active `Grid.Area` and maintains a list of all grid areas
  - Holds references to core systems: `SensorySystem`, `EventSystem`, `GridGenerator`
  - Initializes and registers the player with the grid system
  - Provides `PrepareForTick()` for simulation tick preparation
  - Exposes `GetBeings()` to retrieve all `Being` entities in the world

### Tile.cs
Immutable data class representing a single tile's properties.

- **Namespace**: `VeilOfAges.Grid`
- **Properties**:
  - `SourceId`: TileSet source identifier
  - `AtlasCoords`: Position in the tile atlas
  - `IsWalkable`: Whether entities can traverse this tile
  - `WalkDifficulty`: Movement cost modifier (lower = easier)

### GridSystem.cs
Generic grid system for tracking cell occupancy with dictionary-based storage.

- **Namespace**: `VeilOfAges.Grid`
- **Class**: `System<T>` (generic)
- **Key Features**:
  - Dictionary-based cell storage (`OccupiedCells`)
  - Single and multi-cell occupancy operations
  - Nearest free cell search algorithm (expanding square pattern)
  - World-to-grid position conversion support
  - Debug visualization method

### GridUtils.cs
Static utility class for coordinate conversion between world and grid space.

- **Namespace**: `VeilOfAges.Grid`
- **Constants**:
  - `TileSize`: 8 pixels per tile
  - `WorldOffset`: 5 pixel visual offset correction
  - `WaterAtlasCoords`: Default water tile coordinates
- **Key Methods**:
  - `WorldToGrid()` / `GridToWorld()`: Coordinate conversion
  - `WorldPathToGridPath()` / `GridPathToWorldPath()`: Path conversion
  - `WithinProximityRangeOf()`: Distance checking with diagonal support

### GridArea.cs
Manages a discrete area of the game world with its own tile layers and entity tracking.

- **Namespace**: `VeilOfAges.Grid`
- **Class**: `Area` (extends `Node2D`)
- **Key Features**:
  - Owns ground and object `TileMapLayer` instances
  - Integrates with Godot's `AStarGrid2D` for pathfinding
  - Maintains separate grid systems for ground, objects, and entities
  - Static tile definitions: `WaterTile`, `GrassTile`, `DirtTile`, `PathTile`
  - Supports active/inactive states and player area designation
  - Entity management with automatic pathfinding solid updates

## Key Classes/Interfaces

| Class | Namespace | Description |
|-------|-----------|-------------|
| `World` | `VeilOfAges` | Main world container, Godot node |
| `Tile` | `VeilOfAges.Grid` | Immutable tile data record |
| `System<T>` | `VeilOfAges.Grid` | Generic grid occupancy tracker |
| `Utils` | `VeilOfAges.Grid` | Static coordinate utilities |
| `Area` | `VeilOfAges.Grid` | Discrete world area with tile layers |

## Important Notes

### Coordinate System
- **Tile Size**: 8x8 pixels
- **Visual Offset**: A 5-pixel Y offset exists between visual and logical positions. The `Utils` class handles this automatically.
- **Grid Origin**: Top-left corner (0,0)

### Pathfinding Integration
- `Area` creates and maintains an `AStarGrid2D` instance
- Solid points are updated automatically when:
  - Ground tiles are set (based on `IsWalkable`)
  - Entities are added/removed
- Weight scaling reflects terrain difficulty

### Performance Considerations
- Dictionary-based storage in `System<T>` - efficient for sparse grids
- `FindNearestFreeCell()` uses expanding square search - O(n) where n = search radius squared
- Avoid frequent `PopulateLayersFromGrid()` calls - iterates all cells

### Active Area System
- `_isActive`: Full detail mode with active AI
- `_isPlayerArea`: Currently player-occupied area
- Only player areas have enabled `TileMapLayer` rendering

## Dependencies

### This Module Depends On
- `VeilOfAges.Core.Lib` - `PathFinder` for A* grid creation
- `VeilOfAges.Entities` - `Being`, `Player` classes
- `VeilOfAges.Entities.Sensory` - `SensorySystem`
- `VeilOfAges.WorldGeneration` - `GridGenerator`
- Godot types: `Node2D`, `TileMapLayer`, `AStarGrid2D`, `Vector2I`

### What Depends On This Module
- `/world/generation/` - Uses `Area` for terrain and entity placement
- `/entities/` - Entities reference `Area` for movement and positioning
- `/core/` - `GameController` likely references `World` for game loop
