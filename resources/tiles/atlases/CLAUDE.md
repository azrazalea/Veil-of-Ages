# Tile Atlases Directory

## Purpose

Contains JSON definitions for sprite atlas sources. Each file defines a texture atlas that provides sprites for tiles, including the texture path, tile size, and spacing properties.

## Files

| File | ID | Description | Texture Source |
|------|-----|-------------|----------------|
| `kenney_1bit.json` | `kenney` | Primary building and terrain tiles | Kenney 1-Bit Colored Pack (2x upscaled) |
| `dcss_utumno.json` | `dcss` | Dungeon and environmental tiles | DCSS ProjectUtumno |
| `dcss_supplemental.json` | `dcss_supplemental` | Player doll overlays, extra sprites | DCSS Supplemental Atlas |
| `urizen_onebit.json` | `urizen` | UI elements and small props | Urizen OneBit V2 (upscaled to 32x32) |

## Atlas Pack Details

### Kenney 1-Bit Colored Pack
- **Atlas ID**: `kenney`
- **Original**: `colored-transparent_packed.png` (16x16 tiles)
- **Used Texture**: `colored-transparent_packed_2x.png` (32x32 tiles)
- **Margin/Separation**: None
- **Index File**: `assets/kenney/kenney_atlas_index.json`
- **Visual Reference**: `assets/kenney/kenney_groups/` contains pre-sliced category images

### DCSS ProjectUtumno (Main Atlas)
- **Atlas ID**: `dcss`
- **Texture**: `ProjectUtumno_full.png` (native 32x32 tiles)
- **Margin/Separation**: None
- **Index File**: `assets/dcss/dcss_utumno_index.json`

### DCSS Supplemental Atlas
- **Atlas ID**: `dcss_supplemental`
- **Texture**: `supplemental_atlas.png` (native 32x32 tiles)
- **Margin/Separation**: None
- **Index File**: `assets/dcss/dcss_supplemental_index.json`
- **Contents**: Player doll overlays (base bodies, clothing, cloaks, hair, headwear), additional sprites

**IMPORTANT**: Every individual PNG file under `assets/dcss/` (dungeon/, player/, etc.) is packed into one of these two atlases. The individual files exist ONLY for visual inspection — the game loads from atlas PNGs only.

### Urizen OneBit V2
- **Atlas ID**: `urizen`
- **Original**: `urizen_onebit_tileset__v2d0.png` (12x12 tiles, 1px margin/separation)
- **Used Texture**: `urizen_onebit_tileset__v2d0_32x32.png` (32x32 tiles, no margin/separation)
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
3. For any atlas with sub-32px tiles, `UseTexturePadding` is enabled to center them in 32x32 cells
4. Each atlas is assigned a unique source ID in the TileSet
5. Tile definitions reference atlases by `Id` (e.g., `"AtlasSource": "kenney"`)
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

### DCSS ProjectUtumno (Main)
- **TileSize**: `[32, 32]`
- **Separation**: `[0, 0]`
- **Margin**: `[0, 0]`
- **Upscaling**: None (native 32x32)

### DCSS Supplemental
- **TileSize**: `[32, 32]`
- **Separation**: `[0, 0]`
- **Margin**: `[0, 0]`
- **Upscaling**: None (native 32x32)

### Urizen OneBit
- **TileSize**: `[32, 32]`
- **Separation**: `[0, 0]`
- **Margin**: `[0, 0]`
- **Upscaling**: From 12x12 original to native 32x32

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
