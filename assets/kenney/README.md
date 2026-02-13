# Kenney 1-Bit Pack

Source: https://kenney.nl/assets/1-bit-pack
License: CC0 1.0 Universal (Public Domain)

## Files

- `colored-transparent_packed.png` — original 16×16 packed spritesheet (colored, transparent background)
- `colored-transparent_packed_2x.png` — 2× nearest-neighbor upscale to 32×32 tiles

## Upscale command

```bash
convert colored-transparent_packed.png -filter point -resize 200% colored-transparent_packed_2x.png
```

## Godot import

For the 2× version, use the same grid settings as the original but doubled.
