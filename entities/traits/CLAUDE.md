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

### ItemConsumptionBehaviorTrait.cs
Trait that handles need satisfaction by consuming items from inventory or home storage.

**Features:**
- Checks inventory first, then home storage for food
- Priority-based action generation (critical hunger interrupts sleep)
- Starts ConsumeItemActivity when food is available
- Tag-based food identification

**Constructor Parameters:**
- `needId` - The need to satisfy (e.g., "hunger")
- `foodTag` - Tag to identify food items (e.g., "food", "zombie_food")
- `getHome` - Function to get home building (may return null)
- `restoreAmount` - Amount to restore when eating (default 60)
- `consumptionDuration` - Ticks to spend eating (default 244)

**Usage:**
```csharp
var consumptionTrait = new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",
    () => villagerTrait?.Home,
    restoreAmount: 60f,
    consumptionDuration: 244
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

**Priority:** -1 relative to parent (runs before main behavior when hungry)

### InventoryTrait.cs
Personal item storage for beings (living and undead entities).

**Features:**
- Volume and weight-based capacity limits
- Stack merging for identical items
- Item decay processing
- Encumbrance tracking
- Implements IStorageContainer interface

**Default Capacities:**
- Volume: 0.02 m3 (20 liters, roughly a small backpack)
- Weight: 15 kg (reasonable carry weight)
- Decay modifier: 1.0 (normal decay rate)

**Key Methods:**
- `AddItem(Item)` - Add item, auto-merges with existing stacks
- `RemoveItem(itemDefId, quantity)` - Remove and return items
- `HasItem(itemDefId, quantity)` - Check availability
- `FindItem(itemDefId)` / `FindItemByTag(tag)` - Locate items
- `GetEncumbranceLevel()` - Returns 0-1 based on most restrictive limit
- `ProcessDecay()` - Apply decay to all items, remove spoiled

**Usage:**
```csharp
// Add trait to a being
being.SelfAsEntity().AddTraitToQueue<InventoryTrait>(0);

// Use inventory
var inventory = being.SelfAsEntity().GetTrait<InventoryTrait>();
if (inventory?.CanAdd(item) == true)
{
    inventory.AddItem(item);
}
```

### StorageTrait.cs
Storage trait for buildings and non-being entities.

**Features:**
- Configurable volume and weight capacity
- Decay rate modifier (for cold storage, etc.)
- Facility tracking for crafting requirements
- Stack merging for identical items
- Implements IStorageContainer interface

**Constructor Parameters:**
- `volumeCapacity` - Maximum volume in cubic meters
- `weightCapacity` - Maximum weight in kg, -1 for unlimited
- `decayRateModifier` - Decay modifier (0.5 = half decay, 2.0 = double)
- `facilities` - List of available facilities (e.g., "oven", "mill")

**Key Methods:**
- `AddItem(Item)` - Add item with capacity checking
- `RemoveItem(itemDefId, quantity)` - Remove and return items
- `HasItem(itemDefId, quantity)` - Check availability
- `FindItem(itemDefId)` / `FindItemByTag(tag)` - Locate items
- `HasFacility(facility)` - Check for crafting facility
- `ProcessDecay()` - Apply decay modifier to all items

**Usage:**
```csharp
// In Building class
var storage = new StorageTrait(
    volumeCapacity: 10.0f,
    weightCapacity: -1,
    decayRateModifier: 0.5f,  // Cold storage
    facilities: ["oven", "counter"]
);
building.SelfAsEntity().AddTraitToQueue(storage, 0);
```

### LivingTrait.cs
Base trait for living entities.

**Features:**
- Initializes hunger and energy needs in NeedsSystem
- Hunger: 75 initial, 0.02 decay, thresholds 15/40/90
- Energy: 100 initial, 0.008 decay, thresholds 20/40/80

Simple trait that adds needs - actual consumption behavior is handled by ConsumptionBehaviorTrait or ItemConsumptionBehaviorTrait, energy is restored by SleepActivity.

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
- Uses GoToLocationActivity for returning to spawn and pursuing intruders

**States:**
- `Idle` - Standing still, chance to wander
- `Wandering` - Moving within territory
- `Defending` - Pursuing intruder using GoToLocationActivity

**Navigation Pattern:**
- Uses `GoToLocationActivity` for position-based navigation (return to spawn, pursue intruder)
- Starts activity with `StartActivityAction`, then checks if activity is running and returns `null` to let activity handle navigation
- Pursuit uses BDI pattern: goes to believed position, not tracking entity directly

**Territory Parameters:**
- `TerritoryRange`: 12 tiles
- `DetectionRange`: 8 tiles
- `IntimidationTime`: 40 ticks

### ZombieTrait.cs
Hunger-driven behavior for zombie entities.

**Features:**
- Brain hunger need initialization
- ItemConsumptionBehaviorTrait composition for feeding
- Groan audio integration
- Wider wander range than skeletons
- Uses GoToLocationActivity for returning to spawn when wandering too far

**States:**
- `Idle` - Standing still, chance to groan and wander
- `Wandering` - Shambling movement, returns to spawn if too far

**Navigation Pattern:**
- Uses `GoToLocationActivity` for returning to spawn position
- Checks if activity is running via `_owner.GetCurrentActivity()` and returns `null` to let activity handle navigation

**Behavior Parameters:**
- `WanderProbability`: 0.3 (more active)
- `WanderRange`: 15 tiles (further range)

**Hunger Configuration:**
- Need: "Brain Hunger", 60 initial, 0.0015 decay
- Food tag: "zombie_food" (corpses)
- Consumption duration: 365 ticks (messy eaters)

### VillagerTrait.cs
Autonomous village life behavior.

**Features:**
- Building discovery and memory
- State-based daily routine with sleep schedule
- LivingTrait + ItemConsumptionBehaviorTrait composition
- Home-based food acquisition
- Uses GoToBuildingActivity for visiting buildings (home, other buildings)
- Uses GoToLocationActivity for going to village square

**States:**
- `IdleAtHome` - At home position, may wander or start sleeping; uses GoToBuildingActivity to navigate home
- `IdleAtSquare` - At village center, social time; uses GoToLocationActivity to navigate to square
- `VisitingBuilding` - At a specific building; uses GoToBuildingActivity to navigate
- `Sleeping` - Sleeping at home during Night/Dusk (uses SleepActivity)

**Navigation Pattern:**
- Uses `GoToBuildingActivity` for building-based navigation (home, visiting buildings)
- Uses `GoToLocationActivity` for position-based navigation (village square)
- Pattern: start activity with `StartActivityAction`, then check if activity is running and return `null` to let activity handle navigation
- Example: `return new StartActivityAction(_owner, this, visitActivity, priority: 1);`
- Then check: `if (_owner.GetCurrentActivity() is GoToBuildingActivity) return null;`

**Discovery:**
Scans Entities node for Building children on initialization.

### FarmerJobTrait.cs
Job trait for farmers who work at assigned farms during daytime.

**Features:**
- Assigned to a specific farm building on construction
- Starts WorkFieldActivity during Dawn/Day phases
- Returns null at night (VillagerTrait handles sleep)
- Context-aware dialogue based on time of day
- Deposits harvest to farmer's home storage

**Constants:**
- `WORKDURATION`: 400 ticks (~50 seconds real time at 8 ticks/sec)

**Usage:**
```csharp
var farmerTrait = new FarmerJobTrait(assignedFarm);
typedBeing.SelfAsEntity().AddTraitToQueue(farmerTrait, priority: -1);
```

**Priority:** -1 (runs before VillagerTrait at priority 1)

### BakerJobTrait.cs
Job trait for bakers who work at bakeries during daytime.

**Features:**
- Assigned to a specific workplace (bakery) building
- Finds and processes reactions with "baking" or "milling" tags
- Checks for required facilities and input items
- Starts ProcessReactionActivity when work is available
- Returns null at night (VillagerTrait handles sleep)
- Context-aware dialogue based on time of day

**Reaction Tags (priority order):**
1. "baking"
2. "milling"

**Usage:**
```csharp
var bakerTrait = new BakerJobTrait(assignedBakery);
typedBeing.SelfAsEntity().AddTraitToQueue(bakerTrait, priority: -1);
```

**Priority:** -1 (runs before VillagerTrait at priority 1)

**Work Flow:**
1. Check if work hours (Dawn/Day)
2. Get workplace storage and facilities
3. Find reaction with matching tags that can be performed
4. Check if required inputs are available in storage
5. Start ProcessReactionActivity if all requirements met

## Trait Hierarchy

```
Trait (base)
  +-- BeingTrait (Being-specific helpers)
        +-- LivingTrait (hunger + energy needs)
        +-- MindlessTrait (dialogue limits)
        +-- ConsumptionBehaviorTrait (strategy-based need satisfaction)
        +-- ItemConsumptionBehaviorTrait (item-based need satisfaction)
        +-- InventoryTrait (personal item storage, implements IStorageContainer)
        +-- VillagerTrait (village life + sleep)
        +-- FarmerJobTrait (daytime work at farm)
        +-- BakerJobTrait (daytime work at bakery)
        +-- UndeadTrait (undead properties)
              +-- UndeadBehaviorTrait (abstract, wandering)
                    +-- SkeletonTrait (territorial)
                    +-- ZombieTrait (hunger-driven)
  +-- StorageTrait (building storage, implements IStorageContainer)
```

## Key Classes

| Trait | Description |
|-------|-------------|
| `ConsumptionBehaviorTrait` | Strategy-based need satisfaction |
| `ItemConsumptionBehaviorTrait` | Item-based need satisfaction from inventory/home |
| `InventoryTrait` | Personal item storage for beings |
| `StorageTrait` | Building/entity item storage |
| `LivingTrait` | Living entity needs (hunger, energy) |
| `MindlessTrait` | Non-sapient dialogue limits |
| `UndeadTrait` | Base undead properties |
| `UndeadBehaviorTrait` | Abstract wandering behavior |
| `SkeletonTrait` | Territorial skeleton behavior |
| `ZombieTrait` | Hunger-driven zombie behavior |
| `VillagerTrait` | Village daily routine + sleep |
| `FarmerJobTrait` | Work at assigned farm during day |
| `BakerJobTrait` | Work at assigned bakery during day |

## Important Notes

### Trait Priority
Lower priority values execute first. Typical ordering:
- -1: Job traits (FarmerJobTrait, BakerJobTrait) - run before main behavior
- 0: LivingTrait, base traits, storage traits
- 1: Main behavior trait (VillagerTrait, MindlessTrait)
- 2: Specific behavior (SkeletonTrait, ZombieTrait)
- Priority - 1: Consumption trait (needs to override when hungry)

### Storage System (IStorageContainer)
Both `InventoryTrait` and `StorageTrait` implement `IStorageContainer`:
```csharp
public interface IStorageContainer
{
    bool CanAdd(Item item);
    bool AddItem(Item item);
    Item? RemoveItem(string itemDefId, int quantity);
    bool HasItem(string itemDefId, int quantity = 1);
    int GetItemCount(string itemDefId);
    Item? FindItem(string itemDefId);
    Item? FindItemByTag(string tag);
    IEnumerable<Item> GetAllItems();
    void ProcessDecay();
}
```

### Trait Composition Pattern
Complex behaviors compose simpler traits:
```csharp
// VillagerTrait adds:
_owner?.SelfAsEntity().AddTraitToQueue<LivingTrait>(0, initQueue);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
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
- Critical needs: -1 (interrupts sleep)

### Job Trait Pattern
Job traits (FarmerJobTrait, BakerJobTrait) follow a common pattern:
1. Check if owner is valid and not already moving/working
2. Check if work hours (Dawn/Day)
3. Check for required resources/inputs
4. Start appropriate work activity
5. Return null at night (let VillagerTrait handle sleep)

### Undead Detection
Checking if an entity is undead:
```csharp
if (entity.SelfAsEntity().HasTrait<UndeadTrait>())
```

### Activity-Based Navigation Pattern
Traits should use activities (GoToBuildingActivity, GoToLocationActivity) for navigation instead of manual pathfinding. This centralizes navigation logic and handles edge cases.

**Pattern for starting navigation:**
```csharp
// Start an activity to navigate somewhere
var activity = new GoToLocationActivity(targetPosition, priority: 1);
return new StartActivityAction(_owner, this, activity, priority: 1);
```

**Pattern for checking if navigation is in progress:**
```csharp
// Check if activity is still running - return null to let it handle things
if (_owner.GetCurrentActivity() is GoToLocationActivity)
{
    return null;  // Activity handles navigation
}
```

**Common navigation activities:**
- `GoToBuildingActivity` - Navigate to a building's interior (finds door, enters)
- `GoToLocationActivity` - Navigate to a specific grid position

**Activity state checking (for traits that manage activities directly):**
```csharp
if (_myActivity != null)
{
    if (_myActivity.State == Activity.ActivityState.Running)
    {
        return _myActivity.GetNextAction(position, perception);
    }
    if (_myActivity.State == Activity.ActivityState.Completed)
    {
        _myActivity = null;  // Clear completed activity
        // Handle arrival
    }
    if (_myActivity.State == Activity.ActivityState.Failed)
    {
        _myActivity = null;  // Clear failed activity
        // Handle failure (retry or give up)
    }
}
```

### Audio Integration Pattern
Traits trigger audio via deferred calls:
```csharp
(_owner as MindlessSkeleton)?.CallDeferred("PlayBoneRattle");
(_owner as MindlessZombie)?.CallDeferred("PlayZombieGroan");
```

## Creating a New Trait

### Step-by-Step Guide

1. **Create the file** in `/entities/traits/` (e.g., `MyNewTrait.cs`)

2. **Choose the base class**:
   - `BeingTrait` - For most entity behaviors
   - `Trait` - For non-being entities (like StorageTrait)
   - `UndeadBehaviorTrait` - For undead with wandering behavior (abstract, override `ProcessState`)

3. **Basic trait template**:
```csharp
namespace VeilOfAges.Entities.Traits;

public class MyNewTrait : BeingTrait
{
    private enum State { Idle, Active }
    private State _currentState = State.Idle;
    private int _stateTimer = 0;

    public override void Initialize(Being owner, object? data)
    {
        base.Initialize(owner, data);
        // Add any sub-traits here:
        // _owner?.SelfAsEntity().AddTraitToQueue<OtherTrait>(Priority - 1, initQueue);
    }

    public override EntityAction? SuggestAction(Vector2I currentPosition, Perception perception)
    {
        // Decrement timer
        if (_stateTimer > 0) _stateTimer--;

        // State machine logic
        switch (_currentState)
        {
            case State.Idle:
                if (_stateTimer == 0)
                {
                    // Make decision, transition states
                    _stateTimer = 10; // Reset timer
                }
                return new IdleAction(_owner!, this, priority: 1);

            case State.Active:
                // Return appropriate action
                return new IdleAction(_owner!, this, priority: 0);
        }

        return null;
    }
}
```

4. **Add the trait to a Being** in the Being's `_Ready()` method:
```csharp
SelfAsEntity().AddTraitToQueue<MyNewTrait>(1); // priority 1
```

### Key Considerations

- **Priority**: Lower values execute first (0 = highest priority)
- **Action priorities**: Return actions with appropriate priority (-2 for urgent, 0-1 for normal)
- **State timer**: Use `_stateTimer` pattern to pace decisions
- **Thread safety**: Use `CallDeferred()` for any Godot scene operations
- **Composition**: Complex traits should compose simpler traits via `AddTraitToQueue()`

### Common Patterns

**Adding item-based consumption behavior**:
```csharp
// In Initialize():
var consumptionTrait = new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",
    () => _home,
    restoreAmount: 60f,
    consumptionDuration: 244
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

**Adding a need with strategy-based consumption behavior**:
```csharp
// In Initialize():
_owner?.NeedsSystem.AddNeed(new Need("thirst", "Thirst", 80f, 0.01f, 15f, 30f, 90f));

var consumptionTrait = new ConsumptionBehaviorTrait(
    "thirst",
    new WellSourceIdentifier(),      // You implement these
    new WellAcquisitionStrategy(),
    new DrinkingEffect(),
    new ThirstCriticalHandler(),
    120  // Duration in ticks
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

**Detecting other entities**:
```csharp
foreach (var entity in perception.GetEntitiesOfType<Being>())
{
    if (entity.SelfAsEntity().HasTrait<UndeadTrait>()) continue; // Skip undead
    // React to living entity
}
```

**Creating a job trait**:
```csharp
public class MyJobTrait : BeingTrait
{
    private readonly Building _workplace;

    public MyJobTrait(Building workplace)
    {
        _workplace = workplace;
    }

    public override EntityAction? SuggestAction(Vector2I pos, Perception perception)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_workplace))
            return null;

        if (_owner.IsMoving() || _owner.GetCurrentActivity() is MyWorkActivity)
            return null;

        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
            return null;

        var workActivity = new MyWorkActivity(_workplace, priority: 0);
        return new StartActivityAction(_owner, this, workActivity, priority: 0);
    }
}
```

**Navigating to a building using activity**:
```csharp
// Start navigation to a building
var goToBuildingActivity = new GoToBuildingActivity(targetBuilding, priority: 1);
return new StartActivityAction(_owner, this, goToBuildingActivity, priority: 1);

// In subsequent calls, check if activity is still running
if (_owner.GetCurrentActivity() is GoToBuildingActivity)
{
    return null;  // Let activity handle navigation
}
// Activity completed - we've arrived at the building
```

**Navigating to a position using activity**:
```csharp
// Start navigation to a specific position
var goToLocationActivity = new GoToLocationActivity(targetPosition, priority: 1);
return new StartActivityAction(_owner, this, goToLocationActivity, priority: 1);

// In subsequent calls, check if activity is still running
if (_owner.GetCurrentActivity() is GoToLocationActivity)
{
    return null;  // Let activity handle navigation
}
// Activity completed - we've arrived at the position
```

**Managing activity state directly (for more control)**:
```csharp
private GoToLocationActivity? _navigationActivity;

public override EntityAction? SuggestAction(Vector2I pos, Perception perception)
{
    // Check if we're in the middle of navigation
    if (_navigationActivity != null)
    {
        if (_navigationActivity.State == Activity.ActivityState.Running)
        {
            return _navigationActivity.GetNextAction(pos, new Perception());
        }
        if (_navigationActivity.State == Activity.ActivityState.Completed)
        {
            _navigationActivity = null;
            // Handle arrival...
        }
        if (_navigationActivity.State == Activity.ActivityState.Failed)
        {
            _navigationActivity = null;
            // Handle failure, maybe retry...
        }
    }

    // Start new navigation if needed
    if (needToNavigate)
    {
        _navigationActivity = new GoToLocationActivity(target, priority: 1);
        _navigationActivity.Initialize(_owner);
        return _navigationActivity.GetNextAction(pos, new Perception());
    }

    return new IdleAction(_owner, this);
}
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.BeingTrait` - Base class
- `VeilOfAges.Entities.Actions` - Action types
- `VeilOfAges.Entities.Activities` - Activity types (WorkFieldActivity, ConsumeItemActivity, etc.)
- `VeilOfAges.Entities.Beings.Health` - Body systems
- `VeilOfAges.Entities.Items` - Item and ItemDefinition classes
- `VeilOfAges.Entities.Needs` - Need system
- `VeilOfAges.Entities.Reactions` - Reaction system (for BakerJobTrait)
- `VeilOfAges.Entities.Sensory` - Perception
- `VeilOfAges.Core.Lib` - PathFinder, GameTime
- `VeilOfAges.UI` - Dialogue system

### Depended On By
- All Being subclasses in `/entities/beings/`
- Entity spawning systems
- Building system (for StorageTrait)
