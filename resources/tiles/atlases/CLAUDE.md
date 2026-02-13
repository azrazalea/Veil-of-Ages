# Tile Atlases Directory

## Purpose

Contains JSON definitions for sprite atlas sources. Each file defines a texture atlas that provides sprites for tiles, including the texture path, tile size, and spacing properties.

## Files

| File | ID | Description | Texture Source |
|------|-----|-------------|----------------|
| `kenney_1bit.json` | `kenney_1bit` | Primary building and terrain tiles | Kenney 1-Bit Colored Pack (2x upscaled) |
| `dcss_utumno.json` | `dcss_utumno` | Dungeon and environmental tiles | DCSS ProjectUtumno |
| `urizen_onebit.json` | `urizen_onebit` | UI elements and small props | Urizen OneBit V2 (2x upscaled) |

## Atlas Pack Details

### Kenney 1-Bit Colored Pack
- **Atlas ID**: `kenney_1bit`
- **Original**: `colored-transparent_packed.png` (16x16 tiles)
- **Used Texture**: `colored-transparent_packed_2x.png` (32x32 tiles)
- **Margin/Separation**: None
- **Index File**: `assets/kenney/kenney_atlas_index.json`
- **Visual Reference**: `assets/kenney/kenney_groups/` contains pre-sliced category images

### DCSS ProjectUtumno
- **Atlas ID**: `dcss_utumno`
- **Texture**: `ProjectUtumno_full.png` (native 32x32 tiles)
- **Margin/Separation**: None
- **Index Files**: `assets/dcss/dcss_atlas_index.json` and `dcss_supplemental_index.json`
- **Extracted Sprites**: `assets/dcss/dungeon/` contains individual PNGs by category

### Urizen OneBit V2
- **Atlas ID**: `urizen_onebit`
- **Original**: `urizen_onebit_tileset__v2d0.png` (12x12 tiles, 1px margin/separation)
- **Used Texture**: `urizen_onebit_tileset__v2d0_2x.png` (24x24 tiles, 2px margin/separation)
- **Special Handling**: TileResourceManager enables `UseTexturePadding` to center 24x24 tiles in 32x32 cells
- **Index File**: `assets/urizen/urizen_atlas_index.json`

## JSON Schema

```json
{
  "Id": "string (required)",             // Unique identifier referenced by tile definitions
  "Name": "string (required)",           // Display name
  "Description": "string",               // Human-readable description
  "TexturePath": "res://path/to/texture.png",  // Godot resource path to the texture
  "TileSize": [32, 32],                  // Size of each tile in pixels [width, height]
  "Separation": [0, 0],                  // Pixels between tiles [x, y]
  "Margin": [0, 0],                      // Margin around the atlas [x, y]
  "Properties": {
    "Theme": "string",                   // Visual theme: Medieval, Gothic
    "Style": "string"                    // Style variant: Default, Cemetery
  }
}
```

## Atlas Index Files

Each atlas pack includes `*_atlas_index.json` files that map descriptive names to atlas positions:

```json
{
  "category/descriptive_name": {
    "row": 5,
    "col": 10
  }
}
```

These are developer reference files, NOT loaded by the game engine. Workflow:
1. Look up tile name in the index JSON
2. Note the row and col values
3. Use those as `AtlasCoords` in your tile definition JSON

## How Atlases Are Used

1. `TileResourceManager._Ready()` loads all atlas definitions at startup
2. `SetupTileSet()` creates `TileSetAtlasSource` objects from each definition
3. For Urizen atlas, `UseTexturePadding` is enabled to center 24x24 tiles in 32x32 cells
4. Each atlas is assigned a unique source ID in the TileSet
5. Tile definitions reference atlases by `Id` (e.g., `"AtlasSource": "kenney_1bit"`)
6. At runtime, `GetTileSetSourceId(atlasId)` maps the ID to the TileSet source ID

## Adding New Atlases

1. Add the texture file to `assets/` (use Godot's `res://` path format)
2. Create a new JSON file with a unique `Id`
3. Specify the correct `TileSize` matching your texture's grid
4. Set `Separation` and `Margin` if tiles have spacing/borders
5. Create an index JSON file mapping tile names to {row, col} positions
6. Reference the new atlas `Id` in tile definitions

## Current Configuration

### Kenney 1-Bit
- **TileSize**: `[32, 32]`
- **Separation**: `[0, 0]`
- **Margin**: `[0, 0]`
- **Upscaling**: 2x from 16x16 original

### DCSS ProjectUtumno
- **TileSize**: `[32, 32]`
- **Separation**: `[0, 0]`
- **Margin**: `[0, 0]`
- **Upscaling**: None (native 32x32)

### Urizen OneBit
- **TileSize**: `[24, 24]`
- **Separation**: `[2, 2]`
- **Margin**: `[2, 2]`
- **Upscaling**: 2x from 12x12 original
- **Special**: TileResourceManager automatically centers tiles in 32x32 cells

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
