# Decoration Variants Directory

## Purpose

Contains variant definition files that extend the base `decoration.json` tile definition. Each file adds a new category of decorative elements with their own material variants and atlas coordinates.

## How Variant Merging Works

When `TileResourceManager` loads tile definitions:

1. First pass: Loads `decoration.json` from the parent directory
2. Second pass: Finds the `decoration/` subdirectory (matches the `Id`)
3. Loads each JSON file in this subdirectory
4. Merges each variant into the base definition using `TileDefinition.MergeWithVariant()`

The result is a single `decoration` tile definition with multiple categories.

## Files

| File | Category | Description |
|------|----------|-------------|
| `tombstone.json` | Tombstone | Grave markers in stone and wood variants |

## JSON Schema (Variant Files)

Variant files use a subset of the tile definition schema:

```json
{
  "Category": "string",                      // Creates/merges into this category
  "AtlasSource": "string",                   // Override default atlas
  "BaseDurability": "number",                // Override base durability
  "Properties": {
    "BuildCost": "string",
    "Purpose": "string"
  },
  "Variants": {
    "<material_id>": {
      "<variant_name>": {
        "AtlasCoords": [x, y],
        "AtlasSource": "string"              // Optional: per-variant atlas override
      }
    }
  }
}
```

## Example: tombstone.json

```json
{
  "Category": "Tombstone",
  "AtlasSource": "graveyard_props",
  "BaseDurability": 200,
  "Properties": {
    "BuildCost": "Stone:1",
    "Purpose": "Memorial"
  },
  "Variants": {
    "stone": {
      "Plain": { "AtlasCoords": [1, 1] },
      "Cross": { "AtlasCoords": [5, 1] }
    },
    "wood": {
      "Cross": { "AtlasCoords": [9, 1] }
    }
  }
}
```

## Using Categories in Building Templates

Building templates reference decoration variants using the `Category` field:

```json
{
  "Position": [1, 1],
  "Type": "Decoration",
  "Category": "Tombstone",
  "Material": "Stone",
  "Variant": "Cross"
}
```

## Adding New Decoration Types

1. Create a new JSON file in this directory (e.g., `statue.json`)
2. Set a unique `Category` name (e.g., `"Statue"`)
3. Define variants for each supported material
4. Reference in building templates using `Type: "Decoration"` and `Category: "<your_category>"`

## Dependencies

- **TileDefinition.MergeWithVariant()**: Handles the merge logic
- **TileResourceManager.LoadAllTileDefinitions()**: Processes subdirectory variants
- **graveyard_props atlas**: Currently used for tombstone sprites

## Important Notes

- The directory name must match the parent definition's `Id` exactly
- Variant files do not need `Id`, `Name`, `Type`, or `IsWalkable` (inherited from base)
- Multiple variant files can be added to create additional categories
- Categories provide a namespace for organizing related decoration variants
