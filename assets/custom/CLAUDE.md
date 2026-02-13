# Custom Assets

## Purpose

Original pixel art assets created specifically for Veil of Ages. These are 32x32 sprites in the style of Urizen 1-bit but with expanded color palettes (typically 2 colors + black outline).

## Structure

Each asset has its own subdirectory containing:
- `<name>.png` — Final 32x32 sprite with transparency (game-ready)
- `grid.txt` — Source pixel grid for editing
- `palette.txt` — Color palette used by the sprite

## Creating/Editing Assets

Use the tools in `scripts/`:
- `python scripts/pixelart.py` — CLI tool for grid manipulation
- `python scripts/pixelart_gui.py <asset_dir>` — GUI editor with palette, save, render, refresh

### Workflow
1. Create a working directory in `scripts/` (e.g., `scripts/my_sprite/`)
2. `python scripts/pixelart.py init` to create grid.txt and palette.txt
3. Edit with GUI: `python scripts/pixelart_gui.py scripts/my_sprite/`
4. When done, export: `python scripts/pixelart.py export`
5. Copy `output.png`, `grid.txt`, and `palette.txt` to `assets/custom/<name>/`
6. Rename `output.png` to `<name>.png`

## Current Assets

### quern/
Hand quern (grain grinding stone). Two stacked circular stone discs with a wooden handle, viewed from 3/4 top-down perspective. Light source from top-left.
- **Palette**: Stone light (#C8B8A0), Stone dark (#8B7D6B), Wood light (#A0784B), Wood dark (#7A5C32), Black outline (#000000)

## License

See LICENSE.md in this directory. All custom assets are original works released under CC0 (Public Domain).
