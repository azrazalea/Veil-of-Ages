# /entities/needs/strategies

## Purpose

This directory contains concrete implementations of the food acquisition strategy interfaces. Each entity type that needs food has its own set of strategies defining where it gets food, how it travels there, what happens when it eats, and what to do in emergencies.

## Files

### FarmFoodStrategies.cs
Strategies for living entities (villagers) that eat from farms.

**FarmSourceIdentifier:**
- Searches `GridArea.Entities` for buildings with `BuildingType == "Farm"`
- Returns first farm found (or null)

**FarmAcquisitionStrategy:**
- Uses `PathFinder.SetBuildingGoal()` for navigation
- Creates `MoveAlongPathAction` with priority 0
- Considers entity "at farm" when pathfinder goal is reached

**FarmConsumptionEffect:**
- Restores 60 points to the hunger need
- Logs consumption message

**VillagerCriticalHungerHandler:**
- 5% chance per tick to print complaint message
- Returns null (no special action, keep searching)

### GraveyardFoodStrategies.cs
Strategies for zombies that feed at graveyards.

**GraveyardSourceIdentifier:**
- Searches `GridArea.Entities` for buildings with `BuildingType == "Graveyard"`
- Returns first graveyard found (or null)

**GraveyardAcquisitionStrategy:**
- Uses `PathFinder.SetBuildingGoal()` for navigation
- Creates `MoveAlongPathAction` with priority 0
- Same arrival detection as farm strategy

**ZombieConsumptionEffect:**
- Restores 70 points to the hunger need
- Triggers zombie groan sound via `CallDeferred`
- Logs thematic consumption message

**ZombieCriticalHungerHandler:**
- 5% chance per tick to print growl message
- Triggers zombie groan sound
- Returns null (no special action)

## Key Classes

| Class | Description |
|-------|-------------|
| `FarmSourceIdentifier` | Finds farms for food |
| `FarmAcquisitionStrategy` | Paths to farms |
| `FarmConsumptionEffect` | Eating at farms |
| `VillagerCriticalHungerHandler` | Villager starvation |
| `GraveyardSourceIdentifier` | Finds graveyards |
| `GraveyardAcquisitionStrategy` | Paths to graveyards |
| `ZombieConsumptionEffect` | Feeding at graveyards |
| `ZombieCriticalHungerHandler` | Zombie starvation |

## Important Notes

### Strategy Composition
Strategies are composed in trait initialization:
```csharp
// In VillagerTrait.Initialize():
new ConsumptionBehaviorTrait(
    "hunger",
    new FarmSourceIdentifier(),
    new FarmAcquisitionStrategy(),
    new FarmConsumptionEffect(),
    new VillagerCriticalHungerHandler(),
    244  // Duration in ticks
)
```

### Restoration Values
- Villagers: +60 points (decent meal)
- Zombies: +70 points (more substantial feeding)

### Building Type Matching
Source identifiers use exact string matching:
```csharp
building.BuildingType == "Farm"
building.BuildingType == "Graveyard"
```
Building types must match these strings exactly.

### PathFinder Usage
Both acquisition strategies use:
```csharp
_pathfinder.SetBuildingGoal(owner, foodSource)
```
This sets the goal; actual path calculation is lazy.

### Audio Integration
Zombie strategies integrate with MindlessZombie audio:
```csharp
if (owner is MindlessZombie zombie)
{
    zombie.CallDeferred("PlayZombieGroan");
}
```
Uses `CallDeferred` for thread safety.

### Critical Handler Randomization
Both handlers use RandomNumberGenerator with 5% chance:
```csharp
if (_rng.Randf() < 0.05f)
{
    // Print message / play sound
}
```
This prevents log spam while indicating distress.

## Creating New Food Strategies

### Step-by-Step Guide

When adding a new entity type that eats from a different source (e.g., vampires feeding from crypts), implement all four strategy interfaces:

1. **Create a new file** (e.g., `CryptFoodStrategies.cs`)

2. **Implement IFoodSourceIdentifier** - finds the food source:
```csharp
public class CryptSourceIdentifier : IFoodSourceIdentifier
{
    public Building? IdentifyFoodSource(Being owner, Perception perception)
    {
        foreach (var entity in owner.GridArea?.EntitiesGridSystem.OccupiedCells.Values ?? [])
        {
            if (entity is Building building && building.BuildingType == "Crypt")
                return building;
        }
        return null;
    }
}
```

3. **Implement IFoodAcquisitionStrategy** - moves to the food:
```csharp
public class CryptAcquisitionStrategy : IFoodAcquisitionStrategy
{
    private PathFinder _pathfinder = new();

    public EntityAction? GetAcquisitionAction(Being owner, Building foodSource)
    {
        _pathfinder.SetBuildingGoal(owner, foodSource);
        return new MoveAlongPathAction(owner, this, _pathfinder, priority: 0);
    }

    public bool IsAtFoodSource(Being owner, Building foodSource)
    {
        return _pathfinder.IsGoalReached(owner);
    }
}
```

4. **Implement IConsumptionEffect** - applies the consumption result:
```csharp
public class VampireConsumptionEffect : IConsumptionEffect
{
    public void Apply(Being owner, Need need, Building foodSource)
    {
        need.Restore(80f);  // How much to restore
        GD.Print($"{owner.Name} feeds at the crypt...");

        // Optional: Play sound
        if (owner is Vampire vampire)
            vampire.CallDeferred("PlayFeedingSound");
    }
}
```

5. **Implement ICriticalStateHandler** - handles starvation:
```csharp
public class VampireCriticalHungerHandler : ICriticalStateHandler
{
    private RandomNumberGenerator _rng = new();

    public EntityAction? HandleCriticalState(Being owner, Need need)
    {
        if (_rng.Randf() < 0.05f)  // 5% chance per tick
        {
            GD.Print($"{owner.Name} is desperately hungry!");
            // Could return an aggressive action here
        }
        return null;  // Keep searching normally
    }
}
```

6. **Wire up in the entity's trait**:
```csharp
var consumptionTrait = new ConsumptionBehaviorTrait(
    "blood_hunger",
    new CryptSourceIdentifier(),
    new CryptAcquisitionStrategy(),
    new VampireConsumptionEffect(),
    new VampireCriticalHungerHandler(),
    200  // Duration in ticks (longer = slower eater)
);
```

### Key Considerations

- **Building type matching**: Use exact string match for `BuildingType` (e.g., "Farm", "Graveyard", "Crypt")
- **PathFinder reuse**: Keep `_pathfinder` as instance field, reuse across calls
- **Thread safety**: Use `CallDeferred()` for audio or Godot operations
- **Restoration amounts**: Villagers restore 60, zombies restore 70 - balance accordingly
- **Consumption duration**: Villagers use 244 ticks, zombies use 365 (messier eaters)

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner entity
- `VeilOfAges.Entities.Building` - Food source buildings
- `VeilOfAges.Entities.Actions.MoveAlongPathAction` - Movement
- `VeilOfAges.Entities.Beings.MindlessZombie` - Audio playback
- `VeilOfAges.Core.Lib.PathFinder` - Navigation

### Depended On By
- `VeilOfAges.Entities.Traits.VillagerTrait` - Farm strategies
- `VeilOfAges.Entities.Traits.ZombieTrait` - Graveyard strategies
