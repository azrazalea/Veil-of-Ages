# /entities/beings

## Purpose

This directory contains concrete Being implementations and related subsystems. The `/beings/` hierarchy organizes entity types by their nature (human, undead) with further specialization for specific creature types. This is where game-specific entity classes are defined.

## Subdirectories

### /health
Body part and health system implementation. Provides detailed body simulation.

### /human
Human entity implementations (villagers, townsfolk).

### /undead
Undead entity hierarchy with `/mindless/` subdirectory containing non-sapient undead like skeletons and zombies.

## Key Architecture Notes

### Entity Hierarchy
```
Being (abstract)
  +-- HumanTownsfolk
  +-- MindlessSkeleton
  +-- MindlessZombie
  +-- Player (in /player directory)
```

### Trait Composition
Each Being type composes its behavior through traits:
- **HumanTownsfolk**: VillagerTrait (which adds LivingTrait + ConsumptionBehaviorTrait)
- **MindlessSkeleton**: MindlessTrait + SkeletonTrait (which extends UndeadBehaviorTrait)
- **MindlessZombie**: MindlessTrait + ZombieTrait (which extends UndeadBehaviorTrait)

### Body Structure Customization
Each Being type can override body structure initialization:
- `InitializeBodyStructure()` - Define body parts and groups
- `InitializeBodySystems()` - Define body systems (sight, hearing, etc.)
- Skeletons remove soft tissues and strengthen bones
- Zombies apply random decay damage on spawn

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits` - Behavior traits
- `VeilOfAges.Entities.Beings.Health` - Body system

### Depended On By
- `/world/` - Entity spawning
- `/entities/traits/` - Type-specific trait behaviors
