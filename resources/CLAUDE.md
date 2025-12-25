# Resources Directory

## Purpose

This directory contains all JSON-based data files that define the game's content using a data-driven approach. These resources are loaded at runtime by various managers and define buildings, tiles, materials, and atlas sources.

## Directory Structure

```
resources/
├── buildings/
│   └── templates/          # Building layout templates (houses, farms, etc.)
└── tiles/
    ├── atlases/            # Sprite atlas source definitions
    ├── definitions/        # Tile type definitions (wall, floor, door, etc.)
    │   └── decoration/     # Variant files for decoration tiles
    └── materials/          # Material property definitions
```

## Files

This directory serves as the root for all game resources. It contains no JSON files directly but organizes subdirectories by resource type.

## How Resources Are Loaded

All tile-related resources are loaded by `TileResourceManager` (singleton) during initialization:

1. **Materials** are loaded first from `tiles/materials/`
2. **Atlas Sources** are loaded second from `tiles/atlases/`
3. **Tile Definitions** are loaded last from `tiles/definitions/` (with variant merging)

Building templates are loaded on-demand by `BuildingTemplate.LoadFromJson()`.

## Dependencies

- **TileResourceManager**: `entities/building/TileResourceManager.cs` - Loads tiles, atlases, materials
- **BuildingTemplate**: `entities/building/BuildingTemplate.cs` - Loads building templates
- **TileDefinition**: `entities/building/TileDefinition.cs` - Tile definition data structure
- **TileMaterialDefinition**: `entities/building/TileMaterialDefinition.cs` - Material data structure
- **TileAtlasSourceDefinition**: `entities/building/TileAtlasSourceDefinition.cs` - Atlas data structure

## Important Notes

- All JSON files use `PropertyNameCaseInsensitive = true` for parsing
- Vector2I values can be specified as arrays `[x, y]` or objects `{"X": x, "Y": y}`
- The variant system supports inheritance: base definition + variant files merged together
- Resource paths use Godot's `res://` prefix for texture references
