# Tile Definitions Directory

## Purpose

Contains JSON files that define tile types (wall, floor, door, etc.). Each file specifies the tile's behavior, appearance, and variants for different materials. Subdirectories contain variant files that are merged with base definitions.

## Files

| File | Type | Description | Default Walkable |
|------|------|-------------|------------------|
| `wall.json` | Wall | Solid wall structure | No |
| `floor.json` | Floor | Walkable surface | Yes |
| `door.json` | Door | Interactive entrance | Yes |
| `window.json` | Window | Wall opening with glass | No |
| `fence.json` | Fence | Boundary structure | No |
| `gate.json` | Gate | Interactive fence entrance | Yes |
| `crop.json` | Crop | Farmable plants | No |
| `decoration.json` | Decoration | Base for decorative elements | No |

## Subdirectory (Variants)

The `decoration/` subdirectory contains variant files that are merged with `decoration.json`:
- `tombstone.json` - Defines tombstone variants for stone and wood materials

## JSON Schema

```json
{
  "Id": "string (required)",                 // Unique identifier (usually matches filename)
  "Name": "string (required)",               // Display name
  "Description": "string",                   // Human-readable description
  "Type": "string (required)",               // Tile type enum: Wall, Floor, Door, Window, Fence, Gate, Crop, Decoration
  "IsWalkable": "boolean",                   // Can entities walk through this tile?
  "BaseDurability": "number",                // Base HP (modified by material)
  "AtlasSource": "string",                   // Default atlas ID (fallback)
  "AtlasCoords": [x, y],                     // Default atlas coordinates (fallback)
  "DefaultMaterial": "string",               // Default material ID if not specified
  "DefaultSensoryDifficulties": {
    "Sight": "float",                        // 0.0 = fully visible, 1.0 = blocks vision
    "Hearing": "float",                      // Sound dampening factor
    "Smell": "float"                         // Smell blocking factor
  },
  "Properties": {
    "BuildCost": "string",                   // e.g., "Material:2"
    "Interactable": "string"                 // "true" for doors/gates
  },
  "Variants": {
    "<material_id>": {
      "<variant_name>": {
        "AtlasSource": "string",             // Optional: override atlas
        "AtlasCoords": [x, y]                // Specific coordinates for this variant
      }
    }
  }
}
```

## Variant System

Variants allow different appearances based on material and style:

```json
"Variants": {
  "wood": {
    "CornerTopLeft_Top": { "AtlasCoords": [4, 1] },
    "CornerTopLeft_Bottom": { "AtlasCoords": [4, 2] }
  },
  "stone": {
    "CornerTopLeft_Top": { "AtlasSource": "graveyard_main", "AtlasCoords": [1, 8] }
  }
}
```

**Lookup order** (in `TileResourceManager.GetProcessedVariant`):
1. Global default variant (if exists)
2. Material-specific default (materialId + "Default")
3. Specific variant (materialId + variantName)

## Variant File Merging

Files in subdirectories matching a base definition's ID are merged:

1. Base file: `definitions/decoration.json` (Id: "decoration")
2. Variant file: `definitions/decoration/tombstone.json` (Category: "Tombstone")
3. Result: `decoration` definition gains "Tombstone" category with its variants

Variant files can override:
- `Category` - Creates a new category namespace
- `AtlasSource` - Different texture source
- `BaseDurability` - Different base HP
- `Properties` - Additional/override properties
- `Variants` - Additional variant definitions

## Common Variant Names

**Walls (two-tile-high)**:
- `CornerTopLeft_Top/Bottom`, `CornerTopRight_Top/Bottom`
- `CornerBottomLeft_Top/Bottom`, `CornerBottomRight_Top/Bottom`
- `HorizontalTop_Top/Bottom`, `HorizontalBottom_Top/Bottom`
- `VerticalLeft`, `VerticalRight`

**Doors/Windows**:
- `Top`, `Bottom`

**Fences**:
- `CornerTopLeft`, `CornerTopRight`, `CornerBottomLeft`, `CornerBottomRight`
- `HorizontalTop`, `HorizontalBottom`
- `VerticalLeft`, `VerticalRight`

**Gates**:
- `FenceTop`, `FenceBottom` (wood)
- `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` (stone)

## Adding New Tile Definitions

1. Create a new JSON file with unique `Id`
2. Set the `Type` to a valid TileType enum value
3. Define base `AtlasSource` and `AtlasCoords` for fallback
4. Add `Variants` for each supported material
5. Optionally set `DefaultSensoryDifficulties` for the sensory system

## Dependencies

- **TileDefinition**: `entities/building/TileDefinition.cs`
- **TileCategory**: Nested class for category grouping
- **TileVariantDefinition**: Nested class for variant data
- **TileResourceManager**: Loads and processes definitions
- **Vector2IConverter**: JSON converter for coordinates

## Validation Rules

`TileDefinition.Validate()` checks:
1. `Id` is not null/empty
2. `Name` is not null/empty
3. `Type` is not null/empty
