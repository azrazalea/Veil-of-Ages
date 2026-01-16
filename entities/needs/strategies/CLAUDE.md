# /entities/needs/strategies

## Purpose

This directory previously contained concrete implementations of the food acquisition strategy interfaces for different entity types. **These files have been deleted** as the system has migrated to `ItemConsumptionBehaviorTrait`.

## Deleted Files

### FarmFoodStrategies.cs (DELETED)
Previously contained strategies for living entities (villagers) that ate from farms:
- `FarmSourceIdentifier` - Found buildings with `BuildingType == "Farm"`
- `FarmAcquisitionStrategy` - Pathed to farms
- `FarmConsumptionEffect` - Restored hunger at farms
- `VillagerCriticalHungerHandler` - Handled villager starvation

### GraveyardFoodStrategies.cs (DELETED)
Previously contained strategies for zombies that fed at graveyards:
- `GraveyardSourceIdentifier` - Found buildings with `BuildingType == "Graveyard"`
- `GraveyardAcquisitionStrategy` - Pathed to graveyards
- `ZombieConsumptionEffect` - Restored hunger at graveyards
- `ZombieCriticalHungerHandler` - Handled zombie starvation

## Migration Notes

The strategy-based consumption system has been replaced by `ItemConsumptionBehaviorTrait`, which uses items and inventory/storage instead of building-based strategies:

**Old System:**
```csharp
// ConsumptionBehaviorTrait with strategy interfaces
new ConsumptionBehaviorTrait(
    "hunger",
    new FarmSourceIdentifier(),
    new FarmAcquisitionStrategy(),
    new FarmConsumptionEffect(),
    new VillagerCriticalHungerHandler(),
    244
);
```

**New System:**
```csharp
// ItemConsumptionBehaviorTrait with tag-based food identification
new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",  // Tag to identify food items
    () => villagerTrait?.Home,  // Home for storage access
    restoreAmount: 60f,
    consumptionDuration: 244
);
```

## Key Differences

| Aspect | Old Strategy System | New Item System |
|--------|---------------------|-----------------|
| Food Source | Buildings (Farm, Graveyard) | Items in inventory/storage |
| Movement | `IFoodAcquisitionStrategy` handled navigation | `ConsumeItemActivity` handles everything |
| Configuration | Four separate strategy classes | Single trait with parameters |
| Identification | Building type matching | Item tag matching |

## Legacy Interfaces (In Parent Directory)

The strategy interfaces in `/entities/needs/IFoodStrategies.cs` are still present but are legacy code:
- `IFoodSourceIdentifier`
- `IFoodAcquisitionStrategy`
- `IConsumptionEffect`
- `ICriticalStateHandler`

These interfaces and the old `ConsumptionBehaviorTrait` may be removed in a future cleanup.

## See Also

- `/entities/traits/ItemConsumptionBehaviorTrait.cs` - Current consumption implementation
- `/entities/activities/ConsumeItemActivity.cs` - Activity that handles item consumption
- `/entities/needs/CLAUDE.md` - Parent directory documentation
