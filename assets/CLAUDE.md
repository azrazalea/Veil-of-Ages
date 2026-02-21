# Assets Directory

## Purpose

Contains all game assets including sprites, fonts, and textures. Visual assets come from open/free-license atlas packs plus original custom pixel art, all providing 32x32 pixel tiles for the game's grid system.

## Structure

```
assets/
├── fonts/
│   └── slimes_pixel_font_pack/       # Pixel fonts for UI
├── kenney/                           # Kenney 1-Bit Colored Pack
│   ├── colored-transparent_packed.png        # Original 16x16
│   ├── colored-transparent_packed_2x.png     # 2x upscaled (32x32) - USED BY GAME
│   └── kenney_atlas_index.json              # Row/col position reference
├── dcss/                             # DCSS ProjectUtumno (combined atlas)
│   ├── dcss_combined_atlas.png              # Combined 32x32 atlas - USED BY GAME
│   ├── dcss_combined_index.json             # Combined atlas index (name → row/col) - USE THIS
│   ├── ProjectUtumno_full.png               # Old main atlas - REFERENCE ONLY (being phased out)
│   ├── supplemental_atlas.png               # Old supplemental atlas - REFERENCE ONLY (being phased out)
│   ├── dcss_utumno_index.json               # Old main index - REFERENCE ONLY (being phased out)
│   ├── dcss_supplemental_index.json         # Old supplemental index - REFERENCE ONLY (being phased out)
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
│   ├── urizen_onebit_tileset__v2d0_2x.png   # 2x upscaled (24x24)
│   ├── urizen_onebit_tileset__v2d0_32x32.png # Upscaled to 32x32 - USED BY GAME
│   ├── urizen_atlas_index.json              # Row/col position reference
│   └── LICENSE.md                           # CC0 license
├── custom/                           # Original pixel art for Veil of Ages
│   ├── custom_atlas.png                     # Custom atlas - USED BY GAME
│   ├── custom_atlas_index.json              # Row/col position reference
│   └── LICENSE.md                           # CC0 license
├── pixabay/                          # Free audio assets from Pixabay
│   └── LICENSE.md                           # Pixabay Content License
└── README.md                         # Asset documentation
```

## Atlas Packs

### Kenney 1-Bit Colored Pack
- **Game Path**: `res://assets/kenney/colored-transparent_packed_2x.png`
- **Original**: 16x16 pixel tiles, no margin/separation
- **Used Version**: 2x upscaled to 32x32 pixels
- **Index File**: `kenney_atlas_index.json` maps descriptive names to {row, col}
- **Atlas Definition**: `resources/tiles/atlases/kenney_1bit.json` (ID: `kenney`)
- **Usage**: Primary atlas for building tiles, terrain, and entity placeholders

### DCSS ProjectUtumno (Combined Atlas)
**Every individual PNG file under `dcss/` is packed into the combined atlas and indexed. The individual files exist ONLY for easy inspection/search — the game always loads from `dcss_combined_atlas.png`.**

#### Combined Atlas (Primary — always use this)
- **Game Path**: `res://assets/dcss/dcss_combined_atlas.png`
- **Native Size**: 32x32 pixel tiles, no margin/separation
- **Index File**: `dcss_combined_index.json` — maps names to `{row, col}` positions
- **Atlas Definition**: `resources/tiles/atlases/dcss_utumno.json` (ID: `dcss`)
- **Contents**: All DCSS sprites — dungeon tiles, environmental tiles, monsters, items, player doll overlays, etc.

#### Old Atlases (Reference Only — being phased out)
- `ProjectUtumno_full.png` and `supplemental_atlas.png` exist for visual reference only
- `dcss_utumno_index.json` and `dcss_supplemental_index.json` exist for reference only during migration
- These will be removed once migration is complete. Do not use them for new work.

#### Individual Files (Reference Only)
- `dcss/dungeon/` — dungeon tiles organized by category (floor, wall, etc.)
- `dcss/player/` — player doll overlay layers (base, body, cloak, hair, head, boots, etc.)
- **These files are NOT loaded by the game.** They exist solely for developer reference to visually inspect sprites and find what's available. Always look up sprites by name in `dcss_combined_index.json`, then use row/col from there.

- **Usage**: Primary tileset for dungeon/environmental tiles; player doll system for layered entity sprites

### Urizen OneBit V2
- **Game Path**: `res://assets/urizen/urizen_onebit_tileset__v2d0_32x32.png`
- **Original**: 12x12 pixel tiles with 1px margin/separation
- **Used Version**: Upscaled to native 32x32 pixels, no margin/separation
- **Index File**: `urizen_atlas_index.json` maps descriptive names to {row, col}
- **Atlas Definition**: `resources/tiles/atlases/urizen_onebit.json` (ID: `urizen`)
- **Usage**: Additional tileset for UI elements and small props

### Custom Assets
- **Game Path**: `res://assets/custom/custom_atlas.png`
- **Native Size**: 32x32 pixel tiles, no margin/separation
- **Index File**: `custom_atlas_index.json` maps names to {row, col}
- **Atlas Definition**: `resources/tiles/atlases/custom.json` (ID: `custom`)
- **Source Files**: Each sprite has a subdirectory with `grid.txt` and `palette.txt` for editing using [gridfab](https://github.com/azrazalea/gridfab)
- **Usage**: Original pixel art created specifically for Veil of Ages (e.g., quern)

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

### Tile Size Strategy
All atlases provide consistent 32x32 pixel tiles:
- Kenney: 16x16 → 32x32 (2x upscaled)
- DCSS: Native 32x32 (no upscaling needed)
- Urizen: 12x12 → 32x32 (upscaled to native 32x32, no margin/separation)
- Custom: Native 32x32

This ensures crisp rendering at the game's grid scale.

### Atlas JSON Definitions
The game loads atlases from JSON files in `/resources/tiles/atlases/`:
- `kenney_1bit.json` - ID: `kenney`, references `colored-transparent_packed_2x.png`
- `dcss_utumno.json` - ID: `dcss`, references `dcss_combined_atlas.png`
- `urizen_onebit.json` - ID: `urizen`, references `urizen_onebit_tileset__v2d0_32x32.png`
- `custom.json` - ID: `custom`, references `custom_atlas.png`

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
