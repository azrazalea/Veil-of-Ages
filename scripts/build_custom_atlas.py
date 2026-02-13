#!/usr/bin/env python3
"""
Build a tile atlas from all custom asset PNGs.

Finds all .png files in assets/custom/<subdir>/ and packs them into:
  - assets/custom/custom_atlas.png (32x32 tiles, no separation/margin)
  - assets/custom/custom_atlas_index.json (name -> {row, col})

Usage: python scripts/build_custom_atlas.py
"""

import json
import math
import sys
from pathlib import Path
from PIL import Image

TILE_SIZE = 32
SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
CUSTOM_DIR = PROJECT_ROOT / "assets" / "custom"
OUTPUT_PNG = CUSTOM_DIR / "custom_atlas.png"
OUTPUT_JSON = CUSTOM_DIR / "custom_atlas_index.json"


def find_assets():
    """Find all PNGs in subdirectories of assets/custom/."""
    assets = []
    for subdir in sorted(CUSTOM_DIR.iterdir()):
        if not subdir.is_dir():
            continue
        for png in sorted(subdir.glob("*.png")):
            assets.append((subdir.name, png))
    return assets


def main():
    assets = find_assets()
    if not assets:
        print("No assets found in assets/custom/*/")
        sys.exit(1)

    print(f"Found {len(assets)} asset(s):")
    for name, path in assets:
        print(f"  {name}: {path.name}")

    # Calculate grid size (roughly square)
    cols = math.ceil(math.sqrt(len(assets)))
    rows = math.ceil(len(assets) / cols)

    atlas = Image.new("RGBA", (cols * TILE_SIZE, rows * TILE_SIZE), (0, 0, 0, 0))
    index = {}

    for i, (name, path) in enumerate(assets):
        r = i // cols
        c = i % cols
        tile = Image.open(path)
        if tile.size != (TILE_SIZE, TILE_SIZE):
            print(f"  WARNING: {path} is {tile.size}, expected {TILE_SIZE}x{TILE_SIZE}")
        atlas.paste(tile, (c * TILE_SIZE, r * TILE_SIZE))
        index[name] = {"row": r, "col": c}

    atlas.save(OUTPUT_PNG)
    print(f"\nAtlas: {OUTPUT_PNG} ({cols * TILE_SIZE}x{rows * TILE_SIZE}, {cols}x{rows} tiles)")

    with open(OUTPUT_JSON, "w", newline="\n") as f:
        json.dump(index, f, indent=2)
    print(f"Index: {OUTPUT_JSON}")


if __name__ == "__main__":
    main()
