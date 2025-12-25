# /entities/beings/undead/mindless/skeleton

## Purpose

This directory contains the skeleton entity implementation. Skeletons are faster, more durable undead that exhibit territorial behavior - they patrol an area and investigate/pursue intruders.

## Files

### MindlessSkeleton.cs
Skeletal undead entity with territorial defense behavior.

**Attributes:**
```csharp
Strength: 12, Dexterity: 12, Constitution: 16,
Intelligence: 4, Willpower: 10, Wisdom: 4, Charisma: 1
```

**Configuration:**
- Movement: 0.39 points/tick (faster than villagers, slower than player)
- Bone health scaled to 150% of base
- No soft tissues (removed on initialization)

**Trait Composition:**
- `MindlessTrait` (priority 1) - Limits complex commands
- `SkeletonTrait` (priority 2) - Territorial behavior

**Audio:**
- `AudioStreamPlayer2D` for bone rattle sounds
- Triggered on movement (1% chance) and intruder detection

**Body Modifications in `ModifyForSkeletalStructure()`:**
1. Removes all soft tissues via `Health.RemoveSoftTissuesAndOrgans()`
2. Iterates all body parts and scales bone parts to 150% health

## Key Classes

| Class | Description |
|-------|-------------|
| `MindlessSkeleton` | Territorial skeletal undead entity |

## Important Notes

### Skeleton Behavior (via SkeletonTrait)
The `SkeletonTrait` implements a state machine:
- **Idle**: Stand in place, chance to start wandering
- **Wandering**: Move randomly within territory
- **Defending**: Pursue detected intruder

Territory parameters:
- `TerritoryRange`: 12 tiles from spawn
- `DetectionRange`: 8 tiles
- `IntimidationTime`: 40 ticks of active pursuit

### Bone Identification
Skeleton strengthening affects parts where `IsBonePart()` returns true:
- Skull, Ribs, Spine, Pelvis, Sternum, Clavicles
- Femurs, Tibiae, Humeri, Radii
- Jaw, Fingers, Toes

### Living Detection
Skeletons only react to non-undead entities:
```csharp
if (entity.selfAsEntity().HasTrait<UndeadTrait>())
    continue; // Skip undead
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.MindlessTrait` - Dialogue limitations
- `VeilOfAges.Entities.Traits.SkeletonTrait` - Territorial behavior
- `VeilOfAges.Grid.Area` - Grid system

### Depended On By
- World spawning systems
- Necromancy command system (future)
