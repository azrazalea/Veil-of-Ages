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
- `Duration` - Number of game ticks required to complete (uint)
- `RequiredFacilities` - Facilities/equipment required to perform (e.g., "millstone", "oven", "forge")
- `Tags` - Tags for categorization and job matching (e.g., "milling", "baking", "smithing")

**Key Methods:**
- `LoadFromJson(path)` - Load definition from JSON file
- `Validate()` - Validate required fields and constraints
- `HasTag(tag)` - Check if reaction has a specific tag (case-insensitive)
- `CanPerformWith(availableFacilities)` - Check if reaction can be performed with available facilities
- `RequiresFacility(facilityId)` - Check if a specific facility is required (case-insensitive)

### ReactionResourceManager.cs
Singleton manager for loading and accessing reaction definitions. Registered as a Godot autoload in `project.godot`.

**Key Features:**
- Godot Node autoload pattern (extends `Node`)
- Loads reaction definitions from `res://resources/reactions/*.json` on `_Ready()`
- Supports subdirectories for organization
- Query methods for filtering by tag or facility requirements

**Key Methods:**
- `GetDefinition(id)` - Get definition by ID, returns null if not found
- `GetAllDefinitions()` - Enumerate all loaded definitions
- `GetReactionsByTag(tag)` - Filter definitions by tag
- `GetReactionsForFacilities(availableFacilities)` - Get all reactions performable with given facilities
- `GetReactionsRequiringFacility(facilityId)` - Get all reactions that require a specific facility
- `HasDefinition(id)` - Check if definition exists

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `ItemQuantity` | Record for item ID + quantity pairs |
| `ReactionDefinition` | JSON-serializable reaction template |
| `ReactionResourceManager` | Singleton manager for definitions |

## Important Notes

### Facility System
- `RequiredFacilities` specifies equipment/facilities needed to perform the reaction
- If `RequiredFacilities` is empty, the reaction can be performed anywhere
- If `RequiredFacilities` has entries, ALL listed facilities must be available
- Facility matching is case-insensitive
- Examples: "millstone", "oven", "forge", "mortar_and_pestle"

### Tag System
- Tags are used for job matching (e.g., a Miller job trait looks for "milling" tagged reactions)
- Tag matching is case-insensitive
- Multiple tags can be assigned to a single reaction
- Common tag categories: "milling", "baking", "smithing", "food_processing"

### Duration
- Duration is measured in game ticks (uint)
- At normal game speed, 8 ticks = 1 real second
- Duration of 0 is invalid (validation fails)
- A duration of 80 ticks = 10 seconds real time at normal speed

### Input/Output Validation
- At least one input item is required
- At least one output item is required
- All input/output ItemIds must be non-empty
- All quantities must be at least 1

### Thread Safety
- `ReactionResourceManager` is not thread-safe during initialization
- Once initialized, read-only access to definitions is safe
- `ReactionDefinition` instances are immutable after loading and safe to read from multiple threads

## Dependencies

### Depends On
- `VeilOfAges.Core.Lib` - Log class, JsonOptions
- `Godot` - ProjectSettings for path globalization
- `System.Text.Json` - JSON serialization

### Depended On By
- (Future) Job/work system for assigning reactions to workers
- (Future) Building production queues
- (Future) AI task selection for crafting
- `/entities/items/` - ItemQuantity references item definition IDs

## Creating New Reaction Definitions

1. Create a JSON file in `resources/reactions/` (or a subdirectory for organization)
2. Define required fields: `Id`, `Name`, `Inputs`, `Outputs`, `Duration`
3. Add optional fields: `Description`, `RequiredFacilities`, `Tags`

### JSON Schema

```json
{
  "Id": "string (required, unique identifier)",
  "Name": "string (required, display name)",
  "Description": "string (optional, explanation text)",
  "Inputs": [
    { "ItemId": "string", "Quantity": number }
  ],
  "Outputs": [
    { "ItemId": "string", "Quantity": number }
  ],
  "Duration": number,
  "RequiredFacilities": ["string", "string"],
  "Tags": ["string", "string"]
}
```

### Example: Milling Wheat into Flour

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
  "RequiredFacilities": ["millstone"],
  "Tags": ["milling", "food_processing"]
}
```

### Example: Baking Bread

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
  "RequiredFacilities": ["oven"],
  "Tags": ["baking", "food_processing"]
}
```

### Example: Smithing (Multiple Facilities)

```json
{
  "Id": "forge_iron_ingot",
  "Name": "Forge Iron Ingot",
  "Description": "Smelt iron ore into an ingot",
  "Inputs": [
    { "ItemId": "iron_ore", "Quantity": 3 },
    { "ItemId": "coal", "Quantity": 1 }
  ],
  "Outputs": [
    { "ItemId": "iron_ingot", "Quantity": 1 }
  ],
  "Duration": 160,
  "RequiredFacilities": ["forge", "anvil"],
  "Tags": ["smithing", "metalworking"]
}
```

### Example: No Facility Required

```json
{
  "Id": "craft_rope",
  "Name": "Craft Rope",
  "Description": "Braid plant fibers into rope",
  "Inputs": [
    { "ItemId": "plant_fiber", "Quantity": 5 }
  ],
  "Outputs": [
    { "ItemId": "rope", "Quantity": 1 }
  ],
  "Duration": 40,
  "RequiredFacilities": [],
  "Tags": ["crafting", "basic"]
}
```
