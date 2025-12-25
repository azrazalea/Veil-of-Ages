# /entities/needs

## Purpose

This directory implements the needs system for entities. Needs represent ongoing requirements (like hunger) that decay over time and must be satisfied. The system uses a strategy pattern to allow different entity types to satisfy needs in different ways.

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

### IFoodStrategies.cs
Strategy interfaces for the food acquisition system.

**IFoodSourceIdentifier:**
- `IdentifyFoodSource(owner, perception)` - Find appropriate food building

**IFoodAcquisitionStrategy:**
- `GetAcquisitionAction(owner, foodSource)` - Create movement action
- `IsAtFoodSource(owner, foodSource)` - Check arrival

**IConsumptionEffect:**
- `Apply(owner, need, foodSource)` - Execute consumption effects

**ICriticalStateHandler:**
- `HandleCriticalState(owner, need)` - Handle emergency state

## Subdirectory

### /strategies
Contains concrete implementations of the strategy interfaces for different entity types.

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `Need` | Single need instance with decay |
| `IFoodSourceIdentifier` | Find food source strategy |
| `IFoodAcquisitionStrategy` | Movement to food strategy |
| `IConsumptionEffect` | Consumption result strategy |
| `ICriticalStateHandler` | Emergency state handler |

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
- Example: Villager hunger decays at 0.02/tick, zombie at 0.0015/tick

### Strategy Pattern Usage
The strategy pattern allows entity-specific behavior:
```csharp
// Villagers use farms
new FarmSourceIdentifier()
new FarmAcquisitionStrategy()
new FarmConsumptionEffect()

// Zombies use graveyards
new GraveyardSourceIdentifier()
new GraveyardAcquisitionStrategy()
new ZombieConsumptionEffect()
```

### Integration with ConsumptionBehaviorTrait
The strategies are consumed by `ConsumptionBehaviorTrait` which:
1. Uses identifier to find food
2. Uses acquisition to move to food
3. Handles consumption timing
4. Uses effect to apply results
5. Uses critical handler when needed

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner reference
- `VeilOfAges.Entities.Building` - Food source buildings
- `VeilOfAges.Entities.Sensory.Perception` - For source identification

### Depended On By
- `VeilOfAges.Entities.BeingServices.BeingNeedsSystem` - Need management
- `VeilOfAges.Entities.Traits.ConsumptionBehaviorTrait` - Need satisfaction
- Individual trait implementations (VillagerTrait, ZombieTrait)
