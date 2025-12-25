# /entities/terrain/tree

## Purpose

This directory contains the Tree entity implementation. Trees are static terrain objects that occupy grid space and can be interacted with (future: harvesting for wood).

## Files

### Tree.cs
Static tree entity for world decoration and resources.

**Configuration:**
- `GridSize`: Vector2I(5, 6) - Default size in tiles
- `ZIndex`: 1 - Rendering layer

**Key Properties:**
- `_gridPosition` - Position in grid coordinates
- `GridArea` - Reference to the containing area

**Lifecycle:**
- `Initialize(gridArea, gridPos)` - Set position and register
- `_Ready()` - Snap to grid position
- `_ExitTree()` - Unregister from grid

**Key Methods:**
- `Interact()` - Placeholder for player interaction
  - Currently just prints a message
  - Future: resource gathering, cutting

## Key Classes

| Class | Description |
|-------|-------------|
| `Tree` | Static vegetation terrain entity |

## Important Notes

### Grid Registration
Trees block their occupied cells:
```csharp
public override void _ExitTree()
{
    GridArea?.RemoveEntity(_gridPosition, GridSize);
}
```
Uses multi-cell removal based on GridSize.

### Positioning
World position is calculated from grid position:
```csharp
Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
```

### Future Features
The `Interact()` method is a placeholder for:
- Wood harvesting
- Fruit gathering
- Hiding/cover mechanics
- Environmental effects

### World Group Dependency
Ready method looks for World node:
```csharp
if (GetTree().GetFirstNodeInGroup("World") is not World world)
{
    GD.PrintErr("...");
    return;
}
```
Requires World to be in "World" group.

## Dependencies

### Depends On
- `VeilOfAges.Grid.Area` - Grid registration
- `VeilOfAges.Grid.Utils` - Coordinate conversion
- World node (via group lookup)

### Depended On By
- World generation systems
- Pathfinding (blocked cells)
- Future: Resource gathering systems
