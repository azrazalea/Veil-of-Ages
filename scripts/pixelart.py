#!/usr/bin/env python3
"""
Pixel art CLI tool for creating 32x32 sprites.

Usage: python pixelart.py <command> [args...]

Commands:
    init                                    Create blank grid.txt and starter palette.txt
    render / show                           Render preview.png (scaled 8x, checkerboard bg)
    row <row> <v0 v1 ... v31>              Replace a single row (0-indexed)
    rows <start> <end> <v0 v1 ...>         Replace a range of rows (inclusive)
    fill <row> <col_start> <col_end> <c>   Fill horizontal span with one color
    rect <r0> <c0> <r1> <c1> <color>       Fill a rectangle with one color
    export                                  Export final PNGs (1x, 4x, 8x, 16x) with transparency
    palette                                 Display current palette
"""

import sys
from pathlib import Path

GRID_SIZE = 32
PREVIEW_SCALE = 8
TRANSPARENT = "."
CHECKER_LIGHT = (220, 220, 220)
CHECKER_DARK = (180, 180, 180)
CHECKER_SIZE = 4  # checkerboard square size in source pixels (before scaling)


def die(msg):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def parse_int(s, name):
    try:
        return int(s)
    except ValueError:
        die(f"{name} must be an integer, got: '{s}'")


def validate_hex_color(color, context):
    if not color.startswith("#"):
        die(f"{context}: color must start with '#', got: '{color}'")
    if len(color) != 7:
        die(f"{context}: color must be #RRGGBB (7 chars), got: '{color}'")
    try:
        int(color[1:], 16)
    except ValueError:
        die(f"{context}: invalid hex digits in color: '{color}'")


def hex_to_rgb(hex_color):
    return (int(hex_color[1:3], 16), int(hex_color[3:5], 16), int(hex_color[5:7], 16))


# --- Palette ---


def load_palette(path):
    """Load palette.txt. Returns dict mapping alias -> hex color or None."""
    palette = {TRANSPARENT: None}
    if not path.exists():
        return palette

    with open(path) as f:
        for line_num, raw_line in enumerate(f, 1):
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            if "=" not in line:
                die(f"palette.txt:{line_num}: expected ALIAS=COLOR, got: '{line}'")
            alias, color = line.split("=", 1)
            alias = alias.strip()
            color = color.strip()
            if len(alias) != 1:
                die(f"palette.txt:{line_num}: alias must be exactly one character, got: '{alias}'")
            if alias == TRANSPARENT:
                die(f"palette.txt:{line_num}: '.' is reserved for transparent, cannot redefine")
            if alias in palette:
                die(f"palette.txt:{line_num}: duplicate alias '{alias}'")
            if color.lower() == "transparent":
                palette[alias] = None
            else:
                validate_hex_color(color, f"palette.txt:{line_num}")
                palette[alias] = color

    return palette


def resolve_color(value, palette, context):
    """Resolve a grid value to hex color string or None (transparent)."""
    if value == TRANSPARENT:
        return None
    if len(value) == 1 and value in palette:
        return palette[value]
    if value.startswith("#"):
        validate_hex_color(value, context)
        return value
    if len(value) == 1:
        die(f"{context}: unknown palette alias '{value}' — define it in palette.txt")
    die(f"{context}: invalid value '{value}' — use #RRGGBB, a palette alias, or '.'")


# --- Grid I/O ---


def load_grid(path):
    """Load grid.txt. Returns list of lists of raw string values."""
    if not path.exists():
        die("grid.txt not found — run 'init' first")

    rows = []
    with open(path) as f:
        for line_num, raw_line in enumerate(f, 1):
            line = raw_line.rstrip("\n")
            if line.strip() == "":
                die(f"grid.txt:{line_num}: unexpected blank line")
            values = line.split()
            if len(values) != GRID_SIZE:
                die(f"grid.txt:{line_num}: expected {GRID_SIZE} values, got {len(values)}")
            rows.append(values)

    if len(rows) != GRID_SIZE:
        die(f"grid.txt: expected {GRID_SIZE} rows, got {len(rows)}")

    return rows


def save_grid(path, rows):
    with open(path, "w", newline="\n") as f:
        for row in rows:
            f.write(" ".join(row) + "\n")


def resolve_grid(raw_rows, palette):
    """Convert raw grid to resolved colors (hex string or None)."""
    result = []
    for r, row in enumerate(raw_rows):
        resolved = []
        for c, val in enumerate(row):
            resolved.append(resolve_color(val, palette, f"grid row {r} col {c}"))
        result.append(resolved)
    return result


# --- Rendering ---


def render_image(colors, scale, transparent_bg):
    """Render resolved color grid to a PIL Image.

    If transparent_bg is False, transparent pixels get a checkerboard pattern.
    If transparent_bg is True, transparent pixels are RGBA (0,0,0,0).
    """
    from PIL import Image

    size = GRID_SIZE * scale
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    for r, row in enumerate(colors):
        for c, color in enumerate(row):
            if color is not None:
                rgba = (*hex_to_rgb(color), 255)
            elif not transparent_bg:
                # Checkerboard for preview
                checker = ((r // CHECKER_SIZE) + (c // CHECKER_SIZE)) % 2
                rgba = (*(CHECKER_LIGHT if checker == 0 else CHECKER_DARK), 255)
            else:
                continue  # leave transparent

            # Fill the scaled block
            for dy in range(scale):
                for dx in range(scale):
                    img.putpixel((c * scale + dx, r * scale + dy), rgba)

    return img


# --- Commands ---


def cmd_init():
    grid_path = Path("grid.txt")
    palette_path = Path("palette.txt")

    if grid_path.exists():
        die("grid.txt already exists — delete it first to reinitialize")

    rows = [[TRANSPARENT] * GRID_SIZE for _ in range(GRID_SIZE)]
    save_grid(grid_path, rows)
    print(f"Created grid.txt ({GRID_SIZE}x{GRID_SIZE}, all transparent)")

    if not palette_path.exists():
        with open(palette_path, "w", newline="\n") as f:
            f.write("# Palette: ALIAS=#RRGGBB\n")
            f.write("# Single-character aliases only. '.' is reserved for transparent.\n")
        print("Created palette.txt (empty starter)")
    else:
        print("palette.txt already exists, keeping it")


def cmd_render():
    raw = load_grid(Path("grid.txt"))
    palette = load_palette(Path("palette.txt"))
    colors = resolve_grid(raw, palette)
    img = render_image(colors, PREVIEW_SCALE, transparent_bg=False)
    img.save("preview.png")
    print(f"Rendered preview.png ({GRID_SIZE * PREVIEW_SCALE}x{GRID_SIZE * PREVIEW_SCALE})")


def cmd_row(args):
    if len(args) != GRID_SIZE + 1:
        die(f"Usage: row <row_number> <v0> <v1> ... <v{GRID_SIZE - 1}> — "
            f"got {len(args) - 1} values instead of {GRID_SIZE}")

    row_num = parse_int(args[0], "row number")
    values = args[1:]

    if row_num < 0 or row_num >= GRID_SIZE:
        die(f"Row must be 0-{GRID_SIZE - 1}, got {row_num}")

    palette = load_palette(Path("palette.txt"))
    for i, v in enumerate(values):
        resolve_color(v, palette, f"position {i}")

    rows = load_grid(Path("grid.txt"))
    rows[row_num] = values
    save_grid(Path("grid.txt"), rows)
    print(f"Row {row_num} updated.")


def cmd_rows(args):
    if len(args) < 3:
        die("Usage: rows <start> <end> <values...>")

    start = parse_int(args[0], "start row")
    end = parse_int(args[1], "end row")
    values = args[2:]

    if start < 0 or start >= GRID_SIZE:
        die(f"Start row must be 0-{GRID_SIZE - 1}, got {start}")
    if end < start or end >= GRID_SIZE:
        die(f"End row must be {start}-{GRID_SIZE - 1}, got {end}")

    num_rows = end - start + 1
    expected = num_rows * GRID_SIZE
    if len(values) != expected:
        die(f"Expected {expected} values for {num_rows} rows ({start}-{end}), got {len(values)}")

    palette = load_palette(Path("palette.txt"))
    for i, v in enumerate(values):
        resolve_color(v, palette, f"position {i}")

    rows = load_grid(Path("grid.txt"))
    for i in range(num_rows):
        row_values = values[i * GRID_SIZE : (i + 1) * GRID_SIZE]
        rows[start + i] = row_values
    save_grid(Path("grid.txt"), rows)
    print(f"Rows {start}-{end} updated.")


def cmd_fill(args):
    if len(args) != 4:
        die("Usage: fill <row> <col_start> <col_end> <color>")

    row = parse_int(args[0], "row")
    col_start = parse_int(args[1], "col_start")
    col_end = parse_int(args[2], "col_end")
    color = args[3]

    if row < 0 or row >= GRID_SIZE:
        die(f"Row must be 0-{GRID_SIZE - 1}, got {row}")
    if col_start < 0 or col_start >= GRID_SIZE:
        die(f"col_start must be 0-{GRID_SIZE - 1}, got {col_start}")
    if col_end < col_start or col_end >= GRID_SIZE:
        die(f"col_end must be {col_start}-{GRID_SIZE - 1}, got {col_end}")

    palette = load_palette(Path("palette.txt"))
    resolve_color(color, palette, "fill color")

    rows = load_grid(Path("grid.txt"))
    for c in range(col_start, col_end + 1):
        rows[row][c] = color
    save_grid(Path("grid.txt"), rows)
    print(f"Row {row}, cols {col_start}-{col_end} filled with {color}.")


def cmd_rect(args):
    if len(args) != 5:
        die("Usage: rect <row_start> <col_start> <row_end> <col_end> <color>")

    r0 = parse_int(args[0], "row_start")
    c0 = parse_int(args[1], "col_start")
    r1 = parse_int(args[2], "row_end")
    c1 = parse_int(args[3], "col_end")
    color = args[4]

    if r0 < 0 or r0 >= GRID_SIZE:
        die(f"row_start must be 0-{GRID_SIZE - 1}, got {r0}")
    if r1 < r0 or r1 >= GRID_SIZE:
        die(f"row_end must be {r0}-{GRID_SIZE - 1}, got {r1}")
    if c0 < 0 or c0 >= GRID_SIZE:
        die(f"col_start must be 0-{GRID_SIZE - 1}, got {c0}")
    if c1 < c0 or c1 >= GRID_SIZE:
        die(f"col_end must be {c0}-{GRID_SIZE - 1}, got {c1}")

    palette = load_palette(Path("palette.txt"))
    resolve_color(color, palette, "rect color")

    rows = load_grid(Path("grid.txt"))
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            rows[r][c] = color
    save_grid(Path("grid.txt"), rows)
    print(f"Rect ({r0},{c0})-({r1},{c1}) filled with {color}.")


def cmd_export():
    from PIL import Image

    raw = load_grid(Path("grid.txt"))
    palette = load_palette(Path("palette.txt"))
    colors = resolve_grid(raw, palette)

    for scale in (1, 4, 8, 16):
        img = render_image(colors, scale, transparent_bg=True)
        name = "output.png" if scale == 1 else f"output_{scale}x.png"
        img.save(name)
        size = GRID_SIZE * scale
        print(f"Exported {name} ({size}x{size})")


def cmd_palette():
    palette_path = Path("palette.txt")
    if not palette_path.exists():
        die("No palette.txt found — run 'init' first")

    palette = load_palette(palette_path)
    entries = [(a, c) for a, c in palette.items() if a != TRANSPARENT]

    if not entries:
        print("Palette is empty. Add entries to palette.txt: ALIAS=#RRGGBB")
        return

    print("Current palette:")
    for alias, color in sorted(entries):
        print(f"  {alias} = {color if color else 'transparent'}")


def main():
    if len(sys.argv) < 2:
        print(__doc__.strip())
        sys.exit(1)

    cmd = sys.argv[1]
    args = sys.argv[2:]

    commands = {
        "init": lambda: cmd_init(),
        "render": lambda: cmd_render(),
        "show": lambda: cmd_render(),
        "row": lambda: cmd_row(args),
        "rows": lambda: cmd_rows(args),
        "fill": lambda: cmd_fill(args),
        "rect": lambda: cmd_rect(args),
        "export": lambda: cmd_export(),
        "palette": lambda: cmd_palette(),
    }

    if cmd not in commands:
        die(f"Unknown command: '{cmd}'\nValid: {', '.join(commands)}")

    commands[cmd]()


if __name__ == "__main__":
    main()
