# Building Templates Directory

## Purpose

Contains JSON files that define building layouts, including their tiles, rooms, metadata, and entrance positions. Each template represents a complete building that can be placed in the game world.

## Files

| File | Description |
|------|-------------|
| `simple_house.json` | A 6x6 wooden house with one room, door, and window. Capacity: 2 |
| `simple_farm.json` | A 6x4 fenced farm with crops (rice). Capacity: 2 |
| `graveyard.json` | A 6x5 stone-walled cemetery with tombstones and gate. Capacity: 3 |
| `cellar.json` | A 7x8 underground cellar with dirt walls. Contains ladder entrance and necromancy_altar facility. Capacity: 0 |

## JSON Schema

```json
{
  "Name": "string (required)",           // Display name
  "Description": "string",               // Human-readable description
  "BuildingType": "string",              // Type identifier: House, Farm, Graveyard, etc.
  "Size": [width, height],               // Building dimensions in tiles
  "Culture": "string",                   // Cultural style: Human, Undead, etc.
  "Style": "string",                     // Visual style: Rural, Gothic, etc.
  "Capacity": "number",                  // Max occupants/items
  "EntrancePositions": [[x, y], ...],    // Valid entry points (must be door/gate/walkable)
  "Metadata": {
    "ConstructionTime": "string",        // e.g., "2d" for 2 days
    "RequiredMaterials": "string",       // e.g., "Wood:20,Glass:1"
    "Value": "string"                    // Base value
  },
  "Rooms": [
    {
      "Name": "string",
      "Purpose": "string",               // Living, Farming, Burial, etc.
      "TopLeft": [x, y],
      "Size": [width, height],
      "Properties": { ... }
    }
  ],
  "Tiles": [
    {
      "Position": [x, y],                // Position within building (0-indexed)
      "Type": "string",                  // Tile type: Wall, Floor, Door, Window, Fence, Gate, Crop, Decoration
      "Material": "string",              // Material ID: Wood, Stone, Glass, Dirt, Plant
      "Variant": "string",               // Variant name: CornerTopLeft_Top, HorizontalTop, etc.
      "Category": "string"               // Optional: For decoration subtypes like "Tombstone"
    }
  ]
}
```

## Tile Position Conventions

- Position `[0, 0]` is the top-left corner of the building
- Multiple tiles can exist at the same position (e.g., Floor + Fence overlay)
- All tile positions must be within `Size` bounds

## Wall Variant Naming

Walls use a two-tile-high system with suffixes:
- `_Top` - Upper tile of the wall section
- `_Bottom` - Lower tile of the wall section

Position names:
- `CornerTopLeft`, `CornerTopRight`, `CornerBottomLeft`, `CornerBottomRight`
- `HorizontalTop`, `HorizontalBottom`
- `VerticalLeft`, `VerticalRight`

## Adding New Buildings

1. Create a new JSON file following the schema above
2. Ensure all tile Types reference valid tile definitions in `tiles/definitions/`
3. Ensure all Materials reference valid materials in `tiles/materials/`
4. Ensure entrance positions point to walkable tiles (doors, gates)
5. Validate the template by loading it in-game

## Important Notes

- Pixel offsets for building tile alignment are zeroed out for 32x32 tiles (no manual offset needed)

## Dependencies

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
