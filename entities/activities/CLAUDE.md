# /entities/activities

## Purpose

This directory contains the Activity system - the execution layer between trait decisions and atomic actions. Activities represent "what an entity is currently doing" and handle multi-step behaviors.

## Architecture Overview

### Three Layers of Entity Behavior

| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE (what IS this entity) | Villager, Undead, Living |
| **Activities** | EXECUTE (what is it DOING) | Eating, Sleeping, Working |
| **Actions** | ATOMIC (what THIS TICK) | Move one tile, Stand still |

### Key Principles

1. **Traits DECIDE** what to do based on needs, personality, perception
2. **Activities EXECUTE** the multi-step behavior to accomplish a goal
3. **Activities are separate from Traits** - stored in `Being._currentActivity`, not in `_traits` collection (for threading safety)
4. **Commands START Activities** - player commands create activities, then poll their state

## Files

### Activity.cs
Abstract base class for all activities.

**Key Properties:**
- `State` - ActivityState enum (Running, Completed, Failed)
- `DisplayName` - Human-readable name for UI ("Eating at Farm")
- `Priority` - Default priority for actions returned

**Key Methods:**
- `Initialize(Being owner)` - Called when activity starts
- `GetNextAction(position, perception)` - Returns next action, may set State
- `Cleanup()` - Called when activity ends (for future resource release)
- `Complete()` / `Fail()` - Protected methods to set final state
- `DebugLog(category, message, tickInterval)` - Protected debug logging helper

**Debug Logging:**

The `DebugLog` protected method provides throttled debug logging for activities. It only logs when the owning entity has `DebugEnabled = true`.

```csharp
protected void DebugLog(string category, string message, int tickInterval = 100)
```

**Parameters:**
- `category` - Log category (e.g., "ACTIVITY", "WORK", "SLEEP")
- `message` - The message to log
- `tickInterval` - Minimum ticks between logs for this category (default: 100)
  - Use `0` for immediate logging (every call logs)
  - Use `100` (default) for periodic status updates

**Usage Examples:**

```csharp
// Log phase transitions immediately (tickInterval=0)
DebugLog("WORK", $"Transitioning to {_currentPhase} phase", 0);

// Log periodic status updates (default tickInterval=100)
DebugLog("WORK", $"Working... {_workTicks}/{_workDuration} ticks");

// Log with custom interval
DebugLog("SLEEP", $"Energy: {_energyNeed.CurrentValue:F1}", 50);
```

**Best Practices:**
- Use `tickInterval=0` for one-time events (phase transitions, errors, completions)
- Use default `tickInterval=100` for recurring status messages to avoid log spam
- Category names should be consistent within an activity (e.g., "WORK_FIELD" for WorkFieldActivity)

**Sub-Activity Helper (RunSubActivity):**

When activities compose other activities (e.g., ConsumeItemActivity uses GoToBuildingActivity), the sub-activity may complete immediately on the same tick if the goal is already reached. Without proper handling, this can cause the parent activity to return `null`, allowing other traits to overwrite it.

The `RunSubActivity` helper method handles this pattern safely:

```csharp
protected (SubActivityResult result, EntityAction? action) RunSubActivity(
    Activity subActivity,
    Vector2I position,
    Perception perception)
```

**Returns a tuple with:**
- `SubActivityResult.Continue` - Sub-activity is running, return the action
- `SubActivityResult.Completed` - Sub-activity finished, proceed to next phase
- `SubActivityResult.Failed` - Sub-activity failed, handle the error

**Usage Pattern:**
```csharp
// Initialize sub-activity if needed
if (_goToPhase == null)
{
    _goToPhase = new GoToBuildingActivity(_building, Priority);
    _goToPhase.Initialize(_owner);
}

// Run it using the helper
var (result, action) = RunSubActivity(_goToPhase, position, perception);
switch (result)
{
    case SubActivityResult.Failed:
        Fail();
        return null;
    case SubActivityResult.Continue:
        return action;  // Still navigating
    case SubActivityResult.Completed:
        break;  // Fall through to next phase
}

// We've arrived - continue with next phase logic
```

**Why this matters:**
- If a sub-activity's `GetNextAction()` returns `null` (e.g., goal already reached), the helper checks if the state changed to Completed
- If still Running but returned null (edge case), it returns an IdleAction to "hold the slot" and prevent other traits from overwriting the activity

### GoToLocationActivity.cs
Moves an entity to a specific grid position.

**Usage:**
```csharp
new GoToLocationActivity(targetPosition, priority: 0)
```

**Behavior:**
- Creates PathFinder, sets position goal
- Returns MoveAlongPathAction each tick
- Completes when IsGoalReached()
- Fails if stuck for MAX_STUCK_TICKS

### GoToBuildingActivity.cs
Moves an entity to a building (adjacent position).

**Usage:**
```csharp
new GoToBuildingActivity(farmBuilding, priority: 0)
```

**Behavior:**
- Creates PathFinder with building goal
- Validates building still exists each tick
- Completes when adjacent to building
- Fails if building destroyed or stuck for MAX_STUCK_TICKS (50 ticks)

### SleepActivity.cs
Sleeping at home during night. Restores energy, reduces hunger decay.

**Usage:**
```csharp
new SleepActivity(priority: 0)
```

**Behavior:**
- Completes automatically when Dawn arrives
- Restores energy at 0.15/tick (full restore in ~84 seconds)
- Sets energy decay to 0 (no energy loss while sleeping)
- Sets hunger decay to 0.25x (slow hunger while sleeping)

**Need Effects:**
```csharp
NeedDecayMultipliers["hunger"] = 0.25f;  // Slow hunger
NeedDecayMultipliers["energy"] = 0f;      // No energy decay
// Plus direct restoration: _energyNeed.Restore(0.15f) each tick
```

### WorkFieldActivity.cs
Multi-phase work activity at a farm/field. Produces wheat, brings harvest home.

**Usage:**
```csharp
new WorkFieldActivity(workplace: farm, home: house, workDuration: 400, priority: 0)
```

**Phases:**
1. **GoingToWork** - Navigate to workplace using GoToBuildingActivity
2. **Working** - Idle at location, spend energy (0.05/tick), produce wheat
3. **TakingWheat** - Gather 4-6 wheat from farm storage into inventory
4. **GoingHome** - Navigate home using GoToBuildingActivity
5. **DepositingWheat** - Transfer wheat from inventory to home storage

**Behavior:**
- Automatically transitions to TakingWheat when day phase becomes Dusk/Night
- Produces WHEAT_PRODUCED_PER_SHIFT (3) wheat when work duration completes
- Takes MIN_WHEAT_TO_BRING_HOME to MAX_WHEAT_TO_BRING_HOME (4-6) wheat home
- Completes after depositing wheat or if no home exists

**Need Effects:**
```csharp
NeedDecayMultipliers["hunger"] = 1.2f;  // Hungry faster while working
// Direct cost: _energyNeed.Restore(-0.05f) each tick while in Working phase
```

**Design Decision:** Work uses direct energy cost rather than decay multiplier.
This creates a clearer "work spends energy" mental model and avoids confusing
compounding effects. Decay multipliers are reserved for passive effects.

### ConsumeItemActivity.cs
Consumes food items from inventory or home storage to restore a need.

**Usage:**
```csharp
new ConsumeItemActivity(
    foodTag: "food",           // Tag to identify food items
    need: hungerNeed,          // Need to restore
    home: houseBuilding,       // Home with storage (can be null)
    restoreAmount: 50f,        // Amount to restore
    consumptionDuration: 24,   // Ticks to consume
    priority: 0
)
```

**Phases:**
1. **Check Inventory** - If entity has food in inventory, start consuming immediately
2. **Go to Home** - If no food in inventory, travel to home using GoToBuildingActivity
3. **Check Home Storage** - Look for food in home storage
4. **Consume** - Remove 1 unit of food, idle for consumption duration, then restore need

**Behavior:**
- Checks inventory first (no travel needed if food found)
- Validates home building still exists during travel
- Removes food item on first consumption tick (item is "claimed")
- Applies need restoration only after consumption duration completes
- Fails if no food found in inventory and no home, or home has no food

### ProcessReactionActivity.cs
Processes a crafting reaction (input items -> output items) at a workplace.

**Usage:**
```csharp
new ProcessReactionActivity(
    reaction: breadReaction,    // ReactionDefinition to process
    workplace: bakery,          // Building with facilities (can be null)
    storage: storageTrait,      // Storage for inputs/outputs
    priority: 0
)
```

**Phases:**
1. **Go to Workplace** - Navigate to workplace if specified (skipped if null)
2. **Verify Inputs** - Check all required inputs are available in storage
3. **Consume Inputs** - Remove input items from storage
4. **Process** - Wait for reaction duration (idle)
5. **Produce Outputs** - Create output items and add to storage

**Behavior:**
- Validates workplace exists during travel
- Verifies all inputs available before consuming any
- Consumes all inputs atomically on first processing tick
- Creates output items using ItemResourceManager definitions
- Warns if storage is full and outputs are lost
- Works with any ReactionDefinition that specifies inputs, outputs, and duration

## Key Classes

| Class | Description |
|-------|-------------|
| `Activity` | Abstract base with state, lifecycle |
| `GoToLocationActivity` | Navigate to grid position |
| `GoToBuildingActivity` | Navigate to building |
| `SleepActivity` | Sleep at night, restore energy |
| `WorkFieldActivity` | Work at building, produce/transport wheat |
| `ConsumeItemActivity` | Eat food from inventory/home storage |
| `ProcessReactionActivity` | Craft items via reaction system |

## Integration with Being

Activities are managed in `Being.cs`:

```csharp
// Field
protected Activity? _currentActivity;

// In Think() - after commands, before traits
if (_currentActivity != null)
{
    var action = _currentActivity.GetNextAction(position, perception);

    if (_currentActivity.State != Activity.ActivityState.Running)
    {
        _currentActivity.Cleanup();
        _currentActivity = null;
    }

    if (action != null)
        possibleActions.Enqueue(action, action.Priority);
}

// Public methods
public Activity? GetCurrentActivity() => _currentActivity;
public void SetCurrentActivity(Activity? activity) { ... }
```

## Starting Activities from Traits

Traits don't call SetCurrentActivity directly (threading). They return a StartActivityAction:

```csharp
// In a trait's SuggestAction():
var farm = FindNearestFarm(perception);
if (farm != null)
{
    return new StartActivityAction(_owner, this, new EatActivity(farm), priority: 0);
}
```

StartActivityAction executes on main thread and calls SetCurrentActivity.

## Internal Composition

Activities can use other activities as components:

```csharp
public class EatActivity : Activity
{
    private GoToBuildingActivity? _goToPhase;

    public override EntityAction? GetNextAction(...)
    {
        // Phase 1: Get to the farm
        if (!IsAtFarm())
        {
            _goToPhase ??= new GoToBuildingActivity(_farm);
            _goToPhase.Initialize(_owner);

            var action = _goToPhase.GetNextAction(position, perception);

            // Check if navigation failed
            if (_goToPhase.State == ActivityState.Failed)
            {
                Fail();
                return null;
            }

            return action;
        }

        // Phase 2: Consume
        // ...
    }
}
```

## Future: GoToEntityActivity (Not Yet Implemented)

Going to a moving entity requires BDI (Belief-Desire-Intention) architecture:

### Current Simple Approach (in FollowCommand)
- Hold reference to target Being
- PathFinder tracks it directly (somewhat omniscient)

### Proper BDI Approach (TODO)

**1. Find Phase - Where do I *believe* the entity is?**
- Currently perceiving it? -> Use current position from perception
- Have memory of it? -> Use last known position from `BeingPerceptionSystem._memory`
- Shared knowledge? -> Check collective memory (not yet implemented)
- No information? -> Fail or trigger SearchActivity

**2. Go Phase - Move to believed position**
- Use GoToLocationActivity with believed position
- NOT directly tracking the entity

**3. Verify Phase - Arrived at believed position**
- Is entity actually here? -> Complete
- Entity not here? -> Update belief, decide next action:
  - If within perception range, update position and continue
  - If entity left perception, use memory or fail
  - Let trait decide: continue searching? give up?

**4. Search Behavior (optional)**
- FollowCommand already has expanding radius search
- Could be extracted into SearchActivity
- Picks random points in expanding radius around last known position

### Implementation Notes

When implementing GoToEntityActivity:
1. Take `Being target` and `Perception` in constructor
2. On first tick, find target in perception or memory
3. If found, create internal GoToLocationActivity to believed position
4. On arrival, check if target is there
5. If not, update belief and retry or fail
6. Include timeout/max attempts to prevent infinite searching

The key insight: **GoToEntityActivity goes to where we BELIEVE the target is, not where it actually is.** This creates emergent "realistic" behavior where entities can lose track of targets.

## Future: Consumption Activity Variants

The current `ConsumeItemActivity` handles eating from inventory/storage. Future variants needed:

### FeedOnBeingActivity (Vampires, Predators)
- Target: Living Being
- Uses GoToEntityActivity (with BDI search behavior)
- Consume phase: Active feeding, target may resist/flee
- Effects: Restore blood/hunger, possibly harm target
- Complications: Combat integration, target death handling

### Design Note
Each consumption type has different enough mechanics that separate activity classes make sense rather than one generic ConsumeActivity.

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner entity
- `VeilOfAges.Entities.EntityAction` - Actions returned
- `VeilOfAges.Entities.Sensory.Perception` - Perception data
- `VeilOfAges.Core.Lib.PathFinder` - Navigation
- `VeilOfAges.Entities.Items` - Item, ItemResourceManager
- `VeilOfAges.Entities.Reactions` - ReactionDefinition
- `VeilOfAges.Entities.Traits` - InventoryTrait, StorageTrait

### Depended On By
- `VeilOfAges.Entities.Being` - Manages _currentActivity
- `VeilOfAges.Entities.Actions.StartActivityAction` - Starts activities
- Trait implementations that start activities
- Command implementations that start activities
