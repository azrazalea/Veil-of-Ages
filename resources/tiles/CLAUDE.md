# Tiles Directory

## Purpose

Contains all tile-related resource definitions organized into subdirectories for atlases, definitions, and materials. These resources work together to define how tiles look and behave in the game.

## Directory Structure

```
tiles/
├── atlases/        # Sprite atlas source definitions (textures)
├── definitions/    # Tile type definitions (wall, floor, etc.)
│   └── decoration/ # Variant files merged with decoration.json
└── materials/      # Material property definitions
```

## Loading Order

Resources are loaded by `TileResourceManager.Initialize()` in this order:
1. **Materials** - Must be loaded first (tiles reference materials)
2. **Atlas Sources** - Must be loaded before tiles (tiles reference atlases)
3. **Tile Definitions** - Loaded last, includes variant merging from subdirectories

## How They Work Together

1. **Atlas Sources** define where sprite textures are located and how to parse them
2. **Materials** define physical properties and modifiers for tiles
3. **Tile Definitions** combine atlas coordinates with material references and behavior properties

Example flow:
```
Building Template references: Type="Wall", Material="Wood", Variant="CornerTopLeft_Top"
    ↓
Tile Definition (wall.json): Finds variant "CornerTopLeft_Top" under "wood" material
    ↓
Atlas Source (buildings_main): Provides the actual sprite at AtlasCoords [4, 1]
    ↓
Material (wood.json): Applies DurabilityModifier=1.0, SensoryModifiers, etc.
```

## Dependencies

- **TileResourceManager**: `entities/building/TileResourceManager.cs` - Singleton that loads and manages all tile resources
- **TileDefinition**: `entities/building/TileDefinition.cs`
- **TileMaterialDefinition**: `entities/building/TileMaterialDefinition.cs`
- **TileAtlasSourceDefinition**: `entities/building/TileAtlasSourceDefinition.cs`

## Important Notes

- All `.json` files in each subdirectory are auto-loaded at initialization
- The variant system uses subdirectories matching base definition IDs (e.g., `decoration/` merges with `decoration.json`)
- Atlas IDs must match between tile definitions and atlas source files
