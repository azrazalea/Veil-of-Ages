# Assets Directory

This directory holds game assets used by Veil of Ages, including sprites, fonts, and textures.

## Atlas Packs

The game uses three open/free-license atlas packs for sprites and tiles. All atlases have been upscaled to provide consistent 32x32 pixel tiles.

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
- **Visual Reference**: `kenney_groups/` folder contains pre-sliced category images (floors_and_terrain, buildings_and_roofs, characters_and_faces, etc.) for easier tile discovery

### DCSS ProjectUtumno
- **Used File**: `dcss/ProjectUtumno_full.png`
- **Native Size**: 32x32 pixel tiles (no upscaling needed)
- **Margin/Separation**: None
- **License**: CC0 (Public Domain) - see `dcss/LICENSE.txt`
- **Source**: Dungeon Crawl Stone Soup tileset by ProjectUtumno
- **Usage**: Supplementary tileset for dungeon, environmental, and decorative tiles

The DCSS pack includes:
- **Atlas Indexes**: `dcss_atlas_index.json` and `dcss_supplemental_index.json` - Map sprite names to {row, col} positions
- **Extracted Sprites**: `dcss/dungeon/` folder contains individual PNGs organized by category (floor/, wall/, gateways/, shops/, statues/, traps/, trees/, vaults/)

### Urizen OneBit V2
- **Used File**: `urizen/urizen_onebit_tileset__v2d0_2x.png`
- **Original**: `urizen_onebit_tileset__v2d0.png` (12x12 tiles with 1px margin/separation)
- **Upscaling**: 2x to 24x24 pixels with 2px margin/separation
- **Special Handling**: Tiles are centered within 32x32 grid cells using texture padding
- **License**: CC0 (Public Domain)
- **Source**: [vurmux on itch.io](https://vurmux.itch.io/urizen-onebit-tileset)
- **Usage**: Additional tileset for UI elements and small props

The Urizen pack includes:
- **Atlas Index**: `urizen_atlas_index.json` - Maps descriptive names to {row, col} positions

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
│   ├── kenney_atlas_index.json
│   └── kenney_groups/                       # Pre-sliced reference images
├── dcss/                             # DCSS ProjectUtumno
│   ├── ProjectUtumno_full.png               # USED BY GAME
│   ├── dcss_atlas_index.json
│   ├── dcss_supplemental_index.json
│   ├── LICENSE.txt
│   └── dungeon/                             # Extracted sprites
├── urizen/                           # Urizen OneBit V2
│   ├── urizen_onebit_tileset__v2d0.png      # Original 12x12
│   ├── urizen_onebit_tileset__v2d0_2x.png   # 2x upscaled - USED BY GAME
│   └── urizen_atlas_index.json
└── pixabay/                          # Free assets from Pixabay
```

## Technical Notes

### Upscaling Strategy
All atlases have been upscaled 2x to ensure crisp rendering:
- Kenney: 16x16 → 32x32
- DCSS: Native 32x32 (no upscaling)
- Urizen: 12x12 → 24x24 (then centered in 32x32 cells)

### Atlas Definitions
Atlas JSON definitions are located in `/resources/tiles/atlases/`:
- `kenney_1bit.json` - References the 2x upscaled Kenney atlas
- `dcss_utumno.json` - References the DCSS full atlas
- `urizen_onebit.json` - References the 2x upscaled Urizen atlas

### Urizen Centering
Because Urizen tiles are 24x24, the TileResourceManager automatically enables texture padding to center them within 32x32 grid cells. No manual adjustment is needed.

## License Summary

All three atlas packs use CC0 (Public Domain) licenses and are included in this repository. The game code is licensed separately under Modified AGPLv3.
