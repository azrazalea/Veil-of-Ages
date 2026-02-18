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

### Work Activities: Phase-Based State Machines (CRITICAL)

Work activities (farming, baking, crafting) MUST use phase-based state machines to ensure storage access only happens when the entity has physically arrived at the location.

**Required Pattern:**
1. **Navigation phases** use `RunSubActivity()` with `GoToBuildingActivity`
2. **Storage access phases** only run AFTER navigation completes
3. Activities handle ALL work logic - traits just start them

**Why This Matters:**
Storage access (`AccessStorage`, `TakeFromStorage`, etc.) requires physical proximity (within 1 tile). If a trait tries to access storage before navigation completes, the access returns null/fails. This was the root cause of the baker bug where baking failed because the trait checked storage while the entity was still walking.

**Canonical Examples:**
- `WorkFieldActivity.cs` - Phases: GoingToWork -> Working -> TakingWheat -> GoingHome -> DepositingWheat
- `ProcessReactionActivity.cs` - Phases: GoToWorkplace -> GoToFacility -> VerifyInputs -> ConsumeInputs -> Process -> ProduceOutputs

**Phase Transition Pattern:**
```csharp
private enum Phase { GoingToWork, Working, TakingHarvest, GoingHome, Depositing }
private Phase _currentPhase = Phase.GoingToWork;

public override EntityAction? GetNextAction(...)
{
    switch (_currentPhase)
    {
        case Phase.GoingToWork:
            // Use RunSubActivity for navigation
            var (result, action) = RunSubActivity(_goToWorkActivity, position, perception);
            if (result == SubActivityResult.Failed) { Fail(); return null; }
            if (result == SubActivityResult.Continue) return action;
            // Completed - NOW we can access workplace storage
            _currentPhase = Phase.Working;
            break;

        case Phase.Working:
            // Safe to access storage here - we've arrived
            var storage = _owner.AccessStorage(_workplace);
            // ... do work ...
            break;
    }
}
```

See `/entities/traits/CLAUDE.md` "Job Trait Pattern (CRITICAL)" section for the trait-side requirements.

## Files

### ISubActivityRunner.cs
Shared interface for running sub-activities. Implemented by both `Activity` (for activity composition) and `EntityCommand` (for command-driven activities).

- **Namespace**: `VeilOfAges.Entities.Activities`
- **Interface**: `ISubActivityRunner`
- **Key Properties**:
  - `SubActivityOwner`: The Being that owns this runner
- **Default Method**: `RunSubActivity(subActivity, position, perception, priority)` — single source of truth for the sub-activity driving pattern. Handles immediate completion, null actions, state transitions, and returns an IdleAction to hold the action slot when needed.
- **Implementors**:
  - `Activity` — delegates existing `RunSubActivity(subActivity, position, perception)` to the interface, passing its own `Priority`
  - `EntityCommand` — adds `RunSubActivity(subActivity, position, perception, priority = -1)` wrapper and `InitializeSubActivity(subActivity)` helper

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
Moves an entity to a building (adjacent position or storage access position). Cross-area capable.

**Cross-Area Capable**: PathFinder handles cross-area routing internally. When the target building is in a different area, PathFinder plans the route via known transition points and signals area transitions automatically.

**Usage:**
```csharp
// Basic navigation to building
new GoToBuildingActivity(farmBuilding, priority: 0)

// Navigate to storage access position (handles RequireAdjacentToFacility automatically)
new GoToBuildingActivity(homeBuilding, priority: 0, targetStorage: true)
```

**Parameters:**
- `targetBuilding` - The building to navigate to
- `priority` - Action priority (default 0)
- `targetStorage` - If true, navigate to storage access position (default false)

**Behavior:**
- Creates PathFinder with building goal or facility goal (when targetStorage=true)
- When `targetStorage` is true and building has `RequireAdjacentToFacility` storage, uses `SetFacilityGoal("storage")` to navigate to a position adjacent to the storage facility
- When `targetStorage` is true but `RequireAdjacentToFacility` is false (e.g., wells), uses `SetBuildingGoal` with `requireInterior: false` to navigate to building perimeter
- Validates building still exists each tick
- Completes when at goal position
- Fails if building destroyed or stuck for MAX_STUCK_TICKS (50 ticks)

**Storage Navigation:**
When `targetStorage=true`, the activity automatically handles buildings with storage that requires facility adjacency. This removes the need for traits to know about storage configuration - they simply set `targetStorage: true` when navigating to access storage.

### SleepActivity.cs
Sleeping activity that restores energy. Targets 100% energy with smart wake conditions.

**Usage:**
```csharp
new SleepActivity(priority: 0)
```

**Behavior:**
- Can start during Dusk or Night phases
- Can continue sleeping through Dawn if energy hasn't reached 100%
- Restores energy at 0.025/tick (full restore in ~4000 ticks)
- Sets energy decay to 0 (no energy loss while sleeping)
- Sets hunger decay to 0.25x (slow hunger while sleeping)

**Wake Conditions:**
1. Energy reaches 100% (fully rested)
2. Any non-energy need reaches critical level (e.g., starvation)
3. Day phase starts (must wake regardless of energy)

**Need Effects:**
```csharp
NeedDecayMultipliers["hunger"] = 0.25f;  // Slow hunger
NeedDecayMultipliers["energy"] = 0f;      // No energy decay
// Plus direct restoration: _energyNeed.Restore(0.025f) each tick
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
Uses `TakeFromStorageActivity` to fetch food (cross-area capable) and
`ConsumeFromInventoryAction` for consumption. Hidden entities use
`ConsumeFromStorageByTagAction` directly (they're already inside the building).

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
1. **Check Inventory** - If entity has food in inventory, skip to consuming
2. **Hidden Entity** - If hidden, use ConsumeFromStorageByTagAction directly (no navigation)
3. **Fetch from Storage** - TakeFromStorageActivity(home, foodTag, 1) handles cross-area navigation + local navigation + taking into inventory
4. **Consume** - ConsumeFromInventoryAction + timer + need restoration

**Behavior:**
- Checks inventory first (no travel needed if food found)
- Hidden entities consume directly from storage (no navigation needed)
- Non-hidden entities use TakeFromStorageActivity for cross-area capable food fetching
- Always consumes from inventory via ConsumeFromInventoryAction after fetching
- Validates home building still exists during travel
- Applies need restoration only after consumption duration completes
- Fails if no food found in inventory and no home, or home has no food

### CheckStorageActivity.cs
Goes to a building and observes its storage to refresh memory. Cross-area capable.

**Usage:**
```csharp
new CheckStorageActivity(targetBuilding, priority: 0)
```

**Phases:**
1. **Navigate** - GoToBuildingActivity(targetStorage: true) to reach storage access position (cross-area capable)
2. **Observing** - Call AccessStorage to observe and update personal memory, then complete

**Behavior:**
- Uses `GoToBuildingActivity` with `targetStorage: true` for navigation (cross-area capable via PathFinder)
- Used by ItemConsumptionBehaviorTrait, BakerJobTrait, and DistributorRoundActivity
- Validates building still exists during travel
- Calls `_owner.AccessStorage()` which automatically updates PersonalMemory
- Completes immediately after observing (single tick observation)
- OnResume() nulls navigation, regresses to Navigate phase

**Integration with ItemConsumptionBehaviorTrait:**
When an entity is hungry but has no memory of food locations:
1. Trait checks `HasFoodAvailable()` -> returns false (no inventory, no memory)
2. Trait starts `CheckStorageActivity` to go to building and observe storage
3. Activity navigates (cross-area if needed), calls `AccessStorage()`, completes
4. On next think cycle, `HasFoodAvailable()` may now return true (if building had food)
5. Trait then starts `ConsumeItemActivity` as normal

### TakeFromStorageActivity.cs
Takes specified items from a building's storage into the entity's inventory. Cross-area capable.
Supports both item-by-ID and tag-based modes.

**Usage:**
```csharp
// Take specific items by ID
new TakeFromStorageActivity(building, new List<(string, int)> { ("wheat", 5) }, priority: 0)

// Take items by tag (resolves to item ID after arriving)
new TakeFromStorageActivity(building, tag: "food", quantity: 1, priority: 0)
```

**Phases:**
1. **Navigate** - GoToBuildingActivity(targetStorage: true) to reach storage access position (cross-area capable)
2. **Taking** - TakeFromStorageAction per item (tag mode resolves tag to item ID first)

**Behavior:**
- Uses `GoToBuildingActivity` with `targetStorage: true` for navigation (cross-area capable via PathFinder)
- Tag mode: after arriving, calls AccessStorage to observe, FindItemByTag to resolve tag → item ID
- ID mode: takes items directly using TakeFromStorageAction
- OnResume() nulls navigation, regresses to Navigate phase. Taking progress (items already taken) is preserved.
- Backward compatible: existing callers that already navigate will see navigation phases complete immediately

**Used By:**
- ConsumeItemActivity (tag mode: fetch food from home)
- FetchResourceActivity, WorkFieldActivity, ProcessReactionActivity (ID mode)

### ProcessReactionActivity.cs
Processes a crafting reaction (input items -> output items) at a workplace.
Uses `ConsumeFromStorageAction` and `ProduceToStorageAction` for storage operations.

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
2. **Go to Facility** - Navigate to specific facility if reaction requires one
3. **Verify Inputs** - Check all required inputs are available in storage
4. **Consume Inputs** - Remove input items from storage using `ConsumeFromStorageAction` (one action per input type)
5. **Process** - Wait for reaction duration (idle), spend energy based on EnergyCostMultiplier
6. **Produce Outputs** - Create output items and add to storage using `ProduceToStorageAction` (one action per output type)

**Behavior:**
- Validates workplace exists during travel
- Verifies all inputs available before consuming any
- Consumes inputs one at a time using `ConsumeFromStorageAction`
- Creates output items one at a time using `ProduceToStorageAction`
- Updates entity's memory about storage contents via actions
- Warns if storage is full and outputs are lost
- Works with any ReactionDefinition that specifies inputs, outputs, and duration
- Falls back to direct storage manipulation if workplace is null (legacy behavior)

### StudyNecromancyActivity.cs
Studying necromancy and dark arts at a necromancy_altar facility.

**Usage:**
```csharp
new StudyNecromancyActivity(facilityRef, studyDuration: 400, priority: 0)
```

**Phases:**
1. **GoingToStudy** - Navigate to necromancy_altar (cross-area capable via GoToFacilityActivity)
2. **Studying** - Study dark arts, spend energy, gain necromancy + arcane_theory XP

**Behavior:**
- Dynamically finds necromancy_altar via FacilityReference (cross-area capable)
- Uses GoToFacilityActivity for navigation (cross-area capable via PathFinder)
- Drains energy at 0.015/tick (more taxing than normal study)
- Grants XP in both "necromancy" and "arcane_theory" skills
- Completes after study duration or if energy is low
- Validates facility building exists throughout activity

**Energy Cost:**
```csharp
const float ENERGYCOSTPERTICK = 0.015f;  // Necromantic study is demanding
```

### WorkOnOrderActivity.cs
Generic activity for working on a facility's active work order.

**Usage:**
```csharp
new WorkOnOrderActivity(facilityRef, facility, priority: 0)
```

**Phases:**
1. **Navigating** - Travel to facility (cross-area capable via GoToFacilityActivity)
2. **Working** - Work each tick, advance progress, grant XP, drain energy

**Behavior:**
- Uses GoToFacilityActivity for navigation (cross-area capable via PathFinder)
- Calls `workOrder.Advance(worker)` each tick in Working phase (increments progress, grants XP, drains energy)
- Exits early if energy becomes critical or time phase changes (necromancy is night-only)
- Completes when work order finishes or conditions no longer met (work order stays on facility)
- Calls `facility.CompleteWorkOrder()` when work order reaches 100%

**Integration:**
Used by NecromancyStudyJobTrait when altar has an active work order.

### FetchResourceActivity.cs
Fetches resources from one building (source) and brings them to another (destination).

**Usage:**
```csharp
new FetchResourceActivity(
    sourceBuilding: well,        // Building to take items from
    destinationBuilding: bakery, // Building to deposit items to
    itemId: "water",             // Item ID to fetch
    desiredQuantity: 5,          // How many items to fetch
    priority: 0
)
```

**Phases:**
1. **GoingToSource** - Navigate to source building (cross-area capable via GoToBuildingActivity)
2. **TakingResource** - Take items from source storage into inventory
3. **GoingToDestination** - Navigate to destination building (cross-area capable via GoToBuildingActivity)
4. **DepositingResource** - Transfer items from inventory to destination storage

**Behavior:**
- Validates both buildings exist throughout the activity
- Uses GoToBuildingActivity for navigation (cross-area capable via PathFinder)
- Takes up to desiredQuantity, or all available if less
- Uses Being wrapper methods for storage access (auto-observes contents)
- Completes even if destination storage is full (keeps items in inventory)
- Fails if source has no items or inventory is full when taking

**Used By:**
- BakerJobTrait - Fetches water from Well to Bakery for baking bread
- FetchCorpseCommand - Fetches corpse from Graveyard to necromancy altar (cross-area, driven as sub-activity from command)

## Key Classes

| Class | Description |
|-------|-------------|
| `ISubActivityRunner` | Interface for shared sub-activity driving |
| `Activity` | Abstract base with state, lifecycle |
| `GoToLocationActivity` | Navigate to grid position |
| `GoToBuildingActivity` | Navigate to building (cross-area capable via PathFinder) |
| `CheckStorageActivity` | Navigate to building (cross-area) and observe storage to refresh memory |
| `TakeFromStorageActivity` | Navigate to building (cross-area) and take items by ID or tag |
| `SleepActivity` | Sleep at night, restore energy |
| `WorkFieldActivity` | Work at building, produce/transport wheat |
| `WorkOnOrderActivity` | Work on facility's active work order (cross-area capable) |
| `ConsumeItemActivity` | Eat food from inventory/home storage (cross-area capable) |
| `ProcessReactionActivity` | Craft items via reaction system |
| `FetchResourceActivity` | Fetch items from one building to another (cross-area capable) |

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
