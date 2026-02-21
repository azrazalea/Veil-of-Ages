# /entities/terrain/tree

## Purpose

This directory contains the Tree entity implementation. Trees are static terrain objects that occupy grid space and block pathfinding. They create their sprite programmatically — no scene file needed.

## Files

### Tree.cs
Static tree entity for world decoration and resources. Implements `IEntity<Trait>` for grid entity registration and sensory integration.

**Configuration:**
- `GridSize`: Vector2I(1, 1)
- `ZIndex`: 1 (set in Initialize)
- **Atlas Source**: Kenney 1-Bit Colored Pack (placeholder sprite at 0,0)

**Key Properties:**
- `IsWalkable` - Always false (trees block pathfinding)
- `Traits` - SortedSet of traits (currently empty)
- `GridArea` - Reference to the grid area this tree belongs to
- `DetectionDifficulties` - Per-sense detection difficulty values

**Lifecycle:**
- `Initialize(gridArea, gridPos)` - Sets position, creates Sprite2D from atlas, registers with grid via `GridArea.AddEntity()`
- `_ExitTree()` - Unregisters from grid area via `GridArea.RemoveEntity()`

**Sprite Creation:**
Uses `TileResourceManager.Instance.GetAtlasInfo("kenney")` to get the atlas texture, then creates an `AtlasTexture` region and adds a `Sprite2D` child. Currently uses a placeholder region at (0, 0) — pick an actual tree sprite from `kenney_atlas_index.json` later.

**Key Methods:**
- `GetCurrentGridPosition()` - Returns the tree's absolute grid position (ISensable implementation)
- `GetSensableType()` - Returns `SensableType.WorldObject` (ISensable implementation)
- `Interact()` - Static placeholder for player interaction (future: resource gathering)

## Important Notes

### No Scene File
GridGenerator creates Tree instances directly:
```csharp
var tree = new Entities.Terrain.Tree();
entitiesContainer.AddChild(tree);
tree.Initialize(gridArea, gridPos);
```

### Implements IEntity<Trait>
Trees implement `IEntity<Trait>` and are registered as grid entities. They block A* pathfinding through their occupied cells via `IsWalkable = false`.

## Dependencies

### Depends On
- `VeilOfAges.Grid.Area` - Grid registration
- `VeilOfAges.Grid.Utils` - Coordinate conversion
- `VeilOfAges.Entities.TileResourceManager` - Atlas texture access via `GetAtlasInfo()`

### Depended On By
- `/world/generation/GridGenerator.cs` - Creates tree instances
- Pathfinding (blocked cells)
