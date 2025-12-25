# /entities/beings/undead/mindless/zombie

## Purpose

This directory contains the zombie entity implementation. Zombies are slow, shambling undead driven by hunger. They seek out graveyards to satisfy their hunger for brains and wander aimlessly when sated.

## Files

### MindlessZombie.cs
Shambling undead entity with hunger-driven behavior.

**Attributes:**
```csharp
Strength: 12, Dexterity: 6, Constitution: 14,
Intelligence: 3, Willpower: 8, Wisdom: 4, Charisma: 2
```

**Configuration:**
- Movement: 0.15 points/tick (very slow, ~6.67 ticks per tile)
- Random decay damage on spawn (2-5 body parts affected)
- Standard body structure with decay simulation

**Trait Composition:**
- `MindlessTrait` (priority 1) - Limits complex commands
- `ZombieTrait` (priority 2) - Hunger/wandering behavior

**Audio:**
- `AudioStreamPlayer2D` for groan sounds
- Triggered on feeding and random wandering

**Decay System in `ApplyRandomDecayDamage()`:**
1. Collects all non-vital body parts (excludes Brain, Heart, Spine)
2. Shuffles candidates randomly
3. Applies 30-70% damage to 2-5 random parts
4. Logs which parts show decay

## Key Classes

| Class | Description |
|-------|-------------|
| `MindlessZombie` | Hunger-driven shambling undead |

## Important Notes

### Zombie Behavior (via ZombieTrait)
ZombieTrait implements a simple state machine:
- **Idle**: Stand in place, chance to groan and start wandering
- **Wandering**: Move randomly, further range than skeletons

Wandering parameters:
- `WanderProbability`: 0.3 (30% chance to wander)
- `WanderRange`: 15 tiles from spawn

### Hunger System
ZombieTrait initializes a special hunger need:
```csharp
new Need("hunger", "Brain Hunger", 60f, 0.0015f, 15f, 40f, 90f)
```
- Initial value: 60 (somewhat hungry on spawn)
- Decay rate: 0.0015 per tick (very slow decay)
- Critical threshold: 15
- Uses `GraveyardSourceIdentifier` to find food

### Consumption Behavior
Adds `ConsumptionBehaviorTrait` with zombie-specific strategies:
- Source: `GraveyardSourceIdentifier`
- Acquisition: `GraveyardAcquisitionStrategy`
- Effect: `ZombieConsumptionEffect` (restores 70, plays groan)
- Critical handler: `ZombieCriticalHungerHandler`
- Duration: 365 ticks (messier eaters)

### Decay Damage Protection
Vital parts are protected from decay damage:
```csharp
if (part.Name != "Brain" && part.Name != "Heart" && part.Name != "Spine")
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.MindlessTrait` - Dialogue limitations
- `VeilOfAges.Entities.Traits.ZombieTrait` - Hunger/wandering behavior
- `VeilOfAges.Entities.Needs.Strategies` - Graveyard food strategies
- `VeilOfAges.Grid.Area` - Grid system

### Depended On By
- World spawning systems
- Necromancy command system (future)
