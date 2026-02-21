# Grid Systems Module

## Purpose

The `/world/grid_systems` directory contains specialized grid system implementations that extend the base `Grid.System<T>` class. Each system is typed for a specific kind of content stored in grid cells. These are lightweight wrappers that provide type safety for different grid layers.

## Files

### Ground.cs
Grid system specialized for terrain/ground tiles.

- **Namespace**: `VeilOfAges.Grid`
- **Class**: `GroundSystem`
- **Inherits**: `System<Tile>`
- **Purpose**: Stores `Tile` objects representing terrain (grass, dirt, water, paths)
- **Usage**: Used by `Area` for `_groundGridSystem` and `_objectGridSystem`

### Node2D.cs
Grid system specialized for Godot Node2D entities.

- **Namespace**: `VeilOfAges.Grid`
- **Class**: `Node2DSystem`
- **Inherits**: `System<Node2D>`
- **Purpose**: Tracks entity positions on the grid (buildings, beings, trees)
- **Usage**: Used by `Area` for `EntitiesGridSystem` (publicly exposed)

### Item.cs
Grid system specialized for item storage with metadata.

- **Namespace**: `VeilOfAges.Grid`
- **Class**: `ItemSystem`
- **Inherits**: `System<(int, object[])>`
- **Purpose**: Stores items with an integer ID and additional object array data
- **Usage**: Currently appears unused (TODO noted in `GridArea.cs`)

## Key Classes/Interfaces

| Class | Inherits | Cell Type | Description |
|-------|----------|-----------|-------------|
| `GroundSystem` | `System<Tile>` | `Tile` | Terrain layer storage |
| `Node2DSystem` | `System<Node2D>` | `Node2D` | Entity position tracking |
| `ItemSystem` | `System<(int, object[])>` | Tuple | Item storage with metadata |

## Important Notes

### Design Pattern
All three systems follow the same pattern:
- Primary constructor with `Vector2I? gridSize` parameter
- Inherit all functionality from `System<T>`
- No additional methods or overrides (except `Node2DSystem` — see below)

This pattern provides:
- Type safety for grid operations
- Consistent API across all grid layers
- Easy to add new specialized systems

### Inherited Capabilities (from System<T>)
- `GetCell(Vector2I)`: Returns `List<T>?` — all items at the cell (supports multiple entities per cell)
- `GetFirstCell(Vector2I)`: Returns the first item at the cell (`T?`), for grid systems that store one item per cell
- `SetCell(Vector2I, T)`: Set cell content (base class replaces; see Node2DSystem note below)
- `RemoveFromCell(Vector2I, T)`: Removes a specific item from the cell; removes the key entirely if the cell becomes empty
- `SetMultipleCellsOccupied(Vector2I, Vector2I, T)`: Multi-tile placement
- `IsCellOccupied(Vector2I)`: Check occupancy
- `FindNearestFreeCell(Vector2I, int)`: Find nearby empty cell
- `OccupiedCells`: `Dictionary<Vector2I, List<T>>` — each cell holds a list of items

### Node2DSystem: Multiple Entities Per Cell
`Node2DSystem` overrides `SetCell` to **append** the new entity to the cell's list rather than replacing it. This allows multiple entities (e.g. beings) to occupy the same grid cell simultaneously. The base `System<T>.SetCell` replaces the entire cell value and is appropriate for grid systems that store at most one item per cell (such as `GroundSystem`).

### Usage in Area
```csharp
// In GridArea.cs
private GroundSystem _groundGridSystem = new(worldSize);
private GroundSystem _objectGridSystem = new(worldSize);  // Also uses GroundSystem
public Node2DSystem EntitiesGridSystem { get; private set; } = new(worldSize);
```

### Item System Status
The `ItemSystem` class exists but has a TODO comment in `GridArea.cs`:
```csharp
// TODO: We need to properly implement items and make this
// a proper object that has a texture associated with it
```

This suggests the item system is planned but not yet integrated into the game.

## Dependencies

### This Module Depends On
- `VeilOfAges.Grid.System<T>` (from `GridSystem.cs`)
- `VeilOfAges.Grid.Tile` (from `Tile.cs`)
- Godot types: `Vector2I`, `Node2D`

### What Depends On This Module
- `VeilOfAges.Grid.Area` - Uses `GroundSystem` and `Node2DSystem` for layer management
