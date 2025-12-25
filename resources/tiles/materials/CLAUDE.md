# Tile Materials Directory

## Purpose

Contains JSON definitions for materials that can be applied to tiles. Materials modify tile properties like durability and affect sensory detection (sight, hearing, smell blocking).

## Files

| File | ID | Description | Durability Modifier |
|------|-----|-------------|---------------------|
| `wood.json` | `wood` | Standard wooden material, flammable | 1.0x (baseline) |
| `stone.json` | `stone` | Solid stone, heavy and durable | 1.5x |
| `glass.json` | `glass` | Window glass, fragile | 0.3x |
| `plant.json` | `plant` | Organic plant material | 1.0x |
| `dirt.json` | `dirt` | Basic dirt/soil | 1.0x |

## JSON Schema

```json
{
  "Id": "string (required)",             // Unique identifier referenced by tiles/templates
  "Name": "string (required)",           // Display name
  "Description": "string",               // Human-readable description
  "DurabilityModifier": "float",         // Multiplier for tile BaseDurability (1.0 = 100%)
  "SensoryModifiers": {
    "Sight": "float",                    // Vision blocking (lower = more transparent)
    "Hearing": "float",                  // Sound dampening
    "Smell": "float"                     // Smell blocking
  },
  "Properties": {
    "Flammable": "string",               // "true" or "false"
    "Weight": "string",                  // "light", "medium", "heavy"
    "Insulation": "string"               // "low", "medium", "high"
  }
}
```

## Sensory Modifiers Explained

Sensory modifiers affect how tiles block entity perception:

| Value | Effect |
|-------|--------|
| `0.0` | Fully blocks that sense (opaque) |
| `0.5` | Reduces detection by 50% |
| `1.0` | No modification (baseline) |
| `> 1.0` | Enhances detection (amplifies) |

**Example - Glass**:
- `Sight: 0.2` - Very transparent, almost no vision blocking
- `Hearing: 0.8` - Moderate sound dampening
- `Smell: 0.9` - Slight smell blocking

**Example - Stone**:
- `Sight: 1.0` - Fully opaque (combined with tile's own difficulty)
- `Hearing: 1.2` - Amplifies sound blocking
- `Smell: 0.5` - Partially blocks smells

## Durability Calculation

Final tile durability is calculated in `TileResourceManager.CreateBuildingTile()`:

```csharp
int durability = tileDef.BaseDurability;
if (material != null)
{
    durability = (int)(durability * material.DurabilityModifier);
}
```

**Example**:
- Wall `BaseDurability: 100`
- Stone `DurabilityModifier: 1.5`
- Result: `100 * 1.5 = 150` HP

## Adding New Materials

1. Create a new JSON file with unique `Id`
2. Set appropriate `DurabilityModifier` (relative to wood = 1.0)
3. Define `SensoryModifiers` for Sight, Hearing, Smell
4. Add any relevant `Properties`
5. Reference in tile definitions and building templates

## Dependencies

- **TileMaterialDefinition**: `entities/building/TileMaterialDefinition.cs`
- **TileResourceManager.GetMaterial()**: Retrieves material by ID
- **SenseType enum**: `entities/Sensory/SenseType.cs` (Sight, Hearing, Smell)

## Validation Rules

`TileMaterialDefinition.Validate()` checks:
1. `Id` is not null/empty
2. `Name` is not null/empty

## Usage in Game

Materials are referenced by:
1. **Building templates**: `"Material": "Wood"` in tile entries
2. **Tile creation**: `TileResourceManager.CreateBuildingTile(..., materialId, ...)`
3. **Sensory system**: Detection difficulty calculations multiply tile and material modifiers
