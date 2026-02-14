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

## Entity Initialization Patterns

Understanding Godot's initialization timing is critical when spawning entities and configuring their traits.

### _Ready() is Asynchronous

When you call `AddChild(being)`, Godot calls `_Ready()` **asynchronously** - you cannot rely on it being called immediately after `AddChild()`. This has major implications for trait access.

### Trait Initialization Timing

Traits are initialized during `_Ready()` via Being's init queue processing. You **CANNOT** call `GetTrait<T>()` immediately after `Initialize()` or `AddChild()` - the trait won't exist yet.

```csharp
// WRONG - trait doesn't exist yet!
being.Initialize(gridArea, pos);
container.AddChild(being);
var trait = being.GetTrait<VillagerTrait>();  // Returns null or throws!

// WRONG - still doesn't work
being.Initialize(gridArea, pos);
container.AddChild(being);
await ToSignal(GetTree(), "process_frame");  // Even waiting a frame is unreliable
var trait = being.GetTrait<VillagerTrait>();  // May still fail
```

### The PendingData Pattern

To pass data to a trait that will be created during `_Ready()`:

1. **Add a property to the Being subclass** (e.g., `PendingHome` on HumanTownsfolk)
2. **Set it BEFORE calling AddChild()**
3. **In _Ready(), create the trait with that data** (e.g., `new VillagerTrait(PendingHome)`)
4. This ensures the trait has the data when it initializes

```csharp
// In HumanTownsfolk.cs
public Building? PendingHome { get; set; }

public override void _Ready()
{
    base._Ready();
    // VillagerTrait receives PendingHome during construction
    selfAsEntity().AddTraitToQueue(new VillagerTrait(PendingHome), 0);
}
```

### Correct Spawn Sequence

```csharp
// 1. Create and configure the being
var being = new HumanTownsfolk();

// 2. Call Initialize - sets up Being basics, but traits NOT initialized yet
being.Initialize(gridArea, pos);

// 3. Set any pending data BEFORE AddChild
townsfolk.PendingHome = home;

// 4. AddChild triggers _Ready() asynchronously
container.AddChild(being);

// DON'T try to access traits here - they may not exist yet!
// If you need post-init logic, use signals or callbacks
```

### Why Lambda Capture Works

The lambda capture pattern (e.g., `getHome: () => _home`) works because it captures the **field reference**, not the value. So setting the field before or during `_Ready()` still works for lambdas that are called later.

```csharp
// This works because the lambda captures the field reference
private Building? _home;

public VillagerTrait(Building? initialHome)
{
    _home = initialHome;
    // This lambda will read _home when called, not when constructed
    _getHome = () => _home;
}
```

### Common Mistakes

1. **Trying to configure traits after AddChild()**: Traits don't exist yet
2. **Assuming _Ready() is synchronous**: It's called by Godot's deferred call system
3. **Not using PendingData pattern**: Leads to null traits or missing configuration
4. **Forgetting to set PendingData before AddChild()**: Data won't be available in _Ready()

## Creating a New Being Type

### Step-by-Step Guide

1. **Create the directory and file** (e.g., `/entities/beings/undead/sapient/Lich.cs`)

2. **Create the Godot scene** (`.tscn`) with:
   - Root node: Your Being class (CharacterBody2D)
   - Child: `Sprite2D` node (configured at runtime from sprite definition)
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

## Data-Driven Entity System

The data-driven entity system allows entities to be defined in JSON rather than hardcoded C# classes.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                 BeingResourceManager (Singleton)                │
├─────────────────────────────────────────────────────────────────┤
│  Dictionary<string, BeingDefinition> _definitions               │
│  Dictionary<string, SpriteDefinition> _sprites                  │
└─────────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┴────────────────────┐
         ▼                                         ▼
┌─────────────────────┐               ┌─────────────────────────┐
│ BeingDefinition     │               │ SpriteDefinition        │
│ - Id, Name, Category│               │ - Id, Name, SpriteSize  │
│ - Attributes (7)    │───references──│ - TexturePath           │
│ - Movement speed    │               │ - Layers[] OR Row/Col   │
│ - Traits[]          │               │ (static sprite)         │
│ - Body modifications│               └─────────────────────────┘
│ - Audio config      │                         │
└─────────────────────┘                         ▼
         │                            ┌─────────────────────────┐
         ▼                            │ AtlasTexture (Godot)    │
┌─────────────────────┐               │ Created at runtime      │
│ TraitFactory        │               └─────────────────────────┘
│ - Create traits     │
│ - Merge JSON +      │
│   runtime params    │
└─────────────────────┘
```

### New Files

#### BeingDefinition.cs
JSON-serializable definition class containing:
- `Id`, `Name`, `Description`, `Category`
- `SpriteId` - Reference to sprite definition (backwards compatible `AnimationId` getter/setter)
- `Attributes` - The 7 being attributes
- `Movement` - Movement speed configuration
- `Traits[]` - List of trait definitions with parameters
- `Body` - Body structure and modifications
- `Audio` - Sound configuration
- `Tags[]` - Categorization tags

#### SpriteDefinition.cs
JSON-serializable sprite definition for static entity sprites:
- `Id`, `Name`, `TexturePath`, `SpriteSize`
- Either `Layers[]` array (multi-layer sprites) or top-level `Row`/`Col` (single sprite)
- Creates `Sprite2D` nodes with `AtlasTexture` at runtime

#### BeingResourceManager.cs
Singleton manager for loading entity resources:
- Loads from `res://resources/entities/definitions/` and `sprites/`
- `GetDefinition(id)` - Get entity definition
- `GetSprite(id)` - Get sprite definition
- `GetDefinitionsByTag(tag)` - Filter by tag
- `GetDefinitionsByCategory(category)` - Filter by category

#### GenericBeing.cs
Data-driven Being implementation:
- Factory method: `CreateFromDefinition(definitionId, runtimeParams)`
- Loads traits, sprites, body modifications from definition
- Supports runtime parameters merged with JSON parameters
- Used instead of specific Being subclasses
- `ConfigureSprites(SpriteDefinition, BeingDefinition)` creates `Sprite2D` nodes with `AtlasTexture`
- `SpriteLayers` is `Dictionary<string, Sprite2D>` keyed by layer name

#### generic_being.tscn
Minimal Godot scene containing:
- CharacterBody2D root (GenericBeing.cs script)
- Sprite2D child (configured at runtime from sprite definition)
- AudioStreamPlayer2D child (for sounds)

### Creating Data-Driven Entities

```csharp
// Spawn from definition with optional runtime parameters
var runtimeParams = new Dictionary<string, object?>
{
    { "home", homeBuilding }
};
var being = GenericBeing.CreateFromDefinition("human_townsfolk", runtimeParams);
if (being != null)
{
    being.Initialize(gridArea, position, gameController);
    container.AddChild(being);
}
```

### Trait Configuration System

Traits receive configuration from two sources:
1. **JSON Parameters** - Static values defined in the trait definition
2. **Runtime Parameters** - Dynamic values passed at spawn time

Both are merged into a `TraitConfiguration` object and passed to the trait's `Configure()` method.

```csharp
// In trait class:
public override void Configure(TraitConfiguration config)
{
    var home = config.GetBuilding("home");
    if (home != null)
    {
        SetHome(home);
    }
}
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits` - Behavior traits
- `VeilOfAges.Entities.Beings.Health` - Body system

### Depended On By
- `/world/` - Entity spawning
- `/entities/traits/` - Type-specific trait behaviors
