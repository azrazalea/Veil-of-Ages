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
