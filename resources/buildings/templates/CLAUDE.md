# Building Templates Directory

## Purpose

Contains building layout definitions — both the current **GridFab** directory format and legacy flat JSON files. Each template represents a complete building that can be placed in the game world. `BuildingManager` loads GridFab directories first; legacy `.json` files are still supported as a fallback.

---

## GridFab Format (Current)

Each building is a **subdirectory** containing three kinds of files.

### Directory structure

```
<building_name>/
├── building.json   # Metadata (name, size, entrance positions, rooms, facilities, etc.)
├── palette.json    # Tile alias definitions for this building (may inherit a shared palette)
├── ground.grid     # (optional) Ground-layer tiles (dirt under floors, etc.)
├── floor.grid      # (optional) Floor-layer tiles
└── structure.grid  # (optional) Structure-layer tiles (walls, doors, furniture, etc.)
```

Any number of `*.grid` files may be present. The filename (without extension) is used as the layer name and does not affect parsing order — all layers are merged into the final tile list.

### building.json schema

```json
{
  "Name": "string (required)",
  "Description": "string",
  "BuildingType": "string",              // House, Farm, Graveyard, etc.
  "Size": [width, height],
  "Culture": "string",                   // Human, Undead, etc.
  "Style": "string",                     // Rural, Gothic, etc.
  "Capacity": "number",
  "EntrancePositions": [[x, y], ...],
  "Metadata": {
    "ConstructionTime": "string",        // e.g. "2d"
    "RequiredMaterials": "string",       // e.g. "Wood:20,Glass:1"
    "Value": "string"
  },
  "Rooms": [
    {
      "Name": "string",
      "Purpose": "string",
      "TopLeft": [x, y],
      "Size": [width, height],
      "Properties": { }
    }
  ],
  "Facilities": [ ... ]
}
```

### Grid file syntax

Each `*.grid` file is plain text:

- **Rows** correspond to tile rows from top (row 0) to bottom.
- Each row is a sequence of **space-separated two-character aliases**.
- The alias `.` (dot) always means "empty — place no tile here".
- All other aliases are looked up in the resolved palette to produce a `(TileType, Material)` pair.

Example `structure.grid` for a small 4x3 building:

```
WW WW WW WW
WW FW FW WW
WW WW DW WW
```

### palette.json schema

```json
{
  "Inherits": "human_rural",   // Optional — name of a shared palette in resources/buildings/palettes/
  "Tiles": {
    "WW": { "Type": "Wall",  "Material": "Wood" },
    "FW": { "Type": "Floor", "Material": "Wood" },
    "DW": { "Type": "Door",  "Material": "Wood" }
  }
}
```

Alias resolution: shared palette aliases are loaded first; the template's own `Tiles` entries are overlaid on top, allowing per-building overrides.

### Palette inheritance

`GridBuildingTemplateLoader` resolves inheritance at load time:

1. If `Inherits` is set, the named JSON file is loaded from `resources/buildings/palettes/`.
2. The template's own `Tiles` dictionary is merged on top (template entries win on conflict).
3. The resulting flat alias map is used for all `*.grid` files in that directory.

---

## Template Directories (All 7 Buildings)

| Directory | Grid Files | Description |
|-----------|-----------|-------------|
| `simple_house/` | ground, floor, structure | 6x6 wooden house, capacity 2 |
| `scholars_house/` | ground, floor, structure | Scholar's house with study area |
| `cellar/` | floor, structure | 7x8 underground cellar, necromancy altar, capacity 0 |
| `graveyard/` | structure | 6x5 stone cemetery with gate, capacity 3 |
| `simple_farm/` | floor, structure | 6x4 fenced farm with crops, capacity 2 |
| `granary/` | floor, structure | Grain storage building |
| `well/` | structure | Single-tile well structure |

---

## Legacy JSON Format

The old flat `.json` files (one file per building, all tiles listed as explicit coordinates) are still present and supported. If a template directory and a `.json` file share the same `Name`, the GridFab directory takes priority.

### Legacy JSON schema

```json
{
  "Name": "string (required)",
  "Description": "string",
  "BuildingType": "string",
  "Size": [width, height],
  "Culture": "string",
  "Style": "string",
  "Capacity": "number",
  "EntrancePositions": [[x, y], ...],
  "Metadata": { "ConstructionTime": "string", "RequiredMaterials": "string", "Value": "string" },
  "Rooms": [ { "Name": "string", "Purpose": "string", "TopLeft": [x, y], "Size": [width, height] } ],
  "Tiles": [
    {
      "Position": [x, y],
      "Type": "string",
      "Material": "string",
      "Variant": "string",
      "Category": "string"
    }
  ]
}
```

---

## Tile Position Conventions

- Position `[0, 0]` is the top-left corner of the building.
- Multiple tiles can exist at the same position across different layers (e.g., a ground dirt tile under a wood floor tile).
- All tile positions must be within `Size` bounds.

## Wall Variant Naming

Walls use a two-tile-high system with suffixes:
- `_Top` — upper tile of the wall section
- `_Bottom` — lower tile of the wall section

Position names: `CornerTopLeft`, `CornerTopRight`, `CornerBottomLeft`, `CornerBottomRight`, `HorizontalTop`, `HorizontalBottom`, `VerticalLeft`, `VerticalRight`

---

## Adding New Buildings (GridFab)

1. Create a new subdirectory under `resources/buildings/templates/<building_name>/`
2. Add `building.json` with metadata (name, size, entrance positions, etc.)
3. Add `palette.json` — set `"Inherits"` to an appropriate shared palette (e.g., `"human_rural"`) and add any building-specific aliases
4. Add one or more `*.grid` files drawing the layout with palette aliases; use `.` for empty cells
5. Ensure all palette aliases map to valid tile types and materials from `tiles/definitions/` and `tiles/materials/`
6. Validate by loading in-game

---

## Dependencies

- **GridBuildingTemplateLoader**: `entities/building/GridBuildingTemplateLoader.cs` — parses GridFab directories
- **BuildingTemplate**: `entities/building/BuildingTemplate.cs`
- **BuildingTileData**: Nested class in `BuildingTemplate.cs`
- **RoomData**: Nested class in `BuildingTemplate.cs`
- **Vector2IConverter**: Custom JSON converter for `[x, y]` arrays

## Validation Rules

The `BuildingTemplate.Validate()` method checks:
1. `Name` is not null/empty
2. `Size` has positive dimensions
3. At least one tile is defined
4. All tiles are within building bounds
5. All entrance positions are within bounds
6. All entrance tiles are doors, gates, or walkable
7. All tile Types have corresponding tile definitions

## Important Notes

- Pixel offsets for building tile alignment are zeroed out for 32x32 tiles (no manual offset needed)
- The old legacy `.json` files will be removed after the GridFab migration is verified
