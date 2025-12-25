# /entities/beings/human

## Purpose

This directory contains human entity implementations. Currently contains the basic villager/townsfolk type that populates villages in the game world.

## Files

### HumanTownsfolk.cs
Basic human NPC that lives in villages.

**Configuration:**
- Uses base attribute set (all 10s)
- Movement speed: 0.33 points/tick (3.33 ticks per tile)
- Standard humanoid body structure

**Trait Composition:**
- `VillagerTrait` (priority 1) - Added in `_Ready()`
  - VillagerTrait automatically adds `LivingTrait` and `ConsumptionBehaviorTrait`

**Key Overrides:**
- `_Ready()` - Initializes VillagerTrait with health reference
- `Initialize()` - Sets movement speed before base initialization

## Key Classes

| Class | Description |
|-------|-------------|
| `HumanTownsfolk` | Basic human villager NPC |

## Important Notes

### Trait Order
The `_Ready()` method initializes the VillagerTrait with explicit health access:
```csharp
var villagerTrait = new VillagerTrait();
villagerTrait.Initialize(this, Health);
selfAsEntity().AddTrait(villagerTrait, 1);
```
This pattern ensures health is available before trait initialization.

### Villager Behavior
Villagers exhibit autonomous behavior through VillagerTrait:
- Idle at home position
- Visit village square periodically
- Wander to known buildings
- Seek food from farms when hungry

### Spawning
Typically spawned by village generation code with:
- A designated home position (spawn position)
- Access to the grid area
- Automatic building discovery on initialization

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.VillagerTrait` - Behavior
- `VeilOfAges.Grid.Area` - Grid system

### Depended On By
- Village generation systems
- World spawning logic
