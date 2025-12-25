# Assets Directory

## Purpose

Contains all game assets including sprites, fonts, and textures. The primary visual assets are from the Minifantasy asset pack (not included in the repository due to licensing).

## Structure

```
assets/
├── fonts/
│   └── slimes_pixel_font_pack/   # Pixel fonts for UI
├── minifantasy/                   # Main game assets (NOT in repo)
│   ├── buildings/
│   │   ├── farm/                  # Farm building sprites
│   │   ├── graveyard/             # Graveyard tileset
│   │   └── house/                 # House building sprites
│   ├── entities/
│   │   ├── generic/               # Shared entity sprites
│   │   │   └── clothing/          # Clothing overlays
│   │   ├── human/                 # Human character sprites
│   │   ├── necromancer/           # Player character sprites
│   │   └── undead/                # Undead entity sprites
│   │       ├── skeleton-warrior/
│   │       └── zombie/
│   └── terrain/
│       ├── forest/                # Forest/ground tiles
│       └── water/                 # Water tiles
├── pixabay/                       # Free assets from Pixabay
└── README.md                      # Asset setup instructions
```

## Important Notes

### Licensing
- **Minifantasy assets are NOT included** in the public repository
- They must be purchased separately from Krishna Palacio
- See `README.md` for setup instructions if you own the assets

### Asset Expectations
The code expects specific file names and structures. Key files referenced:
- `necromancer/Necromancer_Idle.png`, `Necromancer_Walk.png`
- `undead/skeleton-warrior/Idle_Activation_Deactivation.png`
- `undead/zombie/ZombieIdle.png`
- Building tilesets in respective building folders

### Adding New Assets
1. Follow the existing directory structure
2. Use consistent naming (PascalCase for sprite sheets)
3. Update atlas definitions in `/resources/tiles/atlases/` if adding tilesets
4. Sprite dimensions should match existing assets (typically 8x8 or 16x16 tiles)

## Dependencies

Code that loads assets:
- `/entities/beings/` - Entity sprite loading
- `/entities/building/TileResourceManager.cs` - Tileset loading
- Scene files (`.tscn`) reference assets directly

## Related Documentation

- See `README.md` in this directory for detailed asset setup
- See `/resources/tiles/atlases/` for atlas definitions
