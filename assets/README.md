# Assets Directory

This directory holds game assets used by Veil of Ages, including sprites, fonts, and textures.

## Atlas Packs

The game uses open/free-license atlas packs plus original custom pixel art. All atlases provide consistent 32x32 pixel tiles.

### Kenney 1-Bit Colored Pack
- **Used File**: `kenney/colored-transparent_packed_2x.png`
- **Original**: `colored-transparent_packed.png` (16x16 tiles)
- **Upscaling**: 2x to 32x32 pixels
- **Margin/Separation**: None
- **License**: CC0 (Public Domain)
- **Source**: [kenney.nl](https://kenney.nl)
- **Usage**: Primary atlas for building tiles, terrain, and entity placeholders

The Kenney pack includes:
- **Atlas Index**: `kenney_atlas_index.json` - Maps descriptive names to {row, col} positions

### DCSS ProjectUtumno
- **Used File**: `dcss/ProjectUtumno_full.png`
- **Native Size**: 32x32 pixel tiles (no upscaling needed)
- **Margin/Separation**: None
- **License**: CC0 (Public Domain) - see `dcss/LICENSE.txt`
- **Source**: Dungeon Crawl Stone Soup tileset by ProjectUtumno
- **Usage**: Supplementary tileset for dungeon, environmental, and decorative tiles

The DCSS pack includes two atlases:
- **Main Atlas**: `ProjectUtumno_full.png` — indexed by `dcss_utumno_index.json`
- **Supplemental Atlas**: `supplemental_atlas.png` — indexed by `dcss_supplemental_index.json`
- **Reference PNGs**: Subdirectories contain individual PNGs for visual inspection (NOT loaded by the game)

### Urizen OneBit V2
- **Used File**: `urizen/urizen_onebit_tileset__v2d0_32x32.png`
- **Original**: `urizen_onebit_tileset__v2d0.png` (12x12 tiles with 1px margin/separation)
- **Upscaling**: To native 32x32 pixels, no margin/separation
- **License**: CC0 (Public Domain)
- **Source**: [vurmux on itch.io](https://vurmux.itch.io/urizen-onebit-tileset)
- **Usage**: Additional tileset for UI elements and small props

The Urizen pack includes:
- **Atlas Index**: `urizen_atlas_index.json` - Maps descriptive names to {row, col} positions

### Custom Assets
- **Used File**: `custom/custom_atlas.png`
- **Native Size**: 32x32 pixel tiles
- **Margin/Separation**: None
- **License**: CC0 (Public Domain)
- **Usage**: Original pixel art created for Veil of Ages

The custom pack includes:
- **Atlas Index**: `custom_atlas_index.json` - Maps names to {row, col} positions
- **Source Files**: Each sprite subdirectory has `grid.txt` + `palette.txt` for editing using [gridfab](https://github.com/azrazalea/gridfab)

## Audio Assets

### Pixabay
- **License**: Pixabay Content License (free to use, no attribution required; see `pixabay/LICENSE.md`)
- **Contents**: Sound effects

## Using Atlas Indexes

Each atlas includes a `*_atlas_index.json` file for developer reference:

```json
{
  "category/descriptive_name": {
    "row": 5,
    "col": 10
  }
}
```

These files are NOT loaded by the game engine. To use a tile:
1. Look up the tile name in the index file
2. Note the row and col coordinates
3. Use those coordinates as `AtlasCoords` in your tile definition JSON files

## Asset Structure

```
assets/
├── fonts/
│   └── slimes_pixel_font_pack/       # Pixel fonts for UI
├── kenney/                           # Kenney 1-Bit Colored Pack
│   ├── colored-transparent_packed.png        # Original 16x16
│   ├── colored-transparent_packed_2x.png     # 2x upscaled - USED BY GAME
│   └── kenney_atlas_index.json
├── dcss/                             # DCSS ProjectUtumno (TWO atlases)
│   ├── ProjectUtumno_full.png               # Main atlas - USED BY GAME
│   ├── supplemental_atlas.png               # Supplemental atlas - USED BY GAME
│   ├── dcss_utumno_index.json
│   ├── dcss_supplemental_index.json
│   ├── LICENSE.txt
│   └── (subdirectories)                     # Individual PNGs (REFERENCE ONLY)
├── urizen/                           # Urizen OneBit V2
│   ├── urizen_onebit_tileset__v2d0.png      # Original 12x12 (not used by game)
│   ├── urizen_onebit_tileset__v2d0_2x.png   # 2x upscaled 24x24 (not used by game)
│   ├── urizen_onebit_tileset__v2d0_32x32.png # Upscaled to 32x32 - USED BY GAME
│   └── urizen_atlas_index.json
├── custom/                           # Original pixel art for Veil of Ages
│   ├── custom_atlas.png                     # USED BY GAME
│   └── custom_atlas_index.json
└── pixabay/                          # Audio assets (Pixabay Content License)
```

## Technical Notes

### Tile Size Strategy
All atlases provide consistent 32x32 pixel tiles:
- Kenney: 16x16 → 32x32 (2x upscaled)
- DCSS: Native 32x32 (no upscaling)
- Urizen: 12x12 → 32x32 (upscaled to native 32x32)
- Custom: Native 32x32

### Atlas Definitions
Atlas JSON definitions are located in `/resources/tiles/atlases/`:
- `kenney_1bit.json` - ID: `kenney`
- `dcss_utumno.json` - ID: `dcss`
- `dcss_supplemental.json` - ID: `dcss_supplemental`
- `urizen_onebit.json` - ID: `urizen`
- `custom.json` - ID: `custom`

## License Summary

- **Visual atlas packs** (Kenney, DCSS, Urizen): CC0 (Public Domain)
- **Custom assets**: CC0 (Public Domain)
- **Audio** (Pixabay): Pixabay Content License (free, no attribution required)
- **Code**: Licensed separately under Modified AGPLv3
