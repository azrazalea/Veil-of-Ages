# /entities/needs

## Purpose

This directory implements the needs system for entities. Needs represent ongoing requirements (like hunger) that decay over time and must be satisfied.

## Files

### Need.cs
Core need class representing a single need type.

**Properties:**
- `Id` - Unique identifier (e.g., "hunger")
- `DisplayName` - Human-readable name
- `Value` - Current level (0-100, clamped)
- `DecayRate` - Amount decreased per tick

**Thresholds:**
- `CriticalThreshold` (default 10) - Emergency level
- `LowThreshold` (default 30) - Needs attention
- `SatisfiedThreshold` (default 90) - Fully satisfied

**Methods:**
- `Decay()` - Reduce value by decay rate
- `Restore(float)` - Add to value
- `IsCritical()`, `IsLow()`, `IsSatisfied()` - Threshold checks
- `GetStatus()` - String status ("Critical", "Low", "Normal", "Satisfied")

### IFoodStrategies.cs (Legacy - May Be Removed)
Strategy interfaces for the old food acquisition system. These are **no longer actively used** - the system has migrated to `ItemConsumptionBehaviorTrait`.

**Legacy Interfaces:**
- `IFoodSourceIdentifier` - Find appropriate food building
- `IFoodAcquisitionStrategy` - Create movement action to food source
- `IConsumptionEffect` - Execute consumption effects
- `ICriticalStateHandler` - Handle emergency state

**Note:** These interfaces and the old `ConsumptionBehaviorTrait` have been superseded by `ItemConsumptionBehaviorTrait`, which uses items and inventory/storage instead of building-based strategies.

## Subdirectory

### /strategies
Previously contained concrete implementations of the strategy interfaces (FarmFoodStrategies.cs, GraveyardFoodStrategies.cs). These files have been **deleted** as the system now uses `ItemConsumptionBehaviorTrait` instead.

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `Need` | Single need instance with decay |
| `IFoodSourceIdentifier` | (Legacy) Find food source strategy |
| `IFoodAcquisitionStrategy` | (Legacy) Movement to food strategy |
| `IConsumptionEffect` | (Legacy) Consumption result strategy |
| `ICriticalStateHandler` | (Legacy) Emergency state handler |

## Important Notes

### Need Value Direction
Values range 0-100 where:
- **0** = Bad (starving, exhausted)
- **100** = Good (full, well-rested)

This is inverted from some other systems - higher is better.

### Decay System
- Decay happens via `BeingNeedsSystem.UpdateNeeds()`
- Called each game tick
- Rate varies by need and entity type
- Activities can modify decay via `NeedDecayMultipliers` (e.g., sleep slows hunger)
- Example: Villager hunger decays at 0.02/tick, zombie at 0.0015/tick

### Existing Needs

**Hunger** (living entities):
- Added by: `LivingTrait`
- Initial: 75, Decay: 0.02/tick, Thresholds: 15/40/90
- Satisfied by: `ItemConsumptionBehaviorTrait` with food items from inventory or home storage
- Modified by activities:
  - Sleep: 0.25x decay (slower)
  - Work: 1.2x decay (faster)

**Energy** (living entities):
- Added by: `LivingTrait`
- Initial: 100, Decay: 0.008/tick, Thresholds: 20/40/80
- Satisfied by: `SleepActivity` (restores 0.15/tick)
- Modified by activities:
  - Sleep: 0x decay + direct restoration
  - Work: direct cost (0.05/tick spent)

**Brain Hunger** (zombies):
- Added by: `ZombieTrait`
- Initial: 60, Decay: 0.0015/tick (very slow)
- Satisfied by: `ItemConsumptionBehaviorTrait` with "zombie_food" tagged items

### ItemConsumptionBehaviorTrait (Current System)
The new item-based consumption system uses `ItemConsumptionBehaviorTrait` which:
1. Checks if the need is low/critical
2. Looks for food in inventory first, then home storage
3. Starts `ConsumeItemActivity` to handle consumption
4. Uses tag-based food identification (e.g., "food", "zombie_food")

```csharp
// Villagers consume items with "food" tag
new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",
    () => villagerTrait?.Home,
    restoreAmount: 60f,
    consumptionDuration: 244
);

// Zombies consume items with "zombie_food" tag
new ItemConsumptionBehaviorTrait(
    "Brain Hunger",
    "zombie_food",
    () => null,  // No home for zombies
    restoreAmount: 70f,
    consumptionDuration: 365
);
```

### Legacy Strategy Pattern (Deprecated)
The old strategy pattern (`ConsumptionBehaviorTrait` with `IFoodSourceIdentifier`, `IFoodAcquisitionStrategy`, etc.) has been replaced by the item-based system above. The strategy interfaces in `IFoodStrategies.cs` may be removed in a future cleanup.

## Creating a New Need

### Step-by-Step Guide

1. **Define the need** in a trait's `Initialize()` method:
```csharp
_owner?.NeedsSystem.AddNeed(new Need(
    "thirst",           // id - unique identifier
    "Thirst",           // displayName - shown in UI
    80f,                // initialValue - starting level (0-100)
    0.01f,              // decayRate - per tick decrease
    15f,                // criticalThreshold - emergency level
    30f,                // lowThreshold - needs attention
    90f                 // satisfiedThreshold - fully satisfied
));
```

2. **Wire up with ItemConsumptionBehaviorTrait** (recommended approach):
```csharp
var consumptionTrait = new ItemConsumptionBehaviorTrait(
    "thirst",                        // needId - matches the Need's id
    "drink",                         // foodTag - tag to identify consumable items
    () => _home,                     // getHome - function to get home building
    restoreAmount: 50f,              // how much to restore on consumption
    consumptionDuration: 120         // ticks to spend consuming
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

3. **Ensure items exist** with the appropriate tag in your item definitions (e.g., items with "drink" tag for thirst).

### Key Considerations

- **Value direction**: 0 = bad (starving), 100 = good (full)
- **Decay rate**: Villager hunger is 0.02/tick, zombie is 0.0015/tick (much slower)
- **Thresholds**: Critical < Low < Satisfied (e.g., 15, 30, 90)
- **ItemConsumptionBehaviorTrait priority**: Should be `Priority - 1` so it can override the parent trait when the entity needs satisfaction
- **Item tags**: Use the item tagging system (in item definitions) to identify consumables for each need

### Decay Rate Reference

At 8 ticks/second:
- `0.02` = ~62.5 seconds from 100 to 0 (hunger)
- `0.01` = ~125 seconds from 100 to 0
- `0.008` = ~156 seconds from 100 to 0 (energy)
- `0.0015` = ~833 seconds (~14 minutes) from 100 to 0 (zombie brain hunger)

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner reference
- `VeilOfAges.Entities.Building` - Home storage access
- `VeilOfAges.Entities.Items` - Item and inventory system

### Depended On By
- `VeilOfAges.Entities.BeingServices.BeingNeedsSystem` - Need management
- `VeilOfAges.Entities.Traits.ItemConsumptionBehaviorTrait` - Need satisfaction via items
- `VeilOfAges.Entities.Activities.ConsumeItemActivity` - Actual consumption logic
- Individual trait implementations (VillagerTrait, ZombieTrait)
