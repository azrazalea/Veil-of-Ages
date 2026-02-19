#!/usr/bin/env python3
"""Build and query a SQLite sprite database from JSON atlas indexes.

The JSON index files remain the source of truth — the database is a derived
cache that can be rebuilt at any time.

Usage:
    python tools/manage_sprite_db.py                              # rebuild all configured indexes
    python tools/manage_sprite_db.py --add path/to.json --name x  # add one index
    python tools/manage_sprite_db.py --rebuild                    # drop all + rebuild
    python tools/manage_sprite_db.py --stats                      # count per atlas
    python tools/manage_sprite_db.py --find "wooden shield"       # FTS5 text search
"""

import argparse
import json
import os
import sqlite3
import sys
from pathlib import Path

# ─── Configuration ──────────────────────────────────────────────────────────

ROOT = Path(__file__).resolve().parent.parent  # Veil of Ages root
DB_PATH = ROOT / "assets" / "sprites.db"

DEFAULT_INDEXES = {
    "kenney": "assets/kenney/kenney_atlas_index.json",
    "dcss_combined": "assets/dcss/dcss_combined_index.json",
}

# ─── Schema ─────────────────────────────────────────────────────────────────

SCHEMA_SQL = """\
CREATE TABLE IF NOT EXISTS atlases (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    index_path TEXT NOT NULL,
    atlas_image TEXT
);

CREATE TABLE IF NOT EXISTS tile_types (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL
);

CREATE TABLE IF NOT EXISTS sprites (
    id INTEGER PRIMARY KEY,
    atlas_id INTEGER NOT NULL REFERENCES atlases(id),
    key TEXT NOT NULL,
    row INTEGER NOT NULL,
    col INTEGER NOT NULL,
    tiles_x INTEGER NOT NULL DEFAULT 1,
    tiles_y INTEGER NOT NULL DEFAULT 1,
    description TEXT,
    tile_type_id INTEGER REFERENCES tile_types(id),
    UNIQUE(atlas_id, key)
);

CREATE TABLE IF NOT EXISTS tags (
    sprite_id INTEGER NOT NULL REFERENCES sprites(id),
    tag TEXT NOT NULL,
    PRIMARY KEY (sprite_id, tag)
);

-- FTS5 virtual table for fast full-text search on key + description
CREATE VIRTUAL TABLE IF NOT EXISTS sprites_fts USING fts5(
    key,
    description,
    content='sprites',
    content_rowid='id'
);

-- Triggers to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS sprites_ai AFTER INSERT ON sprites BEGIN
    INSERT INTO sprites_fts(rowid, key, description) VALUES (new.id, new.key, new.description);
END;

CREATE TRIGGER IF NOT EXISTS sprites_ad AFTER DELETE ON sprites BEGIN
    INSERT INTO sprites_fts(sprites_fts, rowid, key, description) VALUES ('delete', old.id, old.key, old.description);
END;

CREATE TRIGGER IF NOT EXISTS sprites_au AFTER UPDATE ON sprites BEGIN
    INSERT INTO sprites_fts(sprites_fts, rowid, key, description) VALUES ('delete', old.id, old.key, old.description);
    INSERT INTO sprites_fts(rowid, key, description) VALUES (new.id, new.key, new.description);
END;

-- Flattened view for easy querying
CREATE VIEW IF NOT EXISTS sprite_search AS
SELECT s.id, a.name AS atlas, s.key, tt.name AS tile_type,
       s.description, GROUP_CONCAT(t.tag) AS tags
FROM sprites s
JOIN atlases a ON a.id = s.atlas_id
LEFT JOIN tile_types tt ON tt.id = s.tile_type_id
LEFT JOIN tags t ON t.sprite_id = s.id
GROUP BY s.id;

CREATE INDEX IF NOT EXISTS idx_sprites_tile_type ON sprites(tile_type_id);
CREATE INDEX IF NOT EXISTS idx_sprites_key ON sprites(key);
CREATE INDEX IF NOT EXISTS idx_tags_tag ON tags(tag);
"""

# ─── Database helpers ───────────────────────────────────────────────────────


def init_db(db_path: Path) -> sqlite3.Connection:
    """Open (or create) the database and ensure schema exists."""
    conn = sqlite3.connect(str(db_path))
    conn.execute("PRAGMA journal_mode=WAL")
    conn.execute("PRAGMA foreign_keys=ON")
    conn.executescript(SCHEMA_SQL)
    return conn


def get_or_create_tile_type(conn: sqlite3.Connection, name: str) -> int:
    """Return the tile_type id, creating it if needed."""
    row = conn.execute("SELECT id FROM tile_types WHERE name = ?", (name,)).fetchone()
    if row:
        return row[0]
    cur = conn.execute("INSERT INTO tile_types (name) VALUES (?)", (name,))
    return cur.lastrowid


def get_or_create_atlas(conn: sqlite3.Connection, name: str, index_path: str,
                        atlas_image: str | None = None) -> int:
    """Return the atlas id, creating or updating it if needed."""
    row = conn.execute("SELECT id FROM atlases WHERE name = ?", (name,)).fetchone()
    if row:
        conn.execute("UPDATE atlases SET index_path = ?, atlas_image = ? WHERE id = ?",
                     (index_path, atlas_image, row[0]))
        return row[0]
    cur = conn.execute("INSERT INTO atlases (name, index_path, atlas_image) VALUES (?, ?, ?)",
                       (name, index_path, atlas_image))
    return cur.lastrowid


def clear_atlas_sprites(conn: sqlite3.Connection, atlas_id: int):
    """Remove all sprites (and their tags) for an atlas before re-import."""
    sprite_ids = [r[0] for r in conn.execute(
        "SELECT id FROM sprites WHERE atlas_id = ?", (atlas_id,)).fetchall()]
    if sprite_ids:
        placeholders = ",".join("?" * len(sprite_ids))
        conn.execute(f"DELETE FROM tags WHERE sprite_id IN ({placeholders})", sprite_ids)
        conn.execute(f"DELETE FROM sprites WHERE id IN ({placeholders})", sprite_ids)


# ─── Import logic ───────────────────────────────────────────────────────────


def load_index(path: Path) -> dict:
    """Load a JSON atlas index file."""
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def detect_atlas_image(index_data: dict, index_path: Path) -> str | None:
    """Try to detect the atlas .png filename from the index or directory."""
    # Some indexes store it explicitly
    if "atlas" in index_data:
        return index_data["atlas"]
    # Look for a .png in the same directory that isn't a backup
    parent = index_path.parent
    pngs = [p.name for p in parent.glob("*.png")
            if not p.name.endswith(".import") and "bak" not in p.name.lower()]
    if len(pngs) == 1:
        return pngs[0]
    return None


def import_index(conn: sqlite3.Connection, name: str, index_path: Path):
    """Import a single JSON index into the database."""
    if not index_path.exists():
        print(f"ERROR: Index not found: {index_path}")
        return 0

    data = load_index(index_path)
    sprites = data.get("sprites", {})
    if not sprites:
        print(f"WARNING: No sprites in {index_path}")
        return 0

    # Resolve relative path for storage
    try:
        rel_path = str(index_path.relative_to(ROOT)).replace("\\", "/")
    except ValueError:
        rel_path = str(index_path).replace("\\", "/")

    atlas_image = detect_atlas_image(data, index_path)
    atlas_id = get_or_create_atlas(conn, name, rel_path, atlas_image)

    # Clear existing sprites for this atlas (dedup strategy)
    clear_atlas_sprites(conn, atlas_id)

    count = 0
    for key, info in sprites.items():
        # Get or create tile_type
        tile_type_id = None
        tt = info.get("tile_type")
        if tt:
            tile_type_id = get_or_create_tile_type(conn, tt)

        # Insert sprite
        cur = conn.execute(
            """INSERT INTO sprites (atlas_id, key, row, col, tiles_x, tiles_y,
                                    description, tile_type_id)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
            (atlas_id, key, info["row"], info["col"],
             info.get("tiles_x", 1), info.get("tiles_y", 1),
             info.get("description"), tile_type_id))
        sprite_id = cur.lastrowid

        # Insert tags
        tags = info.get("tags", [])
        if tags:
            conn.executemany(
                "INSERT OR IGNORE INTO tags (sprite_id, tag) VALUES (?, ?)",
                [(sprite_id, tag) for tag in tags])

        count += 1

    conn.commit()
    return count


# ─── CLI commands ───────────────────────────────────────────────────────────


def cmd_build_all(conn: sqlite3.Connection, indexes: dict[str, str]):
    """Build database from all configured indexes."""
    total = 0
    for name, rel_path in indexes.items():
        index_path = ROOT / rel_path
        print(f"Importing {name} from {rel_path}...")
        count = import_index(conn, name, index_path)
        print(f"  {count} sprites imported")
        total += count
    print(f"\nTotal: {total} sprites in database")


def cmd_rebuild(conn: sqlite3.Connection, indexes: dict[str, str]):
    """Drop everything and rebuild from scratch."""
    print("Dropping all data...")
    # Drop in reverse dependency order
    conn.executescript("""
        DELETE FROM tags;
        DELETE FROM sprites;
        DELETE FROM atlases;
        DELETE FROM tile_types;
        INSERT INTO sprites_fts(sprites_fts) VALUES('rebuild');
    """)
    conn.commit()
    cmd_build_all(conn, indexes)


def cmd_add(conn: sqlite3.Connection, index_path_str: str, name: str):
    """Add (or replace) a single index."""
    index_path = Path(index_path_str)
    if not index_path.is_absolute():
        index_path = ROOT / index_path_str
    print(f"Importing {name} from {index_path}...")
    count = import_index(conn, name, index_path)
    print(f"  {count} sprites imported")


def cmd_stats(conn: sqlite3.Connection):
    """Show sprite counts per atlas."""
    rows = conn.execute("""
        SELECT a.name,
               COUNT(DISTINCT s.id) as sprite_count,
               COUNT(DISTINCT tt.name) as type_count,
               (SELECT COUNT(DISTINCT t.tag) FROM tags t
                JOIN sprites s2 ON s2.id = t.sprite_id
                WHERE s2.atlas_id = a.id) as tag_count
        FROM atlases a
        LEFT JOIN sprites s ON s.atlas_id = a.id
        LEFT JOIN tile_types tt ON tt.id = s.tile_type_id
        GROUP BY a.id
        ORDER BY a.name
    """).fetchall()

    if not rows:
        print("Database is empty. Run without flags to build.")
        return

    total_sprites = 0
    print(f"{'Atlas':<20} {'Sprites':>8} {'Types':>8} {'Tags':>8}")
    print("-" * 48)
    for name, sprites, types, tags in rows:
        print(f"{name:<20} {sprites:>8} {types:>8} {tags:>8}")
        total_sprites += sprites
    print("-" * 48)
    print(f"{'TOTAL':<20} {total_sprites:>8}")


def cmd_find(conn: sqlite3.Connection, query: str, tag: list[str] | None = None,
             tile_type: str | None = None, atlas: str | None = None,
             limit: int = 20):
    """Combined search: FTS5 text + optional structured filters."""
    conditions = []
    params = []

    # FTS5 text search (optional — if no query, just use structured filters)
    use_fts = bool(query and query.strip())
    if use_fts:
        conditions.append("sprites_fts MATCH ?")
        params.append(query)

    if tag:
        # Require ALL specified tags
        for t in tag:
            conditions.append("""s.id IN (
                SELECT sprite_id FROM tags WHERE tag = ?)""")
            params.append(t)

    if tile_type:
        conditions.append("tt.name = ?")
        params.append(tile_type)

    if atlas:
        conditions.append("a.name = ?")
        params.append(atlas)

    where = " AND ".join(conditions) if conditions else "1=1"
    fts_join = "JOIN sprites_fts f ON f.rowid = s.id" if use_fts else ""
    order = "ORDER BY rank" if use_fts else "ORDER BY s.key"

    sql = f"""
        SELECT a.name, s.key, tt.name, s.description, GROUP_CONCAT(t.tag) as tags
        FROM sprites s
        {fts_join}
        JOIN atlases a ON a.id = s.atlas_id
        LEFT JOIN tile_types tt ON tt.id = s.tile_type_id
        LEFT JOIN tags t ON t.sprite_id = s.id
        WHERE {where}
        GROUP BY s.id
        {order}
        LIMIT ?
    """
    params.append(limit)

    rows = conn.execute(sql, params).fetchall()

    if not rows:
        parts = []
        if query:
            parts.append(f'text="{query}"')
        if tag:
            parts.append(f"tags={tag}")
        if tile_type:
            parts.append(f"type={tile_type}")
        if atlas:
            parts.append(f"atlas={atlas}")
        print(f"No results for: {', '.join(parts)}")
        return

    for atlas_name, key, tt, desc, tags in rows:
        tt_str = f" ({tt})" if tt else ""
        tg_str = f" [{tags}]" if tags else ""
        desc_str = f" \u2014 {desc}" if desc else ""
        print(f"[{atlas_name}] {key}{tt_str}{tg_str}{desc_str}")

    print(f"\n{len(rows)} result(s)")


def cmd_query(conn: sqlite3.Connection, sql: str):
    """Run raw SQL against the database. Has access to all tables and the
    sprite_search view."""
    try:
        cur = conn.execute(sql)
    except Exception as e:
        print(f"SQL error: {e}")
        return

    cols = [d[0] for d in cur.description]
    rows = cur.fetchall()

    if not rows:
        print("No results.")
        return

    # Print as tab-separated with header
    print("\t".join(cols))
    for row in rows:
        print("\t".join(str(v) if v is not None else "" for v in row))

    print(f"\n{len(rows)} row(s)")


# ─── Main ───────────────────────────────────────────────────────────────────


def main():
    parser = argparse.ArgumentParser(
        description="Build and query a SQLite sprite database from JSON atlas indexes"
    )
    parser.add_argument("--rebuild", action="store_true",
                        help="Drop all data and rebuild from configured indexes")
    parser.add_argument("--add", type=str, metavar="PATH",
                        help="Add a single JSON index file")
    parser.add_argument("--name", type=str,
                        help="Atlas name (required with --add)")
    parser.add_argument("--stats", action="store_true",
                        help="Show sprite counts per atlas")
    parser.add_argument("--find", type=str, metavar="QUERY",
                        help="FTS5 text search on key + description")
    parser.add_argument("--tag", type=str, action="append", metavar="TAG",
                        help="Filter by tag (repeatable, requires ALL tags)")
    parser.add_argument("--type", type=str, metavar="TYPE",
                        help="Filter by tile_type (e.g. creature, equipment, wall)")
    parser.add_argument("--atlas", type=str, metavar="NAME",
                        help="Filter by atlas name (e.g. kenney, dcss_combined)")
    parser.add_argument("--limit", type=int, default=20,
                        help="Max results for searches (default: 20)")
    parser.add_argument("--query", type=str, metavar="SQL",
                        help="Run raw SQL (has access to sprite_search view and all tables)")
    parser.add_argument("--db", type=str, default=str(DB_PATH),
                        help=f"Database path (default: {DB_PATH.relative_to(ROOT)})")
    args = parser.parse_args()

    db_path = Path(args.db)

    # Search mode: --find and/or --tag/--type/--atlas
    if args.find or args.tag or args.type:
        if not db_path.exists():
            print(f"ERROR: Database not found: {db_path}")
            print("Run without flags first to build it.")
            sys.exit(1)
        conn = init_db(db_path)
        try:
            cmd_find(conn, args.find or "", tag=args.tag,
                     tile_type=args.type, atlas=args.atlas,
                     limit=args.limit)
        finally:
            conn.close()
        return

    if args.query:
        if not db_path.exists():
            print(f"ERROR: Database not found: {db_path}")
            sys.exit(1)
        conn = init_db(db_path)
        try:
            cmd_query(conn, args.query)
        finally:
            conn.close()
        return

    if args.stats:
        if not db_path.exists():
            print(f"ERROR: Database not found: {db_path}")
            sys.exit(1)
        conn = init_db(db_path)
        try:
            cmd_stats(conn)
        finally:
            conn.close()
        return

    if args.add:
        if not args.name:
            print("ERROR: --name is required with --add")
            sys.exit(1)
        conn = init_db(db_path)
        try:
            cmd_add(conn, args.add, args.name)
        finally:
            conn.close()
        return

    # Default: build all (or rebuild)
    conn = init_db(db_path)
    try:
        if args.rebuild:
            cmd_rebuild(conn, DEFAULT_INDEXES)
        else:
            cmd_build_all(conn, DEFAULT_INDEXES)
    finally:
        conn.close()

    print(f"\nDatabase: {db_path}")


if __name__ == "__main__":
    main()
