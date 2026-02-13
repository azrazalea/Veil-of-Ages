# /entities/terrain/tree

## Purpose

This directory contains the Tree entity implementation. Trees are static terrain objects that occupy grid space and block pathfinding. They create their sprite programmatically — no scene file needed.

## Files

### Tree.cs
Static tree entity for world decoration and resources.

**Configuration:**
- `GridSize`: Vector2I(1, 1)
- `ZIndex`: 1 (set in Initialize)
- **Atlas Source**: Kenney 1-Bit Colored Pack (placeholder sprite at 0,0)

**Lifecycle:**
- `Initialize(gridArea, gridPos)` - Sets position, creates Sprite2D from atlas, registers with grid
- `_ExitTree()` - Unregisters from grid area

**Sprite Creation:**
Uses `TileResourceManager.Instance.GetAtlasInfo("kenney_1bit")` to get the atlas texture, then creates an `AtlasTexture` region and adds a `Sprite2D` child. Currently uses a placeholder region at (0, 0) — pick an actual tree sprite from `kenney_atlas_index.json` later.

**Key Methods:**
- `Interact()` - Static placeholder for player interaction (future: resource gathering)

## Important Notes

### No Scene File
GridGenerator creates Tree instances directly:
```csharp
var tree = new Entities.Terrain.Tree();
entitiesContainer.AddChild(tree);
tree.Initialize(gridArea, gridPos);
```

### Implements IBlocksPathfinding
Trees block A* pathfinding through their occupied cells.

## Dependencies

### Depends On
- `VeilOfAges.Grid.Area` - Grid registration
- `VeilOfAges.Grid.Utils` - Coordinate conversion
- `VeilOfAges.Entities.TileResourceManager` - Atlas texture access via `GetAtlasInfo()`

### Depended On By
- `/world/generation/GridGenerator.cs` - Creates tree instances
- Pathfinding (blocked cells)
