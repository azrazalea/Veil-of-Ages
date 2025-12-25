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
- Fails if building destroyed or stuck

## Key Classes

| Class | Description |
|-------|-------------|
| `Activity` | Abstract base with state, lifecycle |
| `GoToLocationActivity` | Navigate to grid position |
| `GoToBuildingActivity` | Navigate to building |

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
- Currently perceiving it? → Use current position from perception
- Have memory of it? → Use last known position from `BeingPerceptionSystem._memory`
- Shared knowledge? → Check collective memory (not yet implemented)
- No information? → Fail or trigger SearchActivity

**2. Go Phase - Move to believed position**
- Use GoToLocationActivity with believed position
- NOT directly tracking the entity

**3. Verify Phase - Arrived at believed position**
- Is entity actually here? → Complete
- Entity not here? → Update belief, decide next action:
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

The current `EatActivity` handles eating at buildings (farms). Future variants needed:

### FeedOnBeingActivity (Vampires, Predators)
- Target: Living Being
- Uses GoToEntityActivity (with BDI search behavior)
- Consume phase: Active feeding, target may resist/flee
- Effects: Restore blood/hunger, possibly harm target
- Complications: Combat integration, target death handling

### ConsumeItemActivity (Eating Carried/Found Items)
- Target: Item (on ground or in inventory)
- Go-to phase: Only if item is on ground
- Consume phase: Item is destroyed/consumed
- Effects: Based on item type (food restores hunger, potion gives buff)

### Design Note
Each consumption type has different enough mechanics that separate activity classes make sense rather than one generic ConsumeActivity.

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Owner entity
- `VeilOfAges.Entities.EntityAction` - Actions returned
- `VeilOfAges.Entities.Sensory.Perception` - Perception data
- `VeilOfAges.Core.Lib.PathFinder` - Navigation

### Depended On By
- `VeilOfAges.Entities.Being` - Manages _currentActivity
- `VeilOfAges.Entities.Actions.StartActivityAction` - Starts activities
- Trait implementations that start activities
- Command implementations that start activities
