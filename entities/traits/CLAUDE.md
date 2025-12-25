# /entities/traits

## Purpose

This directory contains all trait implementations for Being entities. Traits are modular behavior components that define how entities act, react, and interact. The trait system follows a composition pattern where complex behaviors emerge from combining simpler traits.

## Files

### ConsumptionBehaviorTrait.cs
Generic trait for satisfying needs by consuming from sources.

**Features:**
- Strategy-based food source identification
- Path-based movement to sources
- Timed consumption with configurable duration
- Critical state handling

**State Machine:**
1. Check if need is low
2. Find food source using identifier strategy
3. Move to source using acquisition strategy
4. Consume (timer-based idle)
5. Apply effects using consumption strategy

**Constructor Parameters:**
- `needId` - ID of the need to satisfy
- `sourceIdentifier` - IFoodSourceIdentifier implementation
- `acquisitionStrategy` - IFoodAcquisitionStrategy implementation
- `consumptionEffect` - IConsumptionEffect implementation
- `criticalStateHandler` - ICriticalStateHandler implementation
- `consumptionDuration` - Ticks to consume (default 30)

### LivingTrait.cs
Base trait for living entities.

**Features:**
- Initializes hunger need in NeedsSystem
- Hunger: 75 initial, 0.02 decay, thresholds 15/40/90

Simple trait that just adds the hunger need - actual consumption behavior is handled by ConsumptionBehaviorTrait.

### MindlessTrait.cs
Trait for non-sapient entities.

**Features:**
- Limits dialogue to non-complex commands
- Provides generic dialogue responses
- "Blank stare" initial dialogue
- Silent obedience responses

**Command Filtering:**
```csharp
public override bool IsOptionAvailable(DialogueOption option)
{
    if (option.Command == null) return true;
    return !option.Command.IsComplex;
}
```

### UndeadTrait.cs
Base trait for all undead entities.

**Features:**
- Disables pain body system
- Disables living body systems (breathing, blood, digestion, senses)
- Provides idle action as default behavior

**Disabled Systems:**
- Pain, Breathing, BloodPumping, BloodFiltration, Digestion, Sight, Hearing

### UndeadBehaviorTrait.cs
Abstract base for undead with autonomous behavior.

**Features:**
- Common wandering behavior properties
- State timer management
- Helper for wandering within range
- Range checking utilities

**Properties:**
- `WanderProbability` - Chance to start wandering (default 0.2)
- `WanderRange` - Max distance from spawn (default 10.0)
- `IdleTime` - Ticks between decisions (default 10)

**Abstract Method:**
- `ProcessState(position, perception)` - Implemented by subclasses

### SkeletonTrait.cs
Territorial behavior for skeleton entities.

**Features:**
- Territory defense state machine
- Intruder detection and pursuit
- Bone rattle audio integration
- Last-known-position tracking

**States:**
- `Idle` - Standing still, chance to wander
- `Wandering` - Moving within territory
- `Defending` - Pursuing intruder

**Territory Parameters:**
- `TerritoryRange`: 12 tiles
- `DetectionRange`: 8 tiles
- `IntimidationTime`: 40 ticks

### ZombieTrait.cs
Hunger-driven behavior for zombie entities.

**Features:**
- Brain hunger need initialization
- ConsumptionBehaviorTrait composition
- Groan audio integration
- Wider wander range than skeletons

**States:**
- `Idle` - Standing still, chance to groan and wander
- `Wandering` - Shambling movement

**Behavior Parameters:**
- `WanderProbability`: 0.3 (more active)
- `WanderRange`: 15 tiles (further range)

**Hunger Configuration:**
- Need: "Brain Hunger", 60 initial, 0.0015 decay
- Source: GraveyardSourceIdentifier
- Consumption duration: 365 ticks (messy eaters)

### VillagerTrait.cs
Autonomous village life behavior.

**Features:**
- Building discovery and memory
- State-based daily routine
- LivingTrait + ConsumptionBehaviorTrait composition
- Farm-based food acquisition

**States:**
- `IdleAtHome` - At home position, may wander
- `IdleAtSquare` - At village center, social time
- `VisitingBuilding` - At a specific building

**Discovery:**
Scans Entities node for Building children on initialization.

## Trait Hierarchy

```
Trait (base)
  +-- BeingTrait (Being-specific helpers)
        +-- LivingTrait (hunger need)
        +-- MindlessTrait (dialogue limits)
        +-- ConsumptionBehaviorTrait (need satisfaction)
        +-- VillagerTrait (village life)
        +-- UndeadTrait (undead properties)
              +-- UndeadBehaviorTrait (abstract, wandering)
                    +-- SkeletonTrait (territorial)
                    +-- ZombieTrait (hunger-driven)
```

## Key Classes

| Trait | Description |
|-------|-------------|
| `ConsumptionBehaviorTrait` | Strategy-based need satisfaction |
| `LivingTrait` | Living entity needs |
| `MindlessTrait` | Non-sapient dialogue limits |
| `UndeadTrait` | Base undead properties |
| `UndeadBehaviorTrait` | Abstract wandering behavior |
| `SkeletonTrait` | Territorial skeleton behavior |
| `ZombieTrait` | Hunger-driven zombie behavior |
| `VillagerTrait` | Village daily routine |

## Important Notes

### Trait Priority
Lower priority values execute first. Typical ordering:
- 0: LivingTrait, base traits
- 1: Main behavior trait (VillagerTrait, MindlessTrait)
- 2: Specific behavior (SkeletonTrait, ZombieTrait)
- Priority - 1: Consumption trait (needs to override when hungry)

### Trait Composition Pattern
Complex behaviors compose simpler traits:
```csharp
// VillagerTrait adds:
_owner?.selfAsEntity().AddTraitToQueue<LivingTrait>(0, initQueue);
_owner?.selfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

### State Timer Pattern
Many traits use `_stateTimer` for decision pacing:
```csharp
if (_stateTimer > 0)
    _stateTimer--;

if (_stateTimer == 0)
{
    // Make decision, reset timer
    _stateTimer = IdleTime;
}
```

### Action Priority in Traits
Traits return actions with appropriate priorities:
- Idle actions: 0-1 (lowest priority)
- Movement actions: 1 (normal)
- Defending actions: -2 (high priority)
- Emergency actions: negative values

### Undead Detection
Checking if an entity is undead:
```csharp
if (entity.selfAsEntity().HasTrait<UndeadTrait>())
```

### Audio Integration Pattern
Traits trigger audio via deferred calls:
```csharp
(_owner as MindlessSkeleton)?.CallDeferred("PlayBoneRattle");
(_owner as MindlessZombie)?.CallDeferred("PlayZombieGroan");
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.BeingTrait` - Base class
- `VeilOfAges.Entities.Actions` - Action types
- `VeilOfAges.Entities.Beings.Health` - Body systems
- `VeilOfAges.Entities.Needs` - Need system
- `VeilOfAges.Entities.Sensory` - Perception
- `VeilOfAges.Core.Lib` - PathFinder
- `VeilOfAges.UI` - Dialogue system

### Depended On By
- All Being subclasses in `/entities/beings/`
- Entity spawning systems
