# /resources/items

## Purpose

This directory contains JSON definition files for all item types in Veil of Ages. Item definitions are loaded by `ItemResourceManager` at runtime and serve as templates for creating item instances. The system supports food, materials, tools, and remains with decay simulation and stacking behavior.

## JSON Schema

All item JSON files follow this schema:

```json
{
    "Id": "string",              // Required: Unique identifier (e.g., "bread", "wheat")
    "Name": "string",            // Required: Display name shown to player
    "Description": "string",     // Optional: Descriptive text explaining the item
    "Category": "string",        // Required: One of the ItemCategory enum values
    "VolumeM3": 0.0,             // Optional: Volume per unit in cubic meters (default 0)
    "WeightKg": 0.0,             // Optional: Weight per unit in kilograms (default 0)
    "BaseDecayRatePerTick": 0.0, // Optional: Decay rate per game tick, 0 = no decay (default 0)
    "EdibleNutrition": 0,        // Optional: Nutrition value for edible items, 0 = not edible (default 0)
    "StackLimit": 100,           // Optional: Maximum stack size (default 100, minimum 1)
    "Tags": ["string"]           // Optional: Tags for filtering and behavior (default [])
}
```

### Field Descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier used to reference this item type in code and other definitions |
| `Name` | string | Yes | Human-readable name displayed in the game UI |
| `Description` | string | No | Flavor text explaining what the item is and its uses |
| `Category` | string | Yes | Primary classification for organization and filtering |
| `VolumeM3` | float | No | Physical volume in cubic meters (affects container capacity) |
| `WeightKg` | float | No | Physical weight in kilograms (affects carrying capacity) |
| `BaseDecayRatePerTick` | float | No | How fast the item decays per game tick; 0 means no decay |
| `EdibleNutrition` | float | No | Nutrition points provided when consumed; 0 means not directly edible |
| `StackLimit` | int | No | Maximum quantity in a single stack; defaults to 100 |
| `Tags` | string[] | No | Tags used for filtering, crafting recipes, and special behaviors |

### Category Values

The `Category` field must be one of these `ItemCategory` enum values:

| Category | Description |
|----------|-------------|
| `RawMaterial` | Unprocessed resources gathered from the world (e.g., wheat, ore) |
| `ProcessedMaterial` | Refined materials ready for crafting (e.g., flour, metal ingots) |
| `Food` | Consumable items that provide nutrition (e.g., bread) |
| `Tool` | Items used for tasks or crafting (e.g., hammer, knife) |
| `Remains` | Corpses and body parts (e.g., corpse) |

## Current Items

| Id | Name | Category | Tags | Nutrition | Decay Rate | Stack |
|----|------|----------|------|-----------|------------|-------|
| `bread` | Bread | Food | food, baked | 60 | 0.0001 | 20 |
| `wheat` | Wheat | RawMaterial | grain, millable | 0 | 0.00001 | 100 |
| `flour` | Flour | ProcessedMaterial | flour, bakeable | 0 | 0.00002 | 100 |
| `corpse` | Corpse | Remains | corpse, zombie_food, organic | 0 | 0.00005 | 1 |

## Important Tags

Tags drive game behavior and filtering. The following tags have special meaning:

| Tag | Description |
|-----|-------------|
| `food` | Item can be consumed by living beings to satisfy hunger |
| `zombie_food` | Item can be consumed by undead beings (corpses, flesh) |
| `organic` | Organic material that decays and can be composted |
| `grain` | Grain type for milling into flour |
| `millable` | Can be processed at a mill |
| `flour` | Flour type used in baking |
| `bakeable` | Can be baked into food items |
| `baked` | Result of baking process |
| `corpse` | A dead body; used for necromancy and undead sustenance |

## How to Add New Items

1. **Create a new JSON file** in this directory with a descriptive name matching the item's Id:
   ```
   resources/items/my_item.json
   ```

2. **Define required fields** (`Id`, `Name`, `Category`):
   ```json
   {
       "Id": "my_item",
       "Name": "My Item",
       "Category": "RawMaterial"
   }
   ```

3. **Add optional fields** as needed for the item type:
   - Set `EdibleNutrition > 0` for consumable food items
   - Set `BaseDecayRatePerTick > 0` for perishable items
   - Adjust `StackLimit` for items that should stack less (corpses) or more (coins)
   - Set realistic `VolumeM3` and `WeightKg` for inventory/storage systems

4. **Add appropriate tags** for categorization and behavior:
   - Add `food` tag if living beings can eat it
   - Add `zombie_food` tag if undead can consume it
   - Add processing tags like `millable` or `bakeable` for crafting chains

5. **Subdirectories are supported**: Items can be organized into subdirectories (e.g., `items/food/`, `items/materials/`) and will be automatically discovered by `ItemResourceManager`.

### Example: New Food Item

```json
{
    "Id": "apple",
    "Name": "Apple",
    "Description": "A fresh apple from the orchard. A quick snack for the living.",
    "Category": "Food",
    "VolumeM3": 0.0002,
    "WeightKg": 0.15,
    "BaseDecayRatePerTick": 0.00008,
    "EdibleNutrition": 25,
    "StackLimit": 50,
    "Tags": ["food", "fruit", "organic"]
}
```

### Example: New Raw Material

```json
{
    "Id": "iron_ore",
    "Name": "Iron Ore",
    "Description": "Raw iron ore, ready to be smelted into ingots.",
    "Category": "RawMaterial",
    "VolumeM3": 0.002,
    "WeightKg": 2.5,
    "BaseDecayRatePerTick": 0,
    "EdibleNutrition": 0,
    "StackLimit": 50,
    "Tags": ["ore", "metal", "smeltable"]
}
```

## Important Notes

### Decay System
- `BaseDecayRatePerTick` determines how fast items spoil
- Decay progresses from 0 (fresh) to 1 (spoiled)
- Higher values = faster decay (bread at 0.0001 spoils faster than wheat at 0.00001)
- Storage containers can apply modifiers to slow decay
- Items with decay rate of 0 never spoil

### Food vs Zombie Food
- Items tagged `food` can satisfy hunger for living beings
- Items tagged `zombie_food` can satisfy hunger for undead beings
- The `corpse` item is `zombie_food` but NOT `food` (living beings won't eat corpses)
- Items can have both tags if desired (e.g., raw meat might be both)

### Nutrition Values
- `EdibleNutrition` only applies to items with `food` or `zombie_food` tags
- A value of 60 (bread) represents a full meal
- Lower values (25-40) are snacks
- The `corpse` has 0 nutrition in EdibleNutrition but is still consumed via the `zombie_food` tag system

### Validation
- `ItemResourceManager` validates all definitions on load
- Required fields: `Id`, `Name`
- Numeric fields cannot be negative
- `StackLimit` must be at least 1

## Dependencies

### Loaded By
- `ItemResourceManager` (`entities/items/ItemResourceManager.cs`)

### Related Code
- `ItemDefinition` (`entities/items/ItemDefinition.cs`) - The C# class matching this schema
- `Item` (`entities/items/Item.cs`) - Runtime instances created from definitions
- `entities/items/CLAUDE.md` - Documentation for the item system code
