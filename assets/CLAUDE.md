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
├── dcss/                             # DCSS ProjectUtumno (TWO atlases)
│   ├── ProjectUtumno_full.png               # Main 32x32 atlas - USED BY GAME
│   ├── supplemental_atlas.png               # Supplemental 32x32 atlas - USED BY GAME
│   ├── dcss_utumno_index.json               # Main atlas index (name → row/col)
│   ├── dcss_supplemental_index.json         # Supplemental atlas index (name → row/col)
│   ├── LICENSE.txt
│   ├── dungeon/                             # Individual PNGs (REFERENCE ONLY, not loaded)
│   │   ├── floor/
│   │   ├── wall/
│   │   └── ...
│   └── player/                              # Player doll overlays (REFERENCE ONLY)
│       ├── base/                            # Body bases (human_female, elf_male, etc.)
│       ├── body/                            # Clothing/armor layers
│       ├── cloak/                           # Cloak overlays
│       ├── hair/                            # Hair styles
│       ├── head/                            # Headwear/hoods/helms
│       └── ...
├── urizen/                           # Urizen OneBit V2
│   ├── urizen_onebit_tileset__v2d0.png      # Original 12x12
│   ├── urizen_onebit_tileset__v2d0_32x32.png # Upscaled to 32x32 - USED BY GAME
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
- **Atlas Definition**: `resources/tiles/atlases/kenney_1bit.json` (ID: `kenney`)
- **Usage**: Primary atlas for building tiles, terrain, and entity placeholders

### DCSS ProjectUtumno (Two Atlases)
**Every individual PNG file under `dcss/` is packed into one of these two atlases and indexed. The individual files exist ONLY for easy inspection/search — the game always loads from the atlas PNGs.**

#### Main Atlas
- **Game Path**: `res://assets/dcss/ProjectUtumno_full.png`
- **Native Size**: 32x32 pixel tiles, no margin/separation
- **Index File**: `dcss_utumno_index.json` — maps names to `{row, col}` positions
- **Atlas Definition**: `resources/tiles/atlases/dcss_utumno.json` (ID: `dcss`)
- **Contents**: Dungeon tiles, environmental tiles, monsters, items, etc.

#### Supplemental Atlas
- **Game Path**: `res://assets/dcss/supplemental_atlas.png`
- **Native Size**: 32x32 pixel tiles, no margin/separation
- **Index File**: `dcss_supplemental_index.json` — maps names to `{row, col}` positions
- **Atlas Definition**: `resources/tiles/atlases/dcss_supplemental.json` (ID: `dcss_supplemental`)
- **Contents**: Player doll overlays (base bodies, hair, clothing, cloaks, armor, headwear, etc.), additional sprites not in the main atlas

#### Individual Files (Reference Only)
- `dcss/dungeon/` — dungeon tiles organized by category (floor, wall, etc.)
- `dcss/player/` — player doll overlay layers (base, body, cloak, hair, head, boots, etc.)
- **These files are NOT loaded by the game.** They exist solely for developer reference to visually inspect sprites and find what's available. Always look up sprites by name in the index JSON files, then use row/col from there.

- **Usage**: Primary tileset for dungeon/environmental tiles; player doll system for layered entity sprites

### Urizen OneBit V2
- **Game Path**: `res://assets/urizen/urizen_onebit_tileset__v2d0_32x32.png`
- **Original**: 12x12 pixel tiles with 1px margin/separation
- **Used Version**: Upscaled to native 32x32 pixels, no margin/separation
- **Index File**: `urizen_atlas_index.json` maps descriptive names to {row, col}
- **Atlas Definition**: `resources/tiles/atlases/urizen_onebit.json` (ID: `urizen`)
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

### Upscaling Strategy
All atlases provide consistent 32x32 pixel tiles:
- Kenney: 16x16 → 32x32 (2x upscaled)
- DCSS: Native 32x32 (no upscaling needed)
- Urizen: 12x12 → 32x32 (upscaled to native 32x32, no margin/separation)

This ensures crisp rendering at the game's grid scale.

### Atlas JSON Definitions
The game loads atlases from JSON files in `/resources/tiles/atlases/`:
- `kenney_1bit.json` - ID: `kenney`, references `colored-transparent_packed_2x.png`
- `dcss_utumno.json` - ID: `dcss`, references `ProjectUtumno_full.png`
- `dcss_supplemental.json` - ID: `dcss_supplemental`, references `supplemental_atlas.png`
- `urizen_onebit.json` - ID: `urizen`, references `urizen_onebit_tileset__v2d0_32x32.png`

### Finding Tiles
1. Look up the tile name in the appropriate `*_atlas_index.json` file
2. Note the row and col values
3. Use those as `AtlasCoords` in your tile definition JSON

### Adding New Assets
1. Follow the existing directory structure
2. Use consistent naming (PascalCase for sprite sheets)
3. Create an index JSON file if adding a new atlas
4. Update atlas definitions in `/resources/tiles/atlases/`
5. Ensure final tile size is 32x32

## Dependencies

Code that loads assets:
- `/entities/beings/` - Entity sprite loading
- `/entities/building/TileResourceManager.cs` - Tileset loading and atlas setup
- Scene files (`.tscn`) reference assets directly

## Related Documentation

- See `README.md` in this directory for human-readable asset information
- See `/resources/tiles/atlases/CLAUDE.md` for atlas JSON schema
- See individual atlas index JSON files for tile lookup
