# /entities/beings/undead

## Purpose

This directory contains undead entity implementations organized by intelligence level. Currently focuses on "mindless" undead - non-sapient creatures that operate on instinct rather than reasoning.

## Subdirectories

### /mindless
Contains mindless undead entities that cannot engage in complex thought:
- `/skeleton` - Skeletal undead with territorial behavior
- `/zombie` - Shambling undead with hunger-driven behavior

## Undead Trait Hierarchy

```
UndeadTrait (base undead properties)
  +-- UndeadBehaviorTrait (abstract, adds wandering/state machine)
        +-- SkeletonTrait (territorial defense)
        +-- ZombieTrait (hunger-driven wandering)
```

## Common Undead Features

### Disabled Body Systems
All undead have these systems disabled via `UndeadTrait`:
- Pain (undead don't feel pain)
- Breathing
- Blood Pumping
- Blood Filtration
- Digestion
- Sight (uses different perception)
- Hearing (uses different perception)

### Behavior Properties
Common properties defined in `UndeadBehaviorTrait`:
- `WanderProbability` - Chance to start wandering each tick
- `WanderRange` - Maximum distance from spawn
- `IdleTime` - Ticks to wait in idle state

## Important Notes

### Living vs Undead Detection
Entities can check for undead status:
```csharp
if (entity.selfAsEntity().HasTrait<UndeadTrait>())
{
    // Entity is undead
}
```

### Spawn Position Importance
Undead entities track their spawn position and use it for:
- Wander range limits
- Return-to-spawn behavior
- Territory definition (skeletons)

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits` - Undead trait hierarchy

### Depended On By
- Necromancy systems (future)
- Combat systems (for undead-specific damage)
