# resources/buildings/palettes

## Purpose

Contains shared palette JSON files that define tile alias mappings for use in GridFab building templates. Palettes let multiple building templates share a common vocabulary of two-character aliases (e.g., `WW` = Wall/Wood) without duplicating definitions in every template directory.

---

## Files

### base.json

Universal aliases applicable to all building types and cultures.

| Alias | Type | Material | Notes |
|-------|------|----------|-------|
| `.`   | —    | —        | Always empty; reserved by the loader — do not redefine |
| `DF`  | Floor | Dirt    | Ground-layer dirt, typically placed under wood floors |

### human_rural.json

Culture-specific aliases for human rural buildings. **Inherits from `base`**.

| Alias | Type   | Material | Description |
|-------|--------|----------|-------------|
| `WW`  | Wall   | Wood     | Wooden wall |
| `FW`  | Floor  | Wood     | Wooden floor |
| `DW`  | Door   | Wood     | Wooden door |
| `GW`  | Window | Glass    | Glass window |
| `NW`  | Fence  | Wood     | Wooden fence |
| `NS`  | Fence  | Stone    | Stone fence |
| `TW`  | Gate   | Wood     | Wooden gate |
| `TS`  | Gate   | Stone    | Stone gate |
| `CP`  | Crop   | Plant    | Crop / planted field tile |
| `WL`  | Well   | Stone    | Stone well |
| `FD`  | Floor  | Dirt     | Dirt floor (interior) |
| `WD`  | Wall   | Dirt     | Dirt wall (e.g., cellar) |

---

## Palette JSON Schema

```json
{
  "Inherits": "base",        // Optional — name of another palette file to inherit from (no .json extension)
  "Tiles": {
    "XX": { "Type": "TileTypeName", "Material": "MaterialName" }
  }
}
```

- `Inherits` references another file in this same directory by name (without `.json`).
- Inherited aliases are loaded first; this file's `Tiles` are overlaid on top (local entries win on conflict).
- Inheritance is resolved recursively at load time by `GridBuildingTemplateLoader`.

---

## Inheritance System

The resolution order when a template's `palette.json` declares `"Inherits": "human_rural"`:

1. Load `palettes/base.json` (base has no parent — it is the root)
2. Load `palettes/human_rural.json`, overlay its `Tiles` onto base
3. Load the template's own `palette.json`, overlay its `Tiles` onto the result

This means a template can fine-tune any alias without copying the entire palette.

---

## Adding New Palettes

1. Create a new `<name>.json` file in this directory following the schema above.
2. Set `"Inherits"` to `"base"` (or another existing palette) unless you need a completely fresh set.
3. Define aliases in `"Tiles"` — two-character keys are conventional but not required.
4. Reference the new palette by name in any template's `palette.json` via its `"Inherits"` field.
5. Update this CLAUDE.md with the new file and its alias table.

---

## Dependencies

- **GridBuildingTemplateLoader**: `entities/building/GridBuildingTemplateLoader.cs` — loads and resolves palette inheritance at build-template load time
- **TileDefinition** / **TileMaterialDefinition**: alias `Type` and `Material` values must match IDs defined in `resources/tiles/definitions/` and `resources/tiles/materials/`
