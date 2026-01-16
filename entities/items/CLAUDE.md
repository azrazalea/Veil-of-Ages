# /entities/items

## Purpose

This directory contains the core item system for Veil of Ages. It provides JSON-serializable item definitions, runtime item instances with decay and stacking behavior, a singleton manager for loading and creating items, and a storage container interface. The system is designed for a resource/inventory simulation where items can decay over time, be organized into categories, and stored in various containers.

## Files

### ItemDefinition.cs
JSON-serializable item template that defines the properties of an item type.

**Key Properties:**
- `Id` - Unique identifier for this item type
- `Name` - Display name for the item
- `Description` - Descriptive text
- `Category` - ItemCategory enum (RawMaterial, ProcessedMaterial, Food, Tool, Remains)
- `VolumeM3` - Volume per unit in cubic meters
- `WeightKg` - Weight per unit in kilograms
- `BaseDecayRatePerTick` - Decay rate per game tick (0 = no decay)
- `EdibleNutrition` - Nutrition value if edible (0 = not edible)
- `StackLimit` - Maximum stack size (default 100)
- `Tags` - List of string tags for categorization

**Key Methods:**
- `LoadFromJson(path)` - Load definition from JSON file
- `Validate()` - Validate required fields and constraints
- `HasTag(tag)` - Check if item has a specific tag (case-insensitive)

### Item.cs
Runtime item instance representing a stack of items in the game world.

**Key Properties:**
- `Definition` - The ItemDefinition for this item
- `Quantity` - Current stack size
- `DecayProgress` - Decay from 0 to 1 (1 = spoiled)
- `TotalVolume` - Total volume of stack (computed)
- `TotalWeight` - Total weight of stack (computed)
- `IsSpoiled` - True if DecayProgress >= 1.0
- `IsEdible` - True if EdibleNutrition > 0

**Key Methods:**
- `ApplyDecay(modifier)` - Apply decay with optional rate modifier
- `Split(amount)` - Split stack, returns new Item or null
- `TryMerge(other)` - Merge another item, returns leftover or null

**Internal:**
- `DecayProgressInternal` - Setter for creating items with pre-existing decay

### ItemResourceManager.cs
Singleton manager for loading and creating items.

**Key Features:**
- Singleton pattern with lazy initialization
- Loads item definitions from `res://resources/items/*.json`
- Supports subdirectories for organization
- Creates item instances from definition IDs

**Key Methods:**
- `Initialize()` - Load all definitions
- `GetDefinition(id)` - Get definition by ID
- `CreateItem(definitionId, quantity)` - Create new item instance
- `GetAllDefinitions()` - Enumerate all definitions
- `GetDefinitionsByCategory(category)` - Filter by category
- `GetDefinitionsByTag(tag)` - Filter by tag
- `HasDefinition(id)` - Check if definition exists

### IStorageContainer.cs
Interface for anything that can store items (buildings, inventories, containers).

**Key Properties:**
- `VolumeCapacity` - Maximum volume capacity in cubic meters
- `WeightCapacity` - Maximum weight capacity in kg (-1 = unlimited)
- `UsedVolume` - Currently used volume
- `UsedWeight` - Currently used weight
- `RemainingVolume` - Available volume (computed)
- `RemainingWeight` - Available weight (computed, float.MaxValue if unlimited)
- `DecayRateModifier` - Modifier for item decay (0.5 = half rate, 1.0 = normal, 2.0 = double)

**Key Methods:**
- `CanAdd(item)` - Check if an item can be added
- `AddItem(item)` - Add an item to the container
- `RemoveItem(itemDefId, quantity)` - Remove items by definition ID
- `HasItem(itemDefId, quantity)` - Check if container has items
- `GetItemCount(itemDefId)` - Get total count of an item type
- `FindItem(itemDefId)` - Find first item by definition ID
- `FindItemByTag(tag)` - Find first item with a matching tag
- `GetAllItems()` - Enumerate all items
- `ProcessDecay()` - Process decay for all items (call each tick)
- `GetContentsSummary()` - Default interface method that returns a human-readable summary of container contents (e.g., "3 Wheat, 5 Bread" or "empty"). Useful for debug logging. Note: `StorageTrait` and `InventoryTrait` provide explicit implementations of this method.

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `ItemDefinition` | JSON-serializable item template |
| `Item` | Runtime item instance with decay/stacking |
| `ItemResourceManager` | Singleton manager for definitions |
| `IStorageContainer` | Interface for item storage |
| `ItemCategory` | Enum: RawMaterial, ProcessedMaterial, Food, Tool, Remains |

## Important Notes

### Item Categories
The `ItemCategory` enum defines primary item classifications:
- `RawMaterial` - Unprocessed resources (wheat, ore)
- `ProcessedMaterial` - Refined materials (flour, ingots)
- `Food` - Consumable items providing nutrition (bread)
- `Tool` - Items used for tasks or crafting
- `Remains` - Corpses and body parts for necromancy

### Decay System
- Items decay each tick based on `BaseDecayRatePerTick`
- Storage containers can modify decay rate via `ApplyDecay(modifier)`
- `DecayProgress` ranges from 0 (fresh) to 1 (spoiled)
- Decay is linear; future enhancement could support exponential decay
- Items with `BaseDecayRatePerTick = 0` never decay
- `IsSpoiled` returns true when `DecayProgress >= 1.0`

### Stacking Behavior
- Items stack up to `StackLimit` per definition (default 100)
- `TryMerge()` prevents merging items with >10% decay difference
- When merging, decay is averaged weighted by quantity
- `Split()` preserves decay progress on both resulting items
- Quantity is clamped to StackLimit on creation

### Tags System
Tags provide flexible categorization beyond the primary category:
- Case-insensitive matching via `StringComparer.OrdinalIgnoreCase`
- Used for filtering items (`GetDefinitionsByTag`)
- Used for finding items in storage (`FindItemByTag`)
- Common tags: `food`, `zombie_food`, `organic`, `grain`, `millable`, `bakeable`

### Storage Containers
The `IStorageContainer` interface enables:
- Volume-based capacity (cubic meters)
- Weight-based capacity (kilograms, -1 for unlimited)
- Decay rate modification (e.g., refrigeration at 0.5x)
- Item lookup by ID or tag
- Per-tick decay processing

### Resource Loading
- Definitions loaded from `res://resources/items/` directory
- Supports nested subdirectories for organization (e.g., `items/food/`, `items/materials/`)
- Uses standard JSON serialization via `JsonOptions.Default`
- All JSON files in the directory and subdirectories are loaded automatically

### Thread Safety
- `ItemResourceManager` is not thread-safe during initialization
- Once initialized, read-only access to definitions is safe
- `Item` instances are not thread-safe; each should be owned by one system
- `IStorageContainer` implementations must handle their own thread safety

## Dependencies

### Depends On
- `VeilOfAges.Core.Lib` - Log class, JsonOptions
- `Godot` - ProjectSettings for path globalization
- `System.Text.Json` - JSON serialization

### Depended On By
- (Future) Inventory system
- (Future) Storage container system
- (Future) Crafting system
- (Future) Need satisfaction strategies

## Existing Item Definitions

Current items defined in `resources/items/`:

| Id | Name | Category | Tags | Notes |
|----|------|----------|------|-------|
| `wheat` | Wheat | RawMaterial | grain, millable | Raw grain for milling |
| `flour` | Flour | ProcessedMaterial | flour, bakeable | Milled wheat for baking |
| `bread` | Bread | Food | food, baked | Edible baked goods |
| `corpse` | Corpse | Remains | corpse, zombie_food, organic | Undead sustenance, StackLimit=1 |

## Creating New Item Definitions

1. Create a JSON file in `resources/items/` (or a subdirectory)
2. Define required fields: `Id`, `Name`, `Category`
3. Add optional fields as needed
4. The file will be automatically loaded by `ItemResourceManager.Initialize()`

### JSON Schema

```json
{
  "Id": "string",              // Required: Unique identifier
  "Name": "string",            // Required: Display name
  "Description": "string",     // Optional: Flavor text
  "Category": "string",        // Required: RawMaterial|ProcessedMaterial|Food|Tool|Remains
  "VolumeM3": 0.001,           // Optional: Volume per unit in cubic meters
  "WeightKg": 0.05,            // Optional: Weight per unit in kilograms
  "BaseDecayRatePerTick": 0.0, // Optional: Decay rate per tick (0 = no decay)
  "EdibleNutrition": 0,        // Optional: Nutrition value (0 = not edible)
  "StackLimit": 100,           // Optional: Max stack size (default 100, min 1)
  "Tags": ["tag1", "tag2"]     // Optional: Tags for filtering/behavior
}
```

### Example: Raw Material

```json
{
  "Id": "iron_ore",
  "Name": "Iron Ore",
  "Description": "Raw iron ore ready for smelting",
  "Category": "RawMaterial",
  "VolumeM3": 0.002,
  "WeightKg": 2.5,
  "BaseDecayRatePerTick": 0,
  "StackLimit": 50,
  "Tags": ["ore", "metal", "smeltable"]
}
```

### Example: Food Item

```json
{
  "Id": "apple",
  "Name": "Apple",
  "Description": "A fresh apple from the orchard",
  "Category": "Food",
  "VolumeM3": 0.0002,
  "WeightKg": 0.15,
  "BaseDecayRatePerTick": 0.00008,
  "EdibleNutrition": 25,
  "StackLimit": 50,
  "Tags": ["food", "fruit", "organic"]
}
```

### Validation Rules
- `Id` and `Name` are required
- `VolumeM3`, `WeightKg`, `BaseDecayRatePerTick`, `EdibleNutrition` cannot be negative
- `StackLimit` must be at least 1
