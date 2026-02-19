# tools/ — Veil of Ages Utility Scripts

## Sprite Database — `manage_sprite_db.py`

SQLite database built from the JSON atlas indexes. The JSON files remain the source of truth — the DB is a derived cache rebuilt at any time.

### Building the database

```bash
python tools/manage_sprite_db.py              # build from default indexes (kenney + dcss_combined)
python tools/manage_sprite_db.py --rebuild    # drop all + rebuild from scratch
python tools/manage_sprite_db.py --stats      # show counts per atlas
```

To add a new atlas index:
```bash
python tools/manage_sprite_db.py --add assets/urizen/urizen_index.json --name urizen
```

Output: `assets/sprites.db` (gitignored).

### Searching for sprites

Combine any of these flags in a single call:

```bash
# FTS5 text search (searches key names and descriptions)
python tools/manage_sprite_db.py --find "fire elemental"

# Filter by tag (repeatable — requires ALL specified tags)
python tools/manage_sprite_db.py --tag undead --tag humanoid

# Filter by tile_type
python tools/manage_sprite_db.py --type creature

# Filter by atlas
python tools/manage_sprite_db.py --atlas kenney

# Combined: FTS + structured filters
python tools/manage_sprite_db.py --find "knight" --tag undead --type creature --limit 5

# Raw SQL for complex queries
python tools/manage_sprite_db.py --query "SELECT key, atlas, tile_type, tags, description FROM sprite_search WHERE tags LIKE '%fire%' AND tile_type='creature' LIMIT 10"
```

### Search strategy tips

- For creatures: `--find "name" --type creature`
- For items/equipment: `--find "name" --type equipment` or `--tag weapon`, `--tag armour`, etc.
- For tiles/terrain: `--find "name"` alone usually works
- For UI elements: `--find "name"` or `--query` with `tile_type='icon'`
- If FTS gives nothing, try `--query` with `LIKE` on the `sprite_search` view
- One or two queries per need is usually enough — pick the best result and move on

### Database schema reference

The `sprite_search` view is the easiest way to query:
```sql
SELECT * FROM sprite_search WHERE ...
-- Columns: id, atlas, key, tile_type, description, tags (comma-separated)
```

Core tables: `sprites`, `atlases`, `tile_types`, `tags`. FTS5 table: `sprites_fts` (key + description).

## Atlas Tagger — `tag_atlas.py`

Autonomous tagger that enriches JSON atlas indexes with descriptions, tags, and tile_type using Claude's vision. Requires Pillow and the `claude` CLI.

```bash
python tools/tag_atlas.py --atlas kenney --dry-run          # preview batches
python tools/tag_atlas.py --atlas combined                   # tag dcss_combined sprites
python tools/tag_atlas.py --atlas kenney --check             # local consistency checks (no API)
python tools/tag_atlas.py --atlas kenney --check --fix       # auto-fix mechanical issues
python tools/tag_atlas.py --atlas kenney --verify            # AI-powered review of existing tags
```

## Other Scripts

- **`generate_pot.py`** — Generates translation template files.
- **`propagate_row.py`** — Bulk-fixes a row of sprites in an atlas index (used with tag_atlas.py).
- **`build_combined_atlas.py`** — Merges multiple DCSS atlas images + indexes into one combined atlas.
- **`check_atlas_order.py`** — Validates atlas sprite ordering consistency.
- **`find_supplemental_in_atlas.py`** — Finds supplemental sprites within the main atlas.
