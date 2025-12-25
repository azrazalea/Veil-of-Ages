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

## Creating a New Being Type

### Step-by-Step Guide

1. **Create the directory and file** (e.g., `/entities/beings/undead/sapient/Lich.cs`)

2. **Create the Godot scene** (`.tscn`) with:
   - Root node: Your Being class (CharacterBody2D)
   - Child: `AnimatedSprite2D` named "AnimatedSprite2D" with idle/walk animations
   - Optional: `AudioStreamPlayer2D` for sounds

3. **Basic Being template**:
```csharp
namespace VeilOfAges.Entities.Beings;

public partial class MyNewBeing : Being
{
    // Define attributes
    private static readonly BeingAttributes _attributes = new(
        Strength: 10,
        Dexterity: 10,
        Constitution: 10,
        Intelligence: 10,
        Willpower: 10,
        Wisdom: 10,
        Charisma: 10
    );

    public MyNewBeing() : base(_attributes) { }

    public override void Initialize(Grid.Area gridArea, Vector2I? gridPosition = null)
    {
        // Set movement speed BEFORE base.Initialize()
        _movementPointsPerTick = 0.33f; // Tiles per tick (lower = slower)
        base.Initialize(gridArea, gridPosition);
    }

    public override void _Ready()
    {
        base._Ready();

        // Add traits - order matters!
        selfAsEntity().AddTraitToQueue<MyBaseTrait>(0);      // Priority 0 = first
        selfAsEntity().AddTraitToQueue<MyBehaviorTrait>(1);  // Priority 1 = second
    }

    // Optional: Customize body structure
    protected override void InitializeBodyStructure()
    {
        base.InitializeBodyStructure(); // Standard humanoid
        // Modify body parts here
    }
}
```

4. **Register with spawning system** - Add to village generation or create a spawning method

### Key Considerations

- **Movement speed reference**:
  - Player: 0.5 (fastest, 2 ticks/tile)
  - Skeleton: 0.39 (~2.5 ticks/tile)
  - Villager: 0.33 (~3 ticks/tile)
  - Zombie: 0.15 (slowest, ~6.7 ticks/tile)

- **Trait composition**: Living entities typically need:
  - `LivingTrait` (adds hunger need)
  - `ConsumptionBehaviorTrait` (handles eating)
  - A behavior trait (e.g., `VillagerTrait`)

- **Undead entities** typically need:
  - `UndeadTrait` (disables living body systems)
  - `MindlessTrait` (if non-sapient, limits dialogue)
  - A behavior trait extending `UndeadBehaviorTrait`

### Body Customization Examples

**For skeletal entities** (remove organs, strengthen bones):
```csharp
protected override void InitializeBodyStructure()
{
    base.InitializeBodyStructure();
    Health.RemoveSoftTissuesAndOrgans();

    foreach (var group in Health.BodyPartGroups.Values)
        foreach (var part in group.Parts.Values)
            if (part.IsBonePart())
                part.MaxHealth = (int)(part.MaxHealth * 1.5f);
}
```

**For decayed entities** (random damage):
```csharp
// Apply 30-70% damage to 2-5 random non-vital parts
var candidates = GetNonVitalBodyParts();
var numDamaged = _rng.RandiRange(2, 5);
foreach (var part in candidates.Take(numDamaged))
    part.CurrentHealth = (int)(part.MaxHealth * _rng.RandfRange(0.3f, 0.7f));
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits` - Behavior traits
- `VeilOfAges.Entities.Beings.Health` - Body system

### Depended On By
- `/world/` - Entity spawning
- `/entities/traits/` - Type-specific trait behaviors
