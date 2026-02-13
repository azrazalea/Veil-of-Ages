#!/usr/bin/env python3
"""
Upscale Urizen 12x12 atlas to 32x32 atlas.

Input: urizen_onebit_tileset__v2d0.png (12x12 tiles, 1px margin, 1px separation)
Output: urizen_onebit_tileset__v2d0_32x32.png (32x32 tiles, no margin, no separation)

Process:
1. Extract each 12x12 tile from the original atlas
2. Upscale each tile to 32x32 using nearest-neighbor
3. Save individual tiles to urizen_tiles/ directory
4. Reassemble all tiles into a single atlas with no spacing
"""

import os
from PIL import Image

# Paths
INPUT_ATLAS = r"C:\Users\azraz\Documents\Veil of Ages\assets\urizen\urizen_onebit_tileset__v2d0.png"
OUTPUT_DIR = r"C:\Users\azraz\Documents\Veil of Ages\scripts\urizen_tiles"
OUTPUT_ATLAS = r"C:\Users\azraz\Documents\Veil of Ages\assets\urizen\urizen_onebit_tileset__v2d0_32x32.png"

# Original atlas parameters
ORIGINAL_TILE_SIZE = 12
MARGIN = 1
SEPARATION = 1

# Target tile size
TARGET_TILE_SIZE = 32

def calculate_grid_dimensions(image_width, image_height):
    """Calculate number of tile columns and rows from image dimensions."""
    # Formula: num_tiles = (image_size - margin) / (tile_size + separation)
    # There's a margin on both sides but they overlap with separation, so just 1px margin total
    num_cols = (image_width - MARGIN) // (ORIGINAL_TILE_SIZE + SEPARATION)
    num_rows = (image_height - MARGIN) // (ORIGINAL_TILE_SIZE + SEPARATION)
    return num_cols, num_rows

def extract_tile(atlas_image, col, row):
    """Extract a single 12x12 tile from the atlas at grid position (col, row)."""
    # Top-left pixel position: margin + index * (tile_size + separation)
    x = MARGIN + col * (ORIGINAL_TILE_SIZE + SEPARATION)
    y = MARGIN + row * (ORIGINAL_TILE_SIZE + SEPARATION)

    # Extract the tile (left, upper, right, lower)
    tile = atlas_image.crop((x, y, x + ORIGINAL_TILE_SIZE, y + ORIGINAL_TILE_SIZE))
    return tile

def upscale_tile(tile):
    """Upscale a tile from 12x12 to 32x32 using nearest-neighbor."""
    return tile.resize((TARGET_TILE_SIZE, TARGET_TILE_SIZE), Image.NEAREST)

def main():
    print("=" * 60)
    print("Urizen Atlas Upscaler: 12x12 -> 32x32")
    print("=" * 60)

    # Load the original atlas
    print(f"\nLoading atlas: {INPUT_ATLAS}")
    atlas = Image.open(INPUT_ATLAS)
    atlas_width, atlas_height = atlas.size
    print(f"Atlas dimensions: {atlas_width}x{atlas_height}")

    # Calculate grid dimensions
    num_cols, num_rows = calculate_grid_dimensions(atlas_width, atlas_height)
    total_tiles = num_cols * num_rows
    print(f"Grid dimensions: {num_cols} columns × {num_rows} rows = {total_tiles} tiles")

    # Create output directory for individual tiles
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"\nOutput directory: {OUTPUT_DIR}")

    # Extract, upscale, and save individual tiles
    print(f"\nExtracting and upscaling {total_tiles} tiles...")
    upscaled_tiles = []

    for row in range(num_rows):
        row_tiles = []
        for col in range(num_cols):
            # Extract original 12x12 tile
            tile = extract_tile(atlas, col, row)

            # Upscale to 32x32
            upscaled_tile = upscale_tile(tile)

            # Save individual tile
            tile_filename = f"{row}_{col}.png"
            tile_path = os.path.join(OUTPUT_DIR, tile_filename)
            upscaled_tile.save(tile_path)

            row_tiles.append(upscaled_tile)

            # Progress indicator
            tile_num = row * num_cols + col + 1
            if tile_num % 50 == 0 or tile_num == total_tiles:
                print(f"  Processed {tile_num}/{total_tiles} tiles...")

        upscaled_tiles.append(row_tiles)

    print(f"Done: All tiles extracted and saved to {OUTPUT_DIR}")

    # Reassemble into final atlas (no margin, no separation)
    print(f"\nReassembling atlas...")
    final_width = num_cols * TARGET_TILE_SIZE
    final_height = num_rows * TARGET_TILE_SIZE
    print(f"Final atlas dimensions: {final_width}x{final_height}")

    # Create new image for the final atlas
    final_atlas = Image.new('RGBA', (final_width, final_height))

    # Paste each tile directly adjacent (no spacing)
    for row in range(num_rows):
        for col in range(num_cols):
            x = col * TARGET_TILE_SIZE
            y = row * TARGET_TILE_SIZE
            final_atlas.paste(upscaled_tiles[row][col], (x, y))

    # Save final atlas
    final_atlas.save(OUTPUT_ATLAS)
    print(f"Done: Final atlas saved: {OUTPUT_ATLAS}")

    print("\n" + "=" * 60)
    print("Upscaling complete!")
    print("=" * 60)
    print(f"\nSummary:")
    print(f"  Input:  {atlas_width}x{atlas_height} ({ORIGINAL_TILE_SIZE}x{ORIGINAL_TILE_SIZE} tiles)")
    print(f"  Output: {final_width}x{final_height} ({TARGET_TILE_SIZE}x{TARGET_TILE_SIZE} tiles)")
    print(f"  Tiles:  {total_tiles} ({num_cols}×{num_rows})")
    print(f"  Method: Nearest-neighbor upscaling")

if __name__ == "__main__":
    main()
