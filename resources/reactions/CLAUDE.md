# /resources/reactions

## Purpose

This directory contains JSON definitions for reaction templates. Reactions define item transformations - converting input items into output items over a duration. This enables crafting, processing, and production workflows (e.g., milling wheat into flour, baking flour into bread).

Reactions are loaded by `ReactionResourceManager` at game initialization and queried by job traits to determine what work can be performed.

## JSON Schema

Each reaction JSON file defines a single reaction with the following fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier for this reaction (e.g., `"mill_wheat"`) |
| `Name` | string | Yes | Display name shown to players (e.g., `"Mill Wheat"`) |
| `Description` | string | No | Explanatory text about what the reaction does |
| `Inputs` | ItemQuantity[] | Yes | Items consumed by this reaction (at least one required) |
| `Outputs` | ItemQuantity[] | Yes | Items produced by this reaction (at least one required) |
| `Duration` | uint | Yes | Number of game ticks to complete (must be > 0) |
| `RequiredFacilities` | string[] | No | Facility IDs required to perform this reaction (empty = anywhere) |
| `Tags` | string[] | No | Tags for categorization and job matching |

### ItemQuantity Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ItemId` | string | Yes | Unique identifier of the item definition |
| `Quantity` | int | Yes | Number of items (must be >= 1) |

## Current Reactions

### mill_wheat.json
Grinds wheat into flour using a quern or millstone.

| Property | Value |
|----------|-------|
| Inputs | 2x wheat |
| Outputs | 1x flour |
| Duration | 200 ticks (~25 seconds at normal speed) |
| Required Facilities | quern |
| Tags | milling, processing |

### bake_bread.json
Bakes flour into a fresh loaf of bread.

| Property | Value |
|----------|-------|
| Inputs | 2x flour |
| Outputs | 1x bread |
| Duration | 300 ticks (~37.5 seconds at normal speed) |
| Required Facilities | oven |
| Tags | baking, cooking |

## How to Add New Reactions

1. **Create a JSON file** in this directory (e.g., `smelt_iron.json`)

2. **Define required fields**:
   ```json
   {
       "Id": "smelt_iron",
       "Name": "Smelt Iron Ore",
       "Description": "Smelt iron ore into iron bars in a forge",
       "Inputs": [
           {"ItemId": "iron_ore", "Quantity": 3}
       ],
       "Outputs": [
           {"ItemId": "iron_bar", "Quantity": 1}
       ],
       "Duration": 400,
       "RequiredFacilities": ["forge"],
       "Tags": ["smelting", "metalworking"]
   }
   ```

3. **Validation requirements**:
   - `Id` must be unique across all reactions
   - `Name` must be non-empty
   - At least one input and one output required
   - All input/output `ItemId` values must be non-empty
   - All input/output `Quantity` values must be >= 1
   - `Duration` must be > 0

4. **Subdirectories**: Reactions can be organized in subdirectories (e.g., `resources/reactions/food/`, `resources/reactions/smithing/`). The `ReactionResourceManager` recursively loads from all subdirectories.

## Tags and Job Matching

Tags are used by job traits to find relevant reactions:

| Tag | Used By | Description |
|-----|---------|-------------|
| `milling` | BakerJobTrait | Grain processing reactions |
| `baking` | BakerJobTrait | Bread and pastry creation |
| `processing` | (general) | Raw material processing |
| `cooking` | (general) | Food preparation |

**How job matching works:**

```csharp
// BakerJobTrait looks for reactions with these tags, in priority order
private static readonly string[] _reactionTags = ["baking", "milling"];

// It queries the ReactionResourceManager
foreach (var reaction in ReactionResourceManager.Instance.GetReactionsByTag(tag))
{
    if (reaction.CanPerformWith(availableFacilities) && HasRequiredInputs(reaction, storage))
    {
        // Start processing this reaction
    }
}
```

**Tag naming conventions:**
- Use lowercase with underscores for multi-word tags (e.g., `food_processing`)
- Be specific enough for job matching but general enough for grouping
- Common categories: processing, crafting, cooking, smelting, woodworking

## Required Facilities

Facilities represent equipment or workstations needed to perform a reaction:

| Facility | Example Reactions |
|----------|-------------------|
| `quern` | mill_wheat (hand-grinding grain) |
| `oven` | bake_bread (baking) |
| `forge` | (future) smelting, smithing |
| `tanning_rack` | (future) leather processing |

**Facility matching:**
- If `RequiredFacilities` is empty, the reaction can be performed anywhere
- If specified, ALL listed facilities must be available at the workplace
- Facility matching is case-insensitive
- Buildings expose available facilities via `Building.GetFacilities()`

## Duration Guidelines

Duration is measured in game ticks. At normal game speed (8 ticks/second):

| Duration | Real Time | Game Time | Use For |
|----------|-----------|-----------|---------|
| 80 | ~10 sec | ~5 min | Quick tasks (mixing, cutting) |
| 200 | ~25 sec | ~12 min | Medium tasks (milling, basic crafting) |
| 300 | ~37.5 sec | ~18 min | Longer tasks (baking, smelting) |
| 600 | ~75 sec | ~36 min | Extended tasks (complex crafting) |

## Important Notes

### Loading
- Reactions are loaded once at game initialization by `ReactionResourceManager.Initialize()`
- JSON parsing is case-insensitive for property names
- Invalid reactions (failing validation) are logged and skipped

### Thread Safety
- `ReactionResourceManager` is a singleton, not thread-safe during initialization
- Once loaded, reaction definitions are read-only and safe to access from any thread

### File Location
- JSON files must be in `res://resources/reactions/` or a subdirectory
- Files must have `.json` extension

## Dependencies

### Loaded By
- `VeilOfAges.Entities.Reactions.ReactionResourceManager` - Singleton manager

### Used By
- `VeilOfAges.Entities.Traits.BakerJobTrait` - Queries reactions by tag
- `VeilOfAges.Entities.Activities.ProcessReactionActivity` - Executes reactions

### Related Files
- `/entities/reactions/ReactionDefinition.cs` - C# class definition
- `/entities/reactions/ReactionResourceManager.cs` - Singleton loader
- `/entities/reactions/ItemQuantity.cs` - Item/quantity record
