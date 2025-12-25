# Tile Atlases Directory

## Purpose

Contains JSON definitions for sprite atlas sources. Each file defines a texture atlas that provides sprites for tiles, including the texture path, tile size, and spacing properties.

## Files

| File | ID | Description | Texture Source |
|------|-----|-------------|----------------|
| `buildings_main.json` | `buildings_main` | Primary building tiles (walls, floors, doors, windows) | Minifantasy Stucco Building tileset |
| `farm_main.json` | `farm_main` | Farm structure tiles (fences, gates) | Minifantasy Farm tileset |
| `farm_seeds_crops.json` | `farm_seeds_crops` | Crop and seed tiles | Minifantasy Farm Seeds/Crops |
| `graveyard_main.json` | `graveyard_main` | Graveyard tiles (stone walls, gates, floors) | Minifantasy Graveyard tileset |
| `graveyard_props.json` | `graveyard_props` | Graveyard decorations (tombstones, crosses) | Minifantasy Graveyard props |

## JSON Schema

```json
{
  "Id": "string (required)",             // Unique identifier referenced by tile definitions
  "Name": "string (required)",           // Display name
  "Description": "string",               // Human-readable description
  "TexturePath": "res://path/to/texture.png",  // Godot resource path to the texture
  "TileSize": [8, 8],                    // Size of each tile in pixels [width, height]
  "Separation": [0, 0],                  // Pixels between tiles [x, y]
  "Margin": [0, 0],                      // Margin around the atlas [x, y]
  "Properties": {
    "Theme": "string",                   // Visual theme: Medieval, Gothic
    "Style": "string"                    // Style variant: Default, Cemetery
  }
}
```

## How Atlases Are Used

1. `TileResourceManager` loads all atlas definitions at startup
2. `SetupTileSet()` creates `TileSetAtlasSource` objects from each definition
3. Each atlas is assigned a unique source ID in the TileSet
4. Tile definitions reference atlases by `Id` (e.g., `"AtlasSource": "buildings_main"`)
5. At runtime, `GetTileSetSourceId(atlasId)` maps the ID to the TileSet source ID

## Adding New Atlases

1. Add the texture file to `assets/` (use Godot's `res://` path format)
2. Create a new JSON file with a unique `Id`
3. Specify the correct `TileSize` matching your texture's grid
4. Set `Separation` and `Margin` if tiles have spacing/borders
5. Reference the new atlas `Id` in tile definitions

## Current Configuration

All atlases use:
- **TileSize**: `[8, 8]` pixels (Minifantasy standard)
- **Separation**: `[0, 0]` (no gaps)
- **Margin**: `[0, 0]` (no border)

## Dependencies

- **TileAtlasSourceDefinition**: `entities/building/TileAtlasSourceDefinition.cs`
- **TileResourceManager**: Loads atlases and creates TileSet sources
- **Vector2IConverter**: JSON converter for `[x, y]` arrays

## Validation Rules

`TileAtlasSourceDefinition.Validate()` checks:
1. `Id` is not null/empty
2. `Name` is not null/empty
3. `TexturePath` is not null/empty
4. `TileSize` has positive dimensions
