# /entities/items

## Purpose

This directory contains the core item system for Veil of Ages. It provides JSON-serializable item definitions, runtime item instances with decay and stacking behavior, and a singleton manager for loading and creating items. The system is designed for a resource/inventory simulation where items can decay over time and be organized into categories.

## Files

### ItemDefinition.cs
JSON-serializable item template that defines the properties of an item type.

**Key Properties:**
- `Id` - Unique identifier for this item type
- `Name` - Display name for the item
- `Description` - Descriptive text
- `Category` - ItemCategory enum (RawMaterial, ProcessedMaterial, Food, Tool)
- `VolumeM3` - Volume per unit in cubic meters
- `WeightKg` - Weight per unit in kilograms
- `BaseDecayRatePerTick` - Decay rate per game tick (0 = no decay)
- `EdibleNutrition` - Nutrition value if edible (0 = not edible)
- `StackLimit` - Maximum stack size (default 100)
- `Tags` - List of string tags for categorization

**Key Methods:**
- `LoadFromJson(path)` - Load definition from JSON file
- `Validate()` - Validate required fields and constraints
- `HasTag(tag)` - Check if item has a specific tag

### Item.cs
Runtime item instance representing a stack of items in the game world.

**Key Properties:**
- `Definition` - The ItemDefinition for this item
- `Quantity` - Current stack size
- `DecayProgress` - Decay from 0 to 1 (1 = spoiled)
- `TotalVolume` - Total volume of stack
- `TotalWeight` - Total weight of stack
- `IsSpoiled` - True if DecayProgress >= 1.0
- `IsEdible` - True if EdibleNutrition > 0

**Key Methods:**
- `ApplyDecay(modifier)` - Apply decay with optional rate modifier
- `Split(amount)` - Split stack, returns new Item or null
- `TryMerge(other)` - Merge another item, returns leftover or null

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

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `ItemDefinition` | JSON-serializable item template |
| `Item` | Runtime item instance with decay/stacking |
| `ItemResourceManager` | Singleton manager for definitions |
| `ItemCategory` | Enum: RawMaterial, ProcessedMaterial, Food, Tool |

## Important Notes

### Decay System
- Items decay each tick based on `BaseDecayRatePerTick`
- Storage containers can modify decay rate via `ApplyDecay(modifier)`
- `DecayProgress` ranges from 0 (fresh) to 1 (spoiled)
- Decay is linear; future enhancement could support exponential decay

### Stacking Behavior
- Items stack up to `StackLimit` per definition
- `TryMerge()` prevents merging items with >10% decay difference
- When merging, decay is averaged weighted by quantity
- `Split()` preserves decay progress on both resulting items

### Resource Loading
- Definitions loaded from `res://resources/items/` directory
- Supports nested subdirectories for organization (e.g., `items/food/`, `items/materials/`)
- Uses standard JSON serialization via `JsonOptions.Default`

### Thread Safety
- `ItemResourceManager` is not thread-safe during initialization
- Once initialized, read-only access to definitions is safe
- `Item` instances are not thread-safe; each should be owned by one system

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

## Creating New Item Definitions

1. Create a JSON file in `resources/items/` (or a subdirectory)
2. Define required fields: `Id`, `Name`, `Category`
3. Add optional fields as needed

Example JSON:
```json
{
  "Id": "wheat",
  "Name": "Wheat",
  "Description": "Raw wheat grain harvested from fields",
  "Category": "RawMaterial",
  "VolumeM3": 0.001,
  "WeightKg": 0.05,
  "BaseDecayRatePerTick": 0.00001,
  "EdibleNutrition": 0,
  "StackLimit": 100,
  "Tags": ["grain", "organic", "farmable"]
}
```
