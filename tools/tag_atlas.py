#!/usr/bin/env python3
"""Autonomous tile tagger — enriches index JSONs with descriptions, tags, and tile_type via Sonnet vision."""

import argparse
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime
from pathlib import Path
from collections import defaultdict

try:
    from PIL import Image
except ImportError:
    print("ERROR: Pillow is required. Install with: pip install Pillow")
    sys.exit(1)

# ─── Configuration ──────────────────────────────────────────────────────────

ROOT = Path(__file__).resolve().parent.parent  # Veil of Ages root
DCSS_DIR = ROOT / "assets" / "dcss"
KENNEY_DIR = ROOT / "assets" / "kenney"
URIZEN_DIR = ROOT / "assets" / "urizen"

DEFAULT_MODEL = "sonnet"  # Let claude CLI resolve the latest version

# Tag vocabulary: base from gridfab tagger + expansions
TAG_VOCABULARY = [
    # Structure
    "wall", "floor", "door", "window", "roof", "climbable",
    # Terrain & nature
    "terrain", "vegetation", "water",
    # Entities
    "character", "creature",
    # Objects
    "prop", "equipment", "container", "item",
    # Modifiers
    "corner", "damaged", "exterior",
    # Materials
    "wood", "stone", "metal", "dirt", "fabric", "crystal",
    # Other
    "hazard", "ui", "icon", "path", "overlay",
    # Dungeon features
    "altar", "stairs", "trap", "fountain", "statue", "pillar", "gate",
    "portal", "sigil", "rune",
    # Terrain (expanded)
    "lava", "tree", "cloud", "blood",
    # Entity types
    "undead", "demon", "dragon", "humanoid", "beast", "insect",
    "skeleton", "ghost", "spectral", "slime", "tentacle",
    # Items & equipment
    "weapon", "armour", "potion", "scroll", "wand", "ring", "amulet",
    "food", "gold", "book", "shield", "sword",
    # Combat & magic
    "spell", "effect", "corpse", "projectile", "brand",
    "fire", "ice", "poison", "sacred", "holy", "unholy", "shadow", "melee", "ranged",
    # General modifiers
    "magical", "unique", "boss", "player", "aquatic",
    # General expansions
    "interior", "furniture", "building", "fence", "bridge", "sign", "light",
    "chest", "barrel", "crate", "banner", "flag",
    "arachnid", "skull",
    "coin", "gem", "key", "heart",
    "animated",  # sprite is an animation frame (e.g. _walk_1, _idle), NOT "a living thing"
    "variant",   # sprite is a visual variant of another sprite (e.g. _2, _alt)
    "modern", "tech",
]

TILE_SCALE = 8  # 32px -> 256px for visibility


# ─── Verify Rules (composed into prompts per atlas) ─────────────────────────

DCSS_VERIFY_RULES = """\
DCSS-specific rules:
- Altar alignment: "sacred" on all altars. Add "holy" for Zin/Shining One/Elyvilon, \
"unholy" for Beogh/Jiyva/Kikubaaqudgha/Lugonu/Makhleb/Yredelemnul. Neutral gods: sacred only."""

URIZEN_VERIFY_RULES = """\
Urizen-specific rules:
- This is a 1-bit monochrome tileset. Do not reference colors in descriptions — describe shapes and forms instead.
- Focus on EXPANDING tags rather than correcting.
- Add material tags (wood, stone, metal, fabric, crystal) where the material is implied by the shape.
- Add functional tags: melee/ranged for weapons, humanoid/beast for creatures.
- Add context tags: interior/exterior, building, furniture where appropriate.
- "animated" means the sprite is an animation frame (name contains _walk_, _idle, _attack_, etc.). Do NOT use "animated" just because a sprite depicts a living creature.
- "variant" means the sprite is a visual variant of another (name ends in _2, _3, _alt, etc.). Do NOT use "variant" just because a creature has a color."""

KENNEY_VERIFY_RULES = """\
Kenney-specific rules:
- This is a manually tagged index. Focus on EXPANDING tags rather than correcting.
- Most sprites have too few tags. Add all relevant tags from the vocabulary.
- Add material tags (wood, stone, metal, fabric, crystal) where the material is visible.
- Add functional tags: melee/ranged for weapons, humanoid/beast for creatures.
- Add context tags: interior/exterior, building, furniture where appropriate.
- "animated" means the sprite is an animation frame (name contains _walk_, _idle, _fall_, _attack_, etc.). Do NOT use "animated" just because a sprite depicts a living creature.
- "variant" means the sprite is a visual variant of another (name ends in _2, _3, _alt, etc.). Do NOT use "variant" just because a creature has a color.
- DO NOT question what a sprite represents — many are ambiguous pixel art and the name is authoritative."""


# ─── Check Constants & Atlas-Specific Check Functions ───────────────────────

HOLY_GODS = {"zin", "shining_one", "elyvilon"}
UNHOLY_GODS = {"beogh", "jiyva", "kikubaaqudgha", "lugonu", "makhleb",
               "yredelemnul"}
CREATURE_STRIP_TAGS = {"armour", "equipment", "weapon", "player"}
ALTAR_STRIP_TAGS = {"weapon", "sword"}
TAG_SPELLING = {"gray": "grey", "armor": "armour"}
DESC_SPELLING = [
    (re.compile(r'\bgray\b', re.IGNORECASE), 'grey'),
    (re.compile(r'\barmor\b', re.IGNORECASE), 'armour'),
]
DIR_EXPECTED_TAGS = {
    "dungeon/doors": ["door"],
    "dungeon/altars": ["altar"],
    "dungeon/walls": ["wall"],
    "dungeon/floor": ["floor"],
    "monster/undead": ["undead"],
    "monster/demons": ["demon"],
    "monster/dragons": ["dragon"],
    "monster/insects": ["insect"],
    "item/weapon": ["weapon"],
    "item/armour": ["armour"],
    "item/potion": ["potion"],
    "item/scroll": ["scroll"],
    "item/wand": ["wand"],
    "item/ring": ["ring"],
    "item/amulet": ["amulet"],
    "item/book": ["book"],
    "item/food": ["food"],
    "item/gold": ["gold"],
    "player/": ["player"],
}


def check_dcss(enriched, issues, fix):
    """DCSS-specific checks: creature/altar tag stripping, altar alignment, directory tags."""
    fixes_applied = 0

    # --- Remove equipment/armour/weapon/player tags from creatures ---
    for key, info in enriched.items():
        if info["tile_type"] == "creature":
            removed = [t for t in info["tags"] if t in CREATURE_STRIP_TAGS]
            if removed:
                if fix:
                    info["tags"] = [t for t in info["tags"] if t not in CREATURE_STRIP_TAGS]
                    fixes_applied += 1
                    print(f"  FIX {key}: removed {removed} from creature")
                else:
                    issues.append(("CREATURE_EQUIP_TAG", key,
                                   f"creature has equipment tags: {removed}"))

    # --- Remove weapon/sword tags from altars ---
    for key, info in enriched.items():
        if info["tile_type"] == "altar":
            removed = [t for t in info["tags"] if t in ALTAR_STRIP_TAGS]
            if removed:
                if fix:
                    info["tags"] = [t for t in info["tags"] if t not in ALTAR_STRIP_TAGS]
                    fixes_applied += 1
                    print(f"  FIX {key}: removed {removed} from altar")
                else:
                    issues.append(("ALTAR_EQUIP_TAG", key,
                                   f"altar has equipment tags: {removed}"))

    # --- Altar sacred/holy/unholy alignment ---
    for key, info in enriched.items():
        norm = key.replace("\\", "/")
        if not norm.startswith("dungeon/altars"):
            continue
        tags = info["tags"]

        fname = norm.split("/")[-1].replace(".png", "")
        god_name = re.sub(r'^altar_?', '', fname)
        god_name = re.sub(r'_\d+$', '', god_name)
        god_base = god_name.split("_")[0] if god_name else ""

        should_holy = god_base in HOLY_GODS or god_name in HOLY_GODS
        should_unholy = god_base in UNHOLY_GODS or god_name in UNHOLY_GODS

        tag_fixes = []
        if "sacred" not in tags:
            tag_fixes.append(("add", "sacred"))
        if should_holy and "holy" not in tags:
            tag_fixes.append(("add", "holy"))
        if not should_holy and "holy" in tags:
            tag_fixes.append(("remove", "holy"))
        if should_unholy and "unholy" not in tags:
            tag_fixes.append(("add", "unholy"))
        if not should_unholy and "unholy" in tags:
            tag_fixes.append(("remove", "unholy"))

        if tag_fixes:
            if fix:
                for action, tag in tag_fixes:
                    if action == "add" and tag not in tags:
                        tags.append(tag)
                    elif action == "remove" and tag in tags:
                        tags.remove(tag)
                fixes_applied += 1
                desc = ", ".join(f"+{t}" if a == "add" else f"-{t}"
                                 for a, t in tag_fixes)
                print(f"  FIX {key}: {desc}")
            else:
                desc = ", ".join(f"+{t}" if a == "add" else f"-{t}"
                                 for a, t in tag_fixes)
                issues.append(("ALTAR_ALIGN", key, desc))

    # --- Directory prefix mismatches ---
    for key, info in enriched.items():
        norm = key.replace("\\", "/")
        tt = info.get("tile_type", "")
        for prefix, expected in DIR_EXPECTED_TAGS.items():
            if norm.startswith(prefix):
                missing = [t for t in expected if t not in info["tags"] and t != tt]
                if missing:
                    if fix:
                        for t in missing:
                            if t not in info["tags"]:
                                info["tags"].append(t)
                        fixes_applied += 1
                        print(f"  FIX {key}: added missing dir tags {missing}")
                    else:
                        issues.append(("DIR_MISMATCH", key,
                                       f"in {prefix}/ but missing tags: {missing}"))

    return fixes_applied


# ─── Atlas Registry ─────────────────────────────────────────────────────────

ATLASES = {
    "utumno": {
        "base_dir": DCSS_DIR,
        "atlas": "ProjectUtumno_full.png",
        "index": "dcss_utumno_index.json",
        "output": "dcss_utumno_index_tagged.json",
        "verify_rules": DCSS_VERIFY_RULES,
        "check_extra": check_dcss,
    },
    "supplemental": {
        "base_dir": DCSS_DIR,
        "atlas": "supplemental_atlas.png",
        "index": "dcss_supplemental_index.json",
        "output": "dcss_supplemental_index_tagged.json",
        "verify_rules": DCSS_VERIFY_RULES,
        "check_extra": check_dcss,
    },
    "combined": {
        "base_dir": DCSS_DIR,
        "atlas": "dcss_combined_atlas.png",
        "index": "dcss_combined_index.json",
        "output": "dcss_combined_index.json",
        "verify_rules": DCSS_VERIFY_RULES,
        "check_extra": check_dcss,
    },
    "urizen": {
        "base_dir": URIZEN_DIR,
        "atlas": "urizen_onebit_tileset__v2d0_32x32.png",
        "index": "urizen_onebit_tileset__v2d0_32x32_index.json",
        "output": "urizen_onebit_tileset__v2d0_32x32_index.json",
        "verify_rules": URIZEN_VERIFY_RULES,
        "check_extra": None,
    },
    "kenney": {
        "base_dir": KENNEY_DIR,
        "atlas": "colored-transparent_packed_2x.png",
        "index": "kenney_atlas_index.json",
        "output": "kenney_atlas_index.json",
        "verify_rules": KENNEY_VERIFY_RULES,
        "check_extra": None,
    },
}


# ─── Helpers ────────────────────────────────────────────────────────────────

def load_index(path: Path) -> dict:
    """Load an index JSON, returning the full structure."""
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_index(data: dict, path: Path):
    """Save index JSON with consistent formatting."""
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def backup_index(path: Path):
    """Create a timestamped backup of an index file before modification."""
    if not path.exists():
        return
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = path.with_suffix(f".{timestamp}.bak.json")
    shutil.copy2(path, backup)
    print(f"Backup: {backup.name}")


def get_sprite_image(sprite_key: str, sprite_info: dict, atlas_img: Image.Image | None,
                     base_dir: Path, tile_size: tuple[int, int]) -> Image.Image | None:
    """Get sprite image — try loose file first, fall back to atlas crop."""
    # Try loose file (index key is relative path with forward slashes)
    loose_path = base_dir / sprite_key.replace("/", "\\")
    if loose_path.exists():
        try:
            return Image.open(loose_path).convert("RGBA")
        except Exception:
            pass

    # Fall back to atlas crop
    if atlas_img is None:
        return None
    tw, th = tile_size
    row, col = sprite_info["row"], sprite_info["col"]
    tiles_x = sprite_info.get("tiles_x", 1)
    tiles_y = sprite_info.get("tiles_y", 1)
    x0 = col * tw
    y0 = row * th
    x1 = x0 + tiles_x * tw
    y1 = y0 + tiles_y * th
    try:
        return atlas_img.crop((x0, y0, x1, y1)).convert("RGBA")
    except Exception:
        return None


def upscale(img: Image.Image, scale: int = TILE_SCALE) -> Image.Image:
    """Nearest-neighbor upscale for pixel art visibility."""
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


def compose_batch_grid(images: list[Image.Image], names: list[str],
                       cols: int = 4) -> Image.Image:
    """Compose numbered sprite images into a grid with labels."""
    if not images:
        raise ValueError("No images to compose")

    rows = math.ceil(len(images) / cols)
    # Each cell: upscaled image + label space below
    cell_w = max(img.width for img in images)
    label_h = 20
    cell_h = max(img.height for img in images) + label_h

    grid = Image.new("RGBA", (cols * cell_w, rows * cell_h), (40, 40, 40, 255))

    for i, (img, name) in enumerate(zip(images, names)):
        r, c = divmod(i, cols)
        x = c * cell_w + (cell_w - img.width) // 2
        y = r * cell_h
        grid.paste(img, (x, y), img)

    return grid


def build_numbered_list(sprite_keys: list[str]) -> str:
    """Build a numbered list of sprite names with directory context."""
    lines = []
    for i, key in enumerate(sprite_keys):
        lines.append(f"{i+1}. {key}")
    return "\n".join(lines)


def parse_ai_response(stdout: str) -> list[dict] | None:
    """Parse Claude Code JSON output, extracting the sprite enrichment array."""
    # Parse outer Claude Code wrapper
    try:
        response = json.loads(stdout)
    except json.JSONDecodeError:
        print(f"  Outer JSON parse failed. stdout[:200]: {stdout[:200]}")
        return None

    # Extract text from Claude Code response
    text = ""
    if isinstance(response, dict):
        if "result" in response and response["result"]:
            text = response["result"]
        elif "content" in response:
            content = response["content"]
            if isinstance(content, list):
                text = " ".join(
                    block.get("text", "")
                    for block in content
                    if isinstance(block, dict) and block.get("type") == "text"
                )
            elif isinstance(content, str):
                text = content
        if not text:
            for key in ("text", "message", "output"):
                if key in response and isinstance(response[key], str):
                    text = response[key]
                    break

    if not text:
        print(f"  No extractable text. Keys: {list(response.keys()) if isinstance(response, dict) else type(response)}")
        return None

    text = text.strip()

    # Try direct parse
    try:
        parsed = json.loads(text)
        if isinstance(parsed, list):
            return parsed
    except json.JSONDecodeError:
        pass

    # Strip markdown fences
    if "```" in text:
        fence_match = re.search(r'```(?:json)?\s*\n?(.*?)\n?\s*```', text, re.DOTALL)
        if fence_match:
            try:
                parsed = json.loads(fence_match.group(1).strip())
                if isinstance(parsed, list):
                    return parsed
            except json.JSONDecodeError:
                pass

    # Last resort: find JSON array in text
    arr_match = re.search(r'\[[\s\S]*\]', text)
    if arr_match:
        try:
            parsed = json.loads(arr_match.group(0))
            if isinstance(parsed, list):
                return parsed
        except json.JSONDecodeError:
            pass

    print(f"  Could not parse array from AI response: {text[:300]}")
    return None


def is_enriched(sprite_info: dict) -> bool:
    """Check if a sprite already has tags/description/tile_type."""
    return bool(sprite_info.get("description") and sprite_info.get("tags") and sprite_info.get("tile_type"))


def group_by_prefix(sprites: dict) -> dict[str, list[str]]:
    """Group sprite keys by their directory prefix."""
    groups = defaultdict(list)
    for key in sprites:
        # Use first two path segments as group, or first one if only one level
        parts = key.replace("\\", "/").split("/")
        if len(parts) >= 3:
            prefix = "/".join(parts[:2])
        elif len(parts) >= 2:
            prefix = parts[0]
        else:
            prefix = "_root"
        groups[prefix].append(key)
    return dict(groups)


# ─── Grid Text Conversion (copied from gridfab import_cmd.py) ────────────────

def _pad_cell(value: str) -> str:
    """Pad a cell value to 2 chars with '.' for aligned output."""
    return value.ljust(2, ".")


def generate_alias_sequence():
    """Yield alias strings: A-Z, 0-9, AA-ZZ (712 total)."""
    for c in range(ord("A"), ord("Z") + 1):
        yield chr(c)
    for d in range(10):
        yield str(d)
    for c1 in range(ord("A"), ord("Z") + 1):
        for c2 in range(ord("A"), ord("Z") + 1):
            yield chr(c1) + chr(c2)


def image_to_grid_and_palette(
    pil_image: Image.Image,
    alpha_threshold: int = 128,
    existing_colors: dict[str, str] | None = None,
) -> tuple[list[list[str]], dict[str, str]]:
    """Convert a PIL Image to grid data and palette entries.

    Args:
        pil_image: PIL Image (any mode — converted to RGBA internally).
        alpha_threshold: Pixels with alpha < this value become transparent.
        existing_colors: Optional dict mapping hex color → alias to reuse.

    Returns:
        (grid_data, palette_entries) where grid_data is list[list[str]]
        and palette_entries is dict[alias, hex_color].
    """
    if pil_image.mode != "RGBA":
        pil_image = pil_image.convert("RGBA")

    width, height = pil_image.size

    color_to_alias: dict[str, str] = {}
    if existing_colors:
        for hex_color, alias in existing_colors.items():
            color_to_alias[hex_color.upper()] = alias

    alias_gen = generate_alias_sequence()
    used_aliases = set(color_to_alias.values()) if existing_colors else set()

    grid_data: list[list[str]] = []
    for y in range(height):
        row: list[str] = []
        for x in range(width):
            r, g, b, a = pil_image.getpixel((x, y))
            if a < alpha_threshold:
                row.append(".")
            else:
                hex_color = f"#{r:02X}{g:02X}{b:02X}"
                if hex_color in color_to_alias:
                    row.append(color_to_alias[hex_color])
                else:
                    alias = next(alias_gen)
                    while alias in used_aliases:
                        alias = next(alias_gen)
                    used_aliases.add(alias)
                    color_to_alias[hex_color] = alias
                    row.append(alias)
        grid_data.append(row)

    palette_entries: dict[str, str] = {}
    for hex_color, alias in color_to_alias.items():
        palette_entries[alias] = hex_color

    return grid_data, palette_entries


def batch_to_text(keys: list[str], images: list[Image.Image],
                  alpha_threshold: int = 128) -> str:
    """Convert a batch of sprite images to a combined grid.txt/palette.txt text.

    Uses a shared palette across all sprites so aliases are reused.

    Args:
        keys: Sprite key names (e.g. "dungeon/door_closed.png").
        images: Raw (non-upscaled) PIL images, one per key.
        alpha_threshold: Pixels with alpha < this become transparent.

    Returns:
        Formatted text with shared palette header + per-sprite grid blocks.
    """
    color_map: dict[str, str] = {}  # hex → alias, accumulated across sprites
    grids: list[list[list[str]]] = []

    for img in images:
        grid_data, palette_entries = image_to_grid_and_palette(
            img, alpha_threshold, existing_colors=color_map,
        )
        grids.append(grid_data)
        # Accumulate new colors into shared map
        for alias, hex_color in palette_entries.items():
            if hex_color not in color_map:
                color_map[hex_color] = alias

    # Build output
    lines = ["palette.txt:"]
    for hex_color, alias in sorted(color_map.items(), key=lambda x: x[1]):
        lines.append(f"{alias}={hex_color}")
    lines.append("")

    for i, (key, grid_data) in enumerate(zip(keys, grids)):
        lines.append(f"--- {i+1}. {key} ---")
        for row in grid_data:
            lines.append(" ".join(_pad_cell(v) for v in row))
        lines.append("")

    return "\n".join(lines)


# ─── Main Processing ────────────────────────────────────────────────────────

def process_atlas(atlas_name: str, dcss_dir: Path | None = None,
                  batch_size: int = 12, dry_run: bool = False, limit: int = 0,
                  args_model: str = DEFAULT_MODEL):
    """Process a single atlas: enrich all indexed sprites."""
    config = ATLASES[atlas_name]
    base_dir = dcss_dir or config.get("base_dir", DCSS_DIR)
    index_path = base_dir / config["index"]
    output_path = base_dir / config["output"]
    atlas_path = base_dir / config["atlas"]
    atlas_rules = config.get("verify_rules", "")

    if not index_path.exists():
        print(f"ERROR: Index not found: {index_path}")
        return

    # Load index — prefer existing output (for resume) over original
    if output_path.exists():
        print(f"Resuming from existing output: {output_path.name}")
        data = load_index(output_path)
    else:
        data = load_index(index_path)

    sprites = data["sprites"]
    tile_size = tuple(data.get("tile_size", [32, 32]))

    # Load atlas lazily (only if needed for fallback crops)
    atlas_img = None

    # Find sprites that need enrichment
    todo = [key for key in sprites if not is_enriched(sprites[key])]
    done_count = len(sprites) - len(todo)
    total = len(sprites)

    print(f"\n{'='*60}")
    print(f"Atlas: {atlas_name} ({config['atlas']})")
    print(f"Total sprites: {total}")
    print(f"Already enriched: {done_count}")
    print(f"Remaining: {len(todo)}")
    print(f"Batch size: {batch_size}")
    print(f"Estimated batches: {math.ceil(len(todo) / batch_size)}")
    print(f"{'='*60}\n")

    if not todo:
        print("All sprites already enriched!")
        return

    # Group by prefix for contextual batching
    groups = group_by_prefix({k: sprites[k] for k in todo})
    ordered_keys = []
    for prefix in sorted(groups.keys()):
        ordered_keys.extend(sorted(groups[prefix]))

    if limit:
        ordered_keys = ordered_keys[:limit * batch_size]
        print(f"Limiting to {limit} batch(es) ({len(ordered_keys)} sprites)\n")

    if dry_run:
        print("DRY RUN — showing batches:\n")
        for batch_idx in range(0, len(ordered_keys), batch_size):
            batch = ordered_keys[batch_idx:batch_idx + batch_size]
            print(f"Batch {batch_idx // batch_size + 1} ({len(batch)} sprites):")
            for key in batch:
                print(f"  {key}")
            print()
        return

    # Check claude CLI is available
    if not shutil.which("claude"):
        print("ERROR: 'claude' CLI not found. Install Claude Code first.")
        return

    # Create temp dir for batch images
    temp_dir = Path(tempfile.mkdtemp(prefix="dcss_tagger_"))
    print(f"Temp dir: {temp_dir}")

    try:
        batch_num = 0
        for batch_start in range(0, len(ordered_keys), batch_size):
            batch_keys = ordered_keys[batch_start:batch_start + batch_size]
            batch_num += 1
            total_batches = math.ceil(len(ordered_keys) / batch_size)

            print(f"\n--- Batch {batch_num}/{total_batches} ({len(batch_keys)} sprites) ---")

            # Collect images (upscaled for visual grid, raw for text conversion)
            images = []
            raw_images = []
            valid_keys = []
            for key in batch_keys:
                img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)
                if img is None and atlas_img is None:
                    # Lazy-load atlas
                    print(f"  Loading atlas image: {atlas_path.name}")
                    atlas_img = Image.open(atlas_path).convert("RGBA")
                    img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)

                if img is not None:
                    raw_images.append(img)
                    images.append(upscale(img))
                    valid_keys.append(key)
                else:
                    print(f"  WARNING: Could not load image for {key}")

            if not valid_keys:
                print("  No valid images in batch, skipping")
                continue

            # Compose grid and save
            grid_cols = 4 if len(images) > 3 else len(images)
            grid_img = compose_batch_grid(images, valid_keys, cols=grid_cols)
            grid_path = temp_dir / "batch_grid.png"
            grid_img.save(grid_path)

            # Generate grid.txt/palette.txt text representation
            batch_text = batch_to_text(valid_keys, raw_images)
            text_path = temp_dir / "batch_text.txt"
            text_path.write_text(batch_text, encoding="utf-8", newline="\n")

            # Build prompt
            numbered = build_numbered_list(valid_keys)
            tags_str = ", ".join(TAG_VOCABULARY)

            prompt = f"""Tag pixel art sprites. Read batch_grid.png for the visual grid, then read batch_text.txt for the pixel-level grid.txt/palette.txt representation of each sprite (exact colors per pixel).

{numbered}

For EACH sprite return:
- "description": One sentence with specific visual details (colors, pose, style). Filenames are ground truth for what the sprite depicts; use the image for visual details.
- "tags": 3-6 tags from vocabulary below. You may add 1-2 custom tags if useful for search. Do NOT include tile_type in tags.
- "tile_type": Single category (e.g. "creature", "wall", "door", "altar", "equipment").

Tags/descriptions are for AI agents searching for tiles — use terms they would naturally search for.
{atlas_rules}
Vocabulary: [{tags_str}]

Respond ONLY with a JSON array, one object per sprite in order:
[{{"description": "...", "tags": [...], "tile_type": "..."}}]"""

            # Print first few sprite names for progress visibility
            preview = ", ".join(k.split("/")[-1] for k in valid_keys[:3])
            if len(valid_keys) > 3:
                preview += f", ... +{len(valid_keys)-3} more"
            print(f"  Sprites: {preview}")

            # Strip nesting-detection env vars so claude CLI doesn't refuse
            clean_env = {k: v for k, v in os.environ.items()
                         if not k.startswith("CLAUDECODE")}

            enrichments = None
            max_retries = 3
            for attempt in range(max_retries):
                if attempt > 0:
                    print(f"  Retry {attempt}/{max_retries - 1}...")

                try:
                    result = subprocess.run(
                        ["claude", "-p", prompt,
                         "--model", args_model,
                         "--output-format", "json",
                         "--allowedTools", "Read"],
                        cwd=temp_dir,
                        capture_output=True, text=True,
                        timeout=120,
                        env=clean_env,
                    )
                except subprocess.TimeoutExpired:
                    print(f"  API call timed out (attempt {attempt + 1})")
                    continue
                except Exception as e:
                    print(f"  Subprocess error: {e}")
                    continue

                if result.returncode != 0:
                    print(f"  Non-zero exit ({result.returncode})")
                    if result.stderr:
                        print(f"  stderr: {result.stderr[:200]}")
                    continue

                enrichments = parse_ai_response(result.stdout)
                if enrichments is not None:
                    break
                print(f"  Failed to parse response (attempt {attempt + 1})")

            if enrichments is None:
                print("  All retries exhausted, skipping batch")
                continue

            if len(enrichments) != len(valid_keys):
                print(f"  WARNING: Got {len(enrichments)} results for {len(valid_keys)} sprites")

            # Merge enrichments into index
            applied = 0
            for i, key in enumerate(valid_keys):
                if i >= len(enrichments):
                    break
                entry = enrichments[i]
                if not isinstance(entry, dict):
                    continue
                if entry.get("description"):
                    sprites[key]["description"] = entry["description"]
                if entry.get("tags") and isinstance(entry["tags"], list):
                    sprites[key]["tags"] = entry["tags"]
                if entry.get("tile_type"):
                    sprites[key]["tile_type"] = entry["tile_type"]
                # Strip tile_type from tags if it snuck in
                tt = sprites[key].get("tile_type")
                tags = sprites[key].get("tags", [])
                if tt and tt in tags:
                    tags.remove(tt)
                    sprites[key]["tags"] = tags
                applied += 1

            print(f"  Applied {applied}/{len(valid_keys)} enrichments")

            # Save after every batch for resume
            save_index(data, output_path)
            done_count += applied
            remaining = total - done_count
            print(f"  Progress: {done_count}/{total} ({remaining} remaining)")

    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)

    print(f"\nDone! Output saved to: {output_path}")


def verify_atlas(atlas_name: str, dcss_dir: Path | None = None,
                 batch_size: int = 36, dry_run: bool = False, limit: int = 0,
                 args_model: str = DEFAULT_MODEL):
    """Verify previously tagged sprites — flag incorrect descriptions/tags."""
    config = ATLASES[atlas_name]
    base_dir = dcss_dir or config.get("base_dir", DCSS_DIR)
    output_path = base_dir / config["output"]
    atlas_path = base_dir / config["atlas"]
    atlas_rules = config.get("verify_rules", "")

    if not output_path.exists():
        print(f"ERROR: No tagged output to verify: {output_path}")
        return

    data = load_index(output_path)
    sprites = data["sprites"]
    tile_size = tuple(data.get("tile_size", [32, 32]))
    atlas_img = None

    # Verify sprites that have at least description + tile_type (tags may be empty after check --fix)
    enriched_keys = [k for k in sprites if sprites[k].get("description") and sprites[k].get("tile_type")]
    total = len(enriched_keys)

    print(f"\n{'='*60}")
    print(f"VERIFY: {atlas_name} ({config['output']})")
    print(f"Enriched sprites to verify: {total}")
    print(f"Batch size: {batch_size}")
    print(f"Estimated batches: {math.ceil(total / batch_size)}")
    print(f"{'='*60}\n")

    if not enriched_keys:
        print("Nothing to verify!")
        return

    # Sort for consistent ordering
    enriched_keys.sort()

    if limit:
        enriched_keys = enriched_keys[:limit * batch_size]
        print(f"Limiting to {limit} batch(es) ({len(enriched_keys)} sprites)\n")

    if dry_run:
        print(f"DRY RUN — would verify {len(enriched_keys)} sprites in "
              f"{math.ceil(len(enriched_keys) / batch_size)} batches\n")
        return

    if not shutil.which("claude"):
        print("ERROR: 'claude' CLI not found.")
        return

    # Backup before modifying
    backup_index(output_path)

    temp_dir = Path(tempfile.mkdtemp(prefix="dcss_verify_"))
    tags_str = ", ".join(TAG_VOCABULARY)
    fixes_total = 0

    # Log fixes to a file in the current working directory
    log_path = Path.cwd() / f"verify_{atlas_name}.log"
    log_file = open(log_path, "a", encoding="utf-8", newline="\n", buffering=1)
    log_file.write(f"\n{'='*60}\n")
    log_file.write(f"Verify run: {atlas_name}\n")
    log_file.write(f"{'='*60}\n\n")
    print(f"Logging fixes to: {log_path}")

    try:
        for batch_start in range(0, len(enriched_keys), batch_size):
            batch_keys = enriched_keys[batch_start:batch_start + batch_size]
            batch_num = batch_start // batch_size + 1
            total_batches = math.ceil(len(enriched_keys) / batch_size)

            print(f"\n--- Verify batch {batch_num}/{total_batches} ({len(batch_keys)} sprites) ---")

            # Collect images (upscaled for visual grid, raw for text conversion)
            images = []
            raw_images = []
            valid_keys = []
            for key in batch_keys:
                img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)
                if img is None and atlas_img is None:
                    print(f"  Loading atlas image: {atlas_path.name}")
                    atlas_img = Image.open(atlas_path).convert("RGBA")
                    img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)
                if img is not None:
                    raw_images.append(img)
                    images.append(upscale(img))
                    valid_keys.append(key)

            if not valid_keys:
                continue

            grid_cols = 6 if len(images) > 6 else len(images)
            grid_img = compose_batch_grid(images, valid_keys, cols=grid_cols)
            grid_path = temp_dir / "verify_grid.png"
            grid_img.save(grid_path)

            # Generate grid.txt/palette.txt text representation
            batch_text = batch_to_text(valid_keys, raw_images)
            text_path = temp_dir / "batch_text.txt"
            text_path.write_text(batch_text, encoding="utf-8", newline="\n")

            # Build numbered list with current tags
            lines = []
            for i, key in enumerate(valid_keys):
                info = sprites[key]
                tags = ", ".join(info.get("tags", []))
                lines.append(f"{i+1}. {key}\n"
                             f"   type: {info.get('tile_type', '?')}\n"
                             f"   tags: [{tags}]\n"
                             f"   desc: {info.get('description', '?')}")
            numbered_with_tags = "\n".join(lines)

            prompt = f"""Review sprite tags. Read verify_grid.png for the visual grid, then read batch_text.txt for the pixel-level grid.txt/palette.txt representation of each sprite (exact colors per pixel). Check sprites below.

{numbered_with_tags}

Rules:
- Filenames/names are ground truth. Never contradict them.
- Only fix CLEARLY WRONG descriptions. Do not reword acceptable descriptions.
- Do not change tile_type unless clearly wrong.
- Preserve custom tags (tags not in the vocabulary). Only remove a custom tag if it is factually wrong.
- Do not include tile_type in tags — they are separate fields.
- Expand sparse tags: if a sprite has fewer than 3 tags, add relevant ones from the vocabulary.
- Vocabulary: [{tags_str}]

{atlas_rules}

CRITICAL: Output ONLY a raw JSON array — no explanation, no reasoning, no markdown fences.
Include ONLY sprites needing fixes. Use 1-indexed "index". If all OK return [].
Example: [{{"index": 3, "description": "...", "tags": [...], "tile_type": "..."}}]"""

            preview = ", ".join(k.split("/")[-1] for k in valid_keys[:3])
            if len(valid_keys) > 3:
                preview += f", ... +{len(valid_keys)-3} more"
            print(f"  Sprites: {preview}")

            clean_env = {k: v for k, v in os.environ.items()
                         if not k.startswith("CLAUDECODE")}

            fixes = None
            max_retries = 3
            for attempt in range(max_retries):
                if attempt > 0:
                    print(f"  Retry {attempt}/{max_retries - 1}...")

                try:
                    result = subprocess.run(
                        ["claude", "-p", prompt,
                         "--model", args_model,
                         "--output-format", "json",
                         "--allowedTools", "Read"],
                        cwd=temp_dir,
                        capture_output=True, text=True,
                        timeout=180,
                        env=clean_env,
                    )
                except subprocess.TimeoutExpired:
                    print(f"  Verify call timed out (attempt {attempt + 1})")
                    continue
                except Exception as e:
                    print(f"  Subprocess error: {e}")
                    continue

                if result.returncode != 0:
                    print(f"  Non-zero exit ({result.returncode})")
                    if result.stderr:
                        print(f"  stderr: {result.stderr[:200]}")
                    continue

                fixes = parse_ai_response(result.stdout)
                if fixes is not None:
                    break
                print(f"  Failed to parse response (attempt {attempt + 1})")

            if fixes is None:
                print("  All retries exhausted — logging full response")
                log_file.write(f"\n{'!'*60}\n")
                log_file.write(f"PARSE_FAILURE batch {batch_num} "
                               f"({len(valid_keys)} sprites)\n")
                log_file.write(f"Sprites: {', '.join(valid_keys)}\n")
                log_file.write(f"{'!'*60}\n")
                try:
                    resp = json.loads(result.stdout)
                    text = ""
                    if isinstance(resp, dict):
                        text = resp.get("result", "") or ""
                        if not text:
                            content = resp.get("content", "")
                            if isinstance(content, list):
                                text = " ".join(
                                    b.get("text", "") for b in content
                                    if isinstance(b, dict) and b.get("type") == "text"
                                )
                            elif isinstance(content, str):
                                text = content
                    log_file.write(text or result.stdout)
                except Exception:
                    log_file.write(result.stdout)
                log_file.write(f"\n{'!'*60}\n\n")
                log_file.flush()
                os.fsync(log_file.fileno())
                continue

            if not fixes:
                print("  All OK")
                continue

            applied = 0
            for fix in fixes:
                if not isinstance(fix, dict) or "index" not in fix:
                    continue
                idx = fix["index"] - 1  # Convert to 0-indexed
                if idx < 0 or idx >= len(valid_keys):
                    continue
                key = valid_keys[idx]
                old = {
                    "description": sprites[key].get("description", ""),
                    "tags": list(sprites[key].get("tags", [])),
                    "tile_type": sprites[key].get("tile_type", ""),
                }
                # Apply fixes, only track fields that actually changed
                if fix.get("description") and fix["description"] != old["description"]:
                    sprites[key]["description"] = fix["description"]
                if fix.get("tags") and isinstance(fix["tags"], list):
                    sprites[key]["tags"] = fix["tags"]
                if fix.get("tile_type") and fix["tile_type"] != old["tile_type"]:
                    sprites[key]["tile_type"] = fix["tile_type"]
                # Strip tile_type from tags if it snuck in
                tt = sprites[key].get("tile_type")
                tags = sprites[key].get("tags", [])
                if tt and tt in tags:
                    tags.remove(tt)
                    sprites[key]["tags"] = tags
                # Determine what actually changed
                changed = []
                if sprites[key].get("description") != old["description"]:
                    changed.append("desc")
                if sprites[key].get("tags") != old["tags"]:
                    changed.append("tags")
                if sprites[key].get("tile_type") != old["tile_type"]:
                    changed.append("type")
                if changed:
                    applied += 1
                    print(f"  FIX {key}: {', '.join(changed)}")
                    log_file.write(f"FIX: {key}\n")
                    for field in changed:
                        full = {"desc": "description", "tags": "tags", "type": "tile_type"}[field]
                        log_file.write(f"  {full}:\n")
                        log_file.write(f"    BEFORE: {old[full]}\n")
                        log_file.write(f"    AFTER:  {sprites[key][full]}\n")
                    log_file.write("\n")
                    log_file.flush()
                    os.fsync(log_file.fileno())

            fixes_total += applied
            print(f"  Fixed {applied} sprites in this batch")
            save_index(data, output_path)

    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)
        log_file.write(f"Total fixes: {fixes_total}\n")
        log_file.close()

    print(f"\nVerification done! {fixes_total} total fixes applied.")
    print(f"Fix log: {log_path}")
    print(f"Output: {output_path}")


def check_atlas(atlas_name: str, dcss_dir: Path | None = None, fix: bool = False):
    """Run local consistency checks on tagged output — no API calls needed.
    With fix=True, auto-fix mechanical issues (tile_type in tags, spelling, etc.)."""
    config = ATLASES[atlas_name]
    base_dir = dcss_dir or config.get("base_dir", DCSS_DIR)
    output_path = base_dir / config["output"]

    if not output_path.exists():
        print(f"ERROR: No tagged output to check: {output_path}")
        return

    # Backup before modifying
    if fix:
        backup_index(output_path)

    data = load_index(output_path)
    sprites = data["sprites"]
    enriched = {k: v for k, v in sprites.items() if is_enriched(v)}

    mode = "FIX" if fix else "CHECK"
    print(f"\n{'='*60}")
    print(f"{mode}: {atlas_name} ({len(enriched)}/{len(sprites)} enriched)")
    print(f"{'='*60}\n")

    if not enriched:
        print("Nothing to check!")
        return

    issues = []
    fixes_applied = 0

    # ─── Base checks (all atlases) ──────────────────────────────────────

    # --- Strip tile_type from tags ---
    for key, info in enriched.items():
        tt = info["tile_type"]
        if tt and tt in info["tags"]:
            if fix:
                info["tags"].remove(tt)
                fixes_applied += 1
                print(f"  FIX {key}: removed tile_type {tt!r} from tags")
            else:
                issues.append(("TILE_TYPE_IN_TAGS", key,
                               f"tile_type {tt!r} redundantly in tags"))

    # --- Spelling normalization in tags ---
    for key, info in enriched.items():
        tags = info["tags"]
        new_tags = [TAG_SPELLING.get(t, t) for t in tags]
        if new_tags != tags:
            if fix:
                old_tags = list(tags)
                info["tags"] = new_tags
                changed = [f"{o}->{n}" for o, n in zip(old_tags, new_tags) if o != n]
                fixes_applied += 1
                print(f"  FIX {key}: tag spelling {', '.join(changed)}")
            else:
                americanisms = [t for t in tags if t in TAG_SPELLING]
                issues.append(("SPELLING", key,
                               f"American spelling in tags: {americanisms}"))

    # --- Spelling normalization in descriptions ---
    for key, info in enriched.items():
        desc = info["description"]
        new_desc = desc
        for pattern, repl in DESC_SPELLING:
            new_desc = pattern.sub(repl, new_desc)
        if new_desc != desc:
            if fix:
                info["description"] = new_desc
                fixes_applied += 1
                print(f"  FIX {key}: description spelling (grey/armour)")
            else:
                issues.append(("SPELLING_DESC", key,
                               "American spelling in description"))

    # ─── Atlas-specific checks ──────────────────────────────────────────

    check_extra = config.get("check_extra")
    if check_extra:
        fixes_applied += check_extra(enriched, issues, fix)

    # ─── Report-only checks (all atlases) ───────────────────────────────

    # --- Tag count ---
    for key, info in enriched.items():
        n = len(info["tags"])
        if n < 3:
            issues.append(("FEW_TAGS", key, f"only {n} tags: {info['tags']}"))
        elif n > 10:
            issues.append(("MANY_TAGS", key, f"{n} tags: {info['tags']}"))

    # --- Short / long descriptions ---
    for key, info in enriched.items():
        desc = info["description"]
        if len(desc) < 30:
            issues.append(("SHORT_DESC", key, f"({len(desc)} chars) {desc!r}"))
        elif len(desc) > 300:
            issues.append(("LONG_DESC", key, f"({len(desc)} chars) {desc[:80]}..."))

    # --- Duplicate descriptions ---
    desc_map = defaultdict(list)
    for key, info in enriched.items():
        desc_map[info["description"]].append(key)
    for desc, keys in desc_map.items():
        if len(keys) > 1:
            bases = set()
            for k in keys:
                name = k.split("/")[-1].replace(".png", "")
                base = re.sub(r'_\d+$', '', name)
                bases.add(base)
            if len(bases) > 1:
                issues.append(("DUPE_DESC", ", ".join(keys),
                               f"identical description: {desc[:80]}..."))

    # --- Save if we fixed anything ---
    if fix and fixes_applied:
        save_index(data, output_path)
        print(f"\nApplied {fixes_applied} fixes. Saved to {output_path.name}")

    # --- Report remaining issues ---
    if not issues:
        if not fix:
            print("All checks passed!")
        elif fixes_applied:
            print("No remaining issues.")
        return

    by_cat = defaultdict(list)
    for cat, key, detail in issues:
        by_cat[cat].append((key, detail))

    cat_labels = {
        "FEW_TAGS": "Too few tags (< 3)",
        "MANY_TAGS": "Too many tags (> 10)",
        "TILE_TYPE_IN_TAGS": "tile_type redundantly in tags",
        "CREATURE_EQUIP_TAG": "Equipment tags on creatures",
        "ALTAR_EQUIP_TAG": "Equipment tags on altars",
        "ALTAR_ALIGN": "Altar sacred/holy/unholy alignment",
        "DIR_MISMATCH": "Directory/tag mismatch",
        "SPELLING": "American spelling in tags",
        "SPELLING_DESC": "American spelling in descriptions",
        "SHORT_DESC": "Short description (< 30 chars)",
        "LONG_DESC": "Long description (> 300 chars)",
        "DUPE_DESC": "Duplicate descriptions (possible misalignment)",
    }

    cat_order = [
        "DUPE_DESC", "TILE_TYPE_IN_TAGS",
        "CREATURE_EQUIP_TAG", "ALTAR_EQUIP_TAG", "ALTAR_ALIGN", "DIR_MISMATCH",
        "SPELLING", "SPELLING_DESC",
        "FEW_TAGS", "MANY_TAGS", "SHORT_DESC", "LONG_DESC",
    ]

    total_issues = len(issues)
    print(f"\n{total_issues} remaining issues:\n")
    seen = set()
    for cat in cat_order:
        items = by_cat.get(cat, [])
        if not items:
            continue
        seen.add(cat)
        label = cat_labels.get(cat, cat)
        print(f"--- {label} ({len(items)}) ---")
        for key, detail in items[:20]:
            print(f"  {key}: {detail}")
        if len(items) > 20:
            print(f"  ... and {len(items) - 20} more")
        print()
    # Report any categories not in the predefined order
    for cat in by_cat:
        if cat not in seen:
            items = by_cat[cat]
            label = cat_labels.get(cat, cat)
            print(f"--- {label} ({len(items)}) ---")
            for key, detail in items[:20]:
                print(f"  {key}: {detail}")
            if len(items) > 20:
                print(f"  ... and {len(items) - 20} more")
            print()


def report_unindexed(dcss_dir: Path):
    """Find non-empty tiles in the main atlas that aren't in either index."""
    atlas_path = dcss_dir / "ProjectUtumno_full.png"
    utumno_index_path = dcss_dir / "dcss_utumno_index.json"
    supp_index_path = dcss_dir / "dcss_supplemental_index.json"

    if not atlas_path.exists():
        print("ERROR: Main atlas not found")
        return

    print("\nScanning main atlas for unindexed tiles...")

    atlas = Image.open(atlas_path).convert("RGBA")
    utumno_data = load_index(utumno_index_path)
    tile_size = tuple(utumno_data.get("tile_size", [32, 32]))
    tw, th = tile_size
    columns = utumno_data.get("columns", 64)

    total_cols = atlas.width // tw
    total_rows = atlas.height // th

    # Build set of all indexed positions (from both indices)
    indexed = set()
    for sprite in utumno_data["sprites"].values():
        r, c = sprite["row"], sprite["col"]
        tx, ty = sprite.get("tiles_x", 1), sprite.get("tiles_y", 1)
        for dr in range(ty):
            for dc in range(tx):
                indexed.add((r + dr, c + dc))

    if supp_index_path.exists():
        supp_data = load_index(supp_index_path)
        supp_names = set(supp_data["sprites"].keys())
    else:
        supp_names = set()

    # Scan all tiles for non-empty ones
    unindexed = []
    for row in range(total_rows):
        for col in range(total_cols):
            if (row, col) in indexed:
                continue
            # Check if tile is non-empty (has any non-transparent pixel)
            x0, y0 = col * tw, row * th
            tile = atlas.crop((x0, y0, x0 + tw, y0 + th))
            if tile.getbbox() is not None:  # Has visible pixels
                unindexed.append({"row": row, "col": col})

    output_path = dcss_dir / "unindexed_tiles.json"
    report = {
        "atlas": "ProjectUtumno_full.png",
        "tile_size": list(tile_size),
        "total_atlas_tiles": total_rows * total_cols,
        "indexed_tile_positions": len(indexed),
        "supplemental_sprite_names": len(supp_names),
        "unindexed_nonempty_tiles": len(unindexed),
        "tiles": unindexed,
    }
    save_index(report, output_path)
    print(f"Found {len(unindexed)} unindexed non-empty tiles")
    print(f"Report saved to: {output_path}")


def show_text_atlas(atlas_name: str, dcss_dir: Path | None = None,
                    batch_size: int = 12, limit: int = 1):
    """Generate and print batch text for one or more batches (no API calls)."""
    config = ATLASES[atlas_name]
    base_dir = dcss_dir or config.get("base_dir", DCSS_DIR)
    index_path = base_dir / config["index"]
    atlas_path = base_dir / config["atlas"]

    if not index_path.exists():
        print(f"ERROR: Index not found: {index_path}")
        return

    # Prefer tagged output for verify-like usage
    output_path = base_dir / config["output"]
    if output_path.exists():
        data = load_index(output_path)
    else:
        data = load_index(index_path)

    sprites = data["sprites"]
    tile_size = tuple(data.get("tile_size", [32, 32]))
    atlas_img = None

    # Use all sprites (not just unenriched)
    all_keys = sorted(sprites.keys())
    all_keys = all_keys[:limit * batch_size]

    print(f"\n{'='*60}")
    print(f"SHOW TEXT: {atlas_name} — {len(all_keys)} sprites")
    print(f"{'='*60}\n")

    for batch_start in range(0, len(all_keys), batch_size):
        batch_keys = all_keys[batch_start:batch_start + batch_size]

        raw_images = []
        valid_keys = []
        for key in batch_keys:
            img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)
            if img is None and atlas_img is None:
                atlas_img = Image.open(atlas_path).convert("RGBA")
                img = get_sprite_image(key, sprites[key], atlas_img, base_dir, tile_size)
            if img is not None:
                raw_images.append(img)
                valid_keys.append(key)

        if not valid_keys:
            print("No valid images found")
            continue

        text = batch_to_text(valid_keys, raw_images)
        print(text)


# ─── CLI ────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="Autonomously tag tileset sprites using Sonnet vision"
    )
    parser.add_argument(
        "--atlas", choices=["utumno", "supplemental", "combined", "kenney", "urizen", "both"],
        default="both",
        help="Which atlas to process (default: both = utumno+supplemental)"
    )
    parser.add_argument(
        "--batch-size", type=int, default=12,
        help="Sprites per API call (default: 12)"
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Show batches without making API calls"
    )
    parser.add_argument(
        "--limit", type=int, default=0,
        help="Only process this many batches (0 = all). Use --limit 1 to test a single batch."
    )
    parser.add_argument(
        "--dcss-dir", type=str, default=None,
        help=f"Override base directory for atlas files (default per-atlas)"
    )
    parser.add_argument(
        "--verify", action="store_true",
        help="Review existing tagged output for errors (uses bigger batches)"
    )
    parser.add_argument(
        "--check", action="store_true",
        help="Run local consistency checks on tagged output (no API calls)"
    )
    parser.add_argument(
        "--fix", action="store_true",
        help="With --check, auto-fix mechanical issues (tile_type in tags, spelling, etc.)"
    )
    parser.add_argument(
        "--model", type=str, default=DEFAULT_MODEL,
        help=f"Model for claude CLI (default: {DEFAULT_MODEL})"
    )
    parser.add_argument(
        "--show-text", action="store_true",
        help="Generate and print the batch text (grid.txt/palette.txt) for one batch, then exit"
    )
    parser.add_argument(
        "--unindexed", action="store_true",
        help="Report unindexed tiles in main atlas and exit"
    )
    args = parser.parse_args()

    dcss_dir = Path(args.dcss_dir) if args.dcss_dir else None
    if dcss_dir and not dcss_dir.exists():
        print(f"ERROR: Directory not found: {dcss_dir}")
        sys.exit(1)

    if args.unindexed:
        report_unindexed(dcss_dir or DCSS_DIR)
        return

    targets = ["utumno", "supplemental"] if args.atlas == "both" else [args.atlas]

    if args.show_text:
        for atlas_name in targets:
            show_text_atlas(atlas_name, dcss_dir, batch_size=args.batch_size,
                            limit=args.limit or 1)
        return

    if args.check:
        for atlas_name in targets:
            check_atlas(atlas_name, dcss_dir, fix=args.fix)
        return

    if args.verify:
        verify_size = args.batch_size if args.batch_size != 12 else 36
        for atlas_name in targets:
            verify_atlas(atlas_name, dcss_dir, batch_size=verify_size,
                         dry_run=args.dry_run, limit=args.limit,
                         args_model=args.model)
        return

    for atlas_name in targets:
        process_atlas(atlas_name, dcss_dir, batch_size=args.batch_size,
                      dry_run=args.dry_run, limit=args.limit,
                      args_model=args.model)

    # After processing, also generate unindexed report for main atlas
    if not args.dry_run and "utumno" in targets:
        report_unindexed(dcss_dir or DCSS_DIR)


if __name__ == "__main__":
    main()
