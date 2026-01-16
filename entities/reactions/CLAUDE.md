# /entities/reactions

## Purpose

This directory contains the data-driven reaction system for Veil of Ages. Reactions define item transformations - converting input items into output items over a duration. This enables crafting, processing, and production workflows (e.g., milling wheat into flour, baking flour into bread).

## Files

### ItemQuantity.cs
Simple record for representing an item and its quantity.

**Properties:**
- `ItemId` - The unique identifier of the item definition
- `Quantity` - The number of items

**Usage:**
Used in reaction inputs/outputs and can be reused by storage and inventory systems.

### ReactionDefinition.cs
JSON-serializable reaction template that defines a transformation of items.

**Key Properties:**
- `Id` - Unique identifier for this reaction type
- `Name` - Display name for the reaction
- `Description` - Descriptive text explaining the reaction
- `Inputs` - List of ItemQuantity consumed by this reaction
- `Outputs` - List of ItemQuantity produced by this reaction
- `Duration` - Number of game ticks required to complete
- `RequiredBuildingTypes` - Building types where this can be performed (empty = anywhere)
- `Tags` - Tags for categorization and job matching (e.g., "milling", "baking")

**Key Methods:**
- `LoadFromJson(path)` - Load definition from JSON file
- `Validate()` - Validate required fields and constraints
- `HasTag(tag)` - Check if reaction has a specific tag
- `CanPerformAt(buildingType)` - Check if reaction can be performed at a building type

### ReactionResourceManager.cs
Singleton manager for loading and accessing reaction definitions.

**Key Features:**
- Singleton pattern with lazy initialization
- Loads reaction definitions from `res://resources/reactions/*.json`
- Supports subdirectories for organization
- Query methods for filtering by tag or building type

**Key Methods:**
- `Initialize()` - Load all definitions
- `GetDefinition(id)` - Get definition by ID
- `GetAllDefinitions()` - Enumerate all definitions
- `GetReactionsByTag(tag)` - Filter by tag
- `GetReactionsForBuilding(buildingType)` - Filter by building type
- `HasDefinition(id)` - Check if definition exists

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `ItemQuantity` | Record for item ID + quantity pairs |
| `ReactionDefinition` | JSON-serializable reaction template |
| `ReactionResourceManager` | Singleton manager for definitions |

## Important Notes

### Building Type Matching
- If `RequiredBuildingTypes` is empty, the reaction can be performed anywhere
- If `RequiredBuildingTypes` has entries, the reaction requires one of those building types
- Building type matching is case-insensitive

### Tag System
- Tags are used for job matching (e.g., a Miller job trait looks for "milling" tagged reactions)
- Tag matching is case-insensitive
- Multiple tags can be assigned to a single reaction

### Duration
- Duration is measured in game ticks
- At normal game speed, 8 ticks = 1 real second
- Duration of 0 is invalid (validation fails)

### Thread Safety
- `ReactionResourceManager` is not thread-safe during initialization
- Once initialized, read-only access to definitions is safe
- `ReactionDefinition` instances are safe to read from multiple threads

## Dependencies

### Depends On
- `VeilOfAges.Core.Lib` - Log class, JsonOptions
- `Godot` - ProjectSettings for path globalization
- `System.Text.Json` - JSON serialization

### Depended On By
- (Future) Job/work system for assigning reactions to workers
- (Future) Building production queues
- (Future) AI task selection for crafting

## Creating New Reaction Definitions

1. Create a JSON file in `resources/reactions/` (or a subdirectory)
2. Define required fields: `Id`, `Name`, `Inputs`, `Outputs`, `Duration`
3. Add optional fields as needed

Example JSON (milling wheat into flour):
```json
{
  "Id": "mill_wheat",
  "Name": "Mill Wheat",
  "Description": "Grind wheat into flour using a millstone",
  "Inputs": [
    { "ItemId": "wheat", "Quantity": 10 }
  ],
  "Outputs": [
    { "ItemId": "flour", "Quantity": 8 }
  ],
  "Duration": 80,
  "RequiredBuildingTypes": ["windmill", "watermill", "hand_mill"],
  "Tags": ["milling", "food_processing"]
}
```

Example JSON (baking bread):
```json
{
  "Id": "bake_bread",
  "Name": "Bake Bread",
  "Description": "Bake flour into bread in an oven",
  "Inputs": [
    { "ItemId": "flour", "Quantity": 5 },
    { "ItemId": "water", "Quantity": 2 }
  ],
  "Outputs": [
    { "ItemId": "bread", "Quantity": 3 }
  ],
  "Duration": 120,
  "RequiredBuildingTypes": ["bakery", "kitchen"],
  "Tags": ["baking", "food_processing"]
}
```
