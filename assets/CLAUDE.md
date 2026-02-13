# Assets Directory

## Purpose

Contains all game assets including sprites, fonts, and textures. The primary visual assets come from three open/free-license atlas packs. All three atlases have been upscaled to provide crisp 32x32 pixel tiles for the game's grid system.

## Structure

```
assets/
├── fonts/
│   └── slimes_pixel_font_pack/       # Pixel fonts for UI
├── kenney/                           # Kenney 1-Bit Colored Pack
│   ├── colored-transparent_packed.png        # Original 16x16
│   ├── colored-transparent_packed_2x.png     # 2x upscaled (32x32) - USED BY GAME
│   ├── kenney_atlas_index.json              # Row/col position reference
│   └── kenney_groups/                       # Pre-sliced category images
├── dcss/                             # DCSS ProjectUtumno
│   ├── ProjectUtumno_full.png               # Full 32x32 atlas - USED BY GAME
│   ├── dcss_atlas_index.json                # Row/col position reference
│   ├── dcss_supplemental_index.json         # Additional sprite mappings
│   ├── LICENSE.txt
│   └── dungeon/                             # Extracted individual sprites
│       ├── floor/
│       ├── wall/
│       ├── gateways/
│       └── ...
├── urizen/                           # Urizen OneBit V2
│   ├── urizen_onebit_tileset__v2d0.png      # Original 12x12
│   ├── urizen_onebit_tileset__v2d0_2x.png   # 2x upscaled (24x24) - USED BY GAME
│   └── urizen_atlas_index.json              # Row/col position reference
├── pixabay/                          # Free assets from Pixabay
└── README.md                         # Asset documentation
```

## Atlas Packs

### Kenney 1-Bit Colored Pack
- **Game Path**: `res://assets/kenney/colored-transparent_packed_2x.png`
- **Original**: 16x16 pixel tiles, no margin/separation
- **Used Version**: 2x upscaled to 32x32 pixels
- **Index File**: `kenney_atlas_index.json` maps descriptive names to {row, col}
- **Visual Reference**: `kenney_groups/` contains pre-sliced sections by category
- **Atlas Definition**: `resources/tiles/atlases/kenney_1bit.json`
- **Usage**: Primary atlas for building tiles, terrain, and entity placeholders

### DCSS ProjectUtumno
- **Game Path**: `res://assets/dcss/ProjectUtumno_full.png`
- **Native Size**: 32x32 pixel tiles, no margin/separation
- **Index Files**: `dcss_atlas_index.json` and `dcss_supplemental_index.json`
- **Extracted Sprites**: `dcss/dungeon/` contains individual PNGs organized by category
- **Atlas Definition**: `resources/tiles/atlases/dcss_utumno.json`
- **Usage**: Supplementary tileset for dungeon, environmental, and decorative tiles

### Urizen OneBit V2
- **Game Path**: `res://assets/urizen/urizen_onebit_tileset__v2d0_2x.png`
- **Original**: 12x12 pixel tiles with 1px margin/separation
- **Used Version**: 2x upscaled to 24x24 pixels with 2px margin/separation
- **Index File**: `urizen_atlas_index.json` maps descriptive names to {row, col}
- **Atlas Definition**: `resources/tiles/atlases/urizen_onebit.json`
- **Special Handling**: TileResourceManager enables `UseTexturePadding` to center 24x24 tiles within 32x32 grid cells
- **Usage**: Additional tileset for UI elements and small props

## Atlas Index Files

Each atlas includes a `*_atlas_index.json` file that maps descriptive names to atlas positions:

```json
{
  "category/descriptive_name": {
    "row": 5,
    "col": 10
  }
}
```

These are developer reference files, not loaded by the game engine. Use them to find tiles by name, then copy the row/col coordinates into your tile definition JSONs.

## Important Notes

### 2x Upscaling Strategy
All atlases have been upscaled 2x to provide consistent 32x32 pixel tiles:
- Kenney: 16x16 → 32x32
- DCSS: Native 32x32 (no upscaling needed)
- Urizen: 12x12 → 24x24 (then centered in 32x32 cells)

This ensures crisp rendering at the game's grid scale.

### Urizen Centering
Because Urizen tiles are 24x24, TileResourceManager applies texture padding to center them within 32x32 cells. This is automatic and requires no manual adjustment.

### Atlas JSON Definitions
The game loads atlases from JSON files in `/resources/tiles/atlases/`:
- `kenney_1bit.json` - References `colored-transparent_packed_2x.png`
- `dcss_utumno.json` - References `ProjectUtumno_full.png`
- `urizen_onebit.json` - References `urizen_onebit_tileset__v2d0_2x.png`

### Finding Tiles
1. Look up the tile name in the appropriate `*_atlas_index.json` file
2. Note the row and col values
3. Use those as `AtlasCoords` in your tile definition JSON

### Adding New Assets
1. Follow the existing directory structure
2. Use consistent naming (PascalCase for sprite sheets)
3. Create an index JSON file if adding a new atlas
4. Update atlas definitions in `/resources/tiles/atlases/`
5. Ensure final tile size is 32x32 (or 24x24 with centering)

## Dependencies

Code that loads assets:
- `/entities/beings/` - Entity sprite loading
- `/entities/building/TileResourceManager.cs` - Tileset loading and atlas setup
- Scene files (`.tscn`) reference assets directly

## Related Documentation

- See `README.md` in this directory for human-readable asset information
- See `/resources/tiles/atlases/CLAUDE.md` for atlas JSON schema
- See individual atlas index JSON files for tile lookup
