# /entities/actions

## Purpose

This directory contains concrete action implementations that entities can execute. Actions represent discrete behaviors that an entity performs during a game tick, such as moving, idling, or interacting with objects. All actions extend `EntityAction` and implement the `Execute()` method.

## Files

### IdleAction.cs
The simplest action - entity does nothing for this tick.
- Sets entity direction to `Vector2.Zero`
- Always returns `true` (success)
- Used as default when no other action is appropriate
- Common fallback for many trait state machines

### InteractAction.cs
Action for interacting with objects at a target position.
- Stores a `_targetPosition` for the interaction target
- Currently returns `false` (not fully implemented)
- Contains TODO for implementing interaction logic via World
- Will handle resource gathering, door opening, etc.

### MoveAction.cs
Simple single-tile movement action.
- Moves entity to an adjacent grid position
- Delegates to `Entity.TryMoveToGridPosition()`
- Returns success/failure based on whether move was possible
- If blocked by another entity, stores blocker for Think() to handle next tick
- Used for wandering and simple movements

### RequestMoveAction.cs
Communication action for sapient beings to request another entity to move.
- Costs a turn (proper action, not free)
- Queues a `MoveRequest` event on the target entity
- Target may step aside, ask us to queue, or report they're stuck
- Used when a sapient being is blocked by another entity

**Constructor:**
```csharp
new RequestMoveAction(entity, source, targetEntity, targetPosition, priority: 0)
```

### PushAction.cs
Physical push action for mindless beings.
- Costs a turn (essentially an attack action)
- Queues an `EntityPushed` event on the target entity
- Target will stumble in the push direction if possible
- Used when a mindless being is blocked by another entity

**Constructor:**
```csharp
new PushAction(entity, source, targetEntity, pushDirection, priority: 0)
```

### MoveAlongPathAction.cs
Pathfinding-based movement action that follows a pre-calculated path.
- Takes a `PathFinder` instance with path already calculated
- Delegates to `PathFinder.FollowPath()` - does NOT calculate path
- **Path must be calculated in Think thread** via `CalculatePathIfNeeded()` before returning this action
- Used for complex multi-tile navigation
- Supports position goals, entity proximity goals, and area goals

**Critical Threading Note:**
Path calculation happens in Think() (background thread), not Execute() (main thread).
The caller must call `_pathFinder.CalculatePathIfNeeded(entity)` before creating this action.

```csharp
// Correct usage in Activity.GetNextAction() or Trait.SuggestAction():
_pathFinder.SetPositionGoal(_owner, targetPos);
if (!_pathFinder.CalculatePathIfNeeded(_owner))
{
    return new IdleAction(_owner, this, priority);  // Path failed
}
return new MoveAlongPathAction(_owner, this, _pathFinder, priority);
```

### TakeFromStorageAction.cs
Action to take items from a facility's storage into the entity's inventory.
- Requires entity to be adjacent to the facility (checked via `TakeFromFacilityStorage`)
- Takes items using `Entity.TakeFromFacilityStorage()` which handles adjacency and memory
- Adds taken items to entity's `InventoryTrait`
- Returns item to storage if inventory is full or missing
- Exposes `TakenItem` and `ActualQuantity` for post-execution inspection

**Constructor:**
```csharp
new TakeFromStorageAction(entity, source, facility, itemDefId, quantity, priority: 0)
```

### DepositToStorageAction.cs
Action to deposit items from entity's inventory to a facility's storage.
- Requires entity to be adjacent to the facility (checked via `CanAccessFacility`)
- Uses safe `TransferTo()` method - items never disappear
- Updates entity's memory about storage contents after transfer
- Exposes `ActualDeposited` for post-execution inspection

**Constructor:**
```csharp
new DepositToStorageAction(entity, source, facility, itemDefId, quantity, priority: 0)
```

### TransferBetweenStoragesAction.cs
Action to transfer items between two storage containers.
- Source or destination must be the entity's inventory
- For facility storages, entity must be adjacent to the facility
- Uses safe `TransferTo()` method - items never disappear
- Updates entity's memory for all facilities involved
- Exposes `ActualTransferred` for post-execution inspection

**Static Factory Methods:**
```csharp
// From facility storage to entity inventory
TransferBetweenStoragesAction.FromFacility(entity, source, facility, itemDefId, quantity, priority: 0)

// From entity inventory to facility storage
TransferBetweenStoragesAction.ToFacility(entity, source, facility, itemDefId, quantity, priority: 0)
```

### ConsumeFromStorageAction.cs
Action to consume (remove) items from a facility's storage for in-place processing.
Unlike TakeFromStorageAction, items are consumed directly at the workstation rather than being transferred to inventory. Used for crafting reactions.
- Requires entity to be adjacent to the facility (checked via `CanAccessFacility`)
- Verifies items are available before consuming
- Removes items from storage (items are consumed, not transferred to inventory)
- Updates entity's memory about storage contents
- Exposes `ConsumedItem` and `ActualQuantity` for post-execution inspection

**Constructor:**
```csharp
new ConsumeFromStorageAction(entity, source, facility, itemDefId, quantity, priority: 0)
```

**Used By:**
- `ProcessReactionActivity` - Consumes reaction input items from workplace storage

### ProduceToStorageAction.cs
Action to produce (create and add) items to a facility's storage from in-place processing.
Unlike DepositToStorageAction, items are created from an item definition rather than being transferred from inventory. Used for crafting reactions.
- Requires entity to be adjacent to the facility (checked via `CanAccessFacility`)
- Creates new items from item definition
- Adds items to storage (logs warning if storage is full)
- Updates entity's memory about storage contents
- Exposes `ActualProduced` for post-execution inspection

**Constructor:**
```csharp
new ProduceToStorageAction(entity, source, facility, itemDefId, quantity, priority: 0)
```

**Used By:**
- `ProcessReactionActivity` - Produces reaction output items to workplace storage

### ConsumeFromInventoryAction.cs
Action to consume (remove) items from the entity's own inventory for direct consumption.
Unlike ConsumeFromStorageAction, this works with the entity's inventory rather than building storage, and does not require adjacency to any building. Used for eating food from inventory.
- Works with entity's `InventoryTrait` directly
- No adjacency requirement (consuming from own inventory)
- Verifies items are available before consuming
- Removes items from inventory (items are consumed/eaten)
- Exposes `ConsumedItem` and `ActualQuantity` for post-execution inspection

**Constructor:**
```csharp
new ConsumeFromInventoryAction(entity, source, itemDefId, quantity, priority: 0)
```

**Used By:**
- `ConsumeItemActivity` - Consumes food from inventory for eating

### ConsumeFromStorageByTagAction.cs
Action to consume (remove) items from a facility's storage by tag for direct consumption.
Searches for items matching the specified tag and consumes them. Unlike TakeFromStorageAction, the items are consumed directly rather than being transferred to inventory. Used for eating food from home storage.
- Requires entity to be adjacent to the facility
- Searches for items by tag (e.g., "food", "zombie_food")
- Uses `Entity.TakeFromFacilityStorageByTag()` which handles adjacency check and memory observation
- Items are consumed (eaten), not transferred to inventory
- Exposes `ConsumedItem` and `ActualQuantity` for post-execution inspection

**Constructor:**
```csharp
new ConsumeFromStorageByTagAction(entity, source, facility, itemTag, quantity, priority: 0)
```

**Used By:**
- `ConsumeItemActivity` - Consumes food from home storage for eating

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `IdleAction` | No-op action, entity stands still |
| `InteractAction` | Interaction with world objects (WIP) |
| `MoveAction` | Single-tile movement to adjacent position |
| `MoveAlongPathAction` | Multi-tile pathfinding movement |
| `RequestMoveAction` | Sapient communication to request another entity move |
| `PushAction` | Mindless physical push of another entity |
| `TakeFromStorageAction` | Take items from facility to inventory |
| `DepositToStorageAction` | Deposit items from inventory to facility |
| `TransferBetweenStoragesAction` | Transfer items between storages |
| `ConsumeFromStorageAction` | Consume items from facility storage (for crafting) |
| `ProduceToStorageAction` | Produce items to facility storage (for crafting) |
| `ConsumeFromInventoryAction` | Consume items from own inventory (for eating) |
| `ConsumeFromStorageByTagAction` | Consume items from facility storage by tag (for eating) |

## Important Notes

### Action Execution Pattern
- All actions inherit from `EntityAction` and override `Execute()`
- `Execute()` returns `bool` - true if action succeeded, false otherwise
- Actions are executed on the main thread after entity thinking completes
- Movement is processed via `ProcessMovementTick()` after action execution

### Lazy Pathfinding
- `MoveAlongPathAction` uses lazy path calculation
- The `PathFinder` stores the goal but calculates the path only when needed
- This avoids wasted calculations for actions that never execute
- Path is recalculated if goal moves (e.g., following another entity)

### Priority Usage
- Default priority is 1 for most actions
- Priority 0 is used for important actions (like consumption behavior)
- Negative priorities indicate urgent actions (defending, emergencies)
- The priority is passed through the constructor

### Entity Blocking and Communication Pattern
When an entity's movement is blocked by another entity:
1. `MoveAction` fails and stores the blocking entity in `MovementController`
2. Next tick, `Being.Think()` consumes the blocking info via `ConsumeBlockingEntity()`
3. Think() iterates through traits calling `GetBlockingResponse(blockingEntity, targetPosition)`
4. First non-null response is used; otherwise defaults to `RequestMoveAction`
5. The action executes on the main thread and queues an event on the target
6. Target processes the event in their next Think() cycle

This ensures all entity interactions cost a turn and go through the action system.

**Trait-defined behavior:**
- Traits override `GetBlockingResponse()` in `BeingTrait` to customize blocking response
- `MindlessTrait` returns `PushAction` (physical push)
- Default behavior (no trait override) returns `RequestMoveAction` (polite communication)

**Important**: Only self-notification events can be queued directly (no action cost).
All entity-to-entity communication must go through actions.

### Constructor Patterns
All action constructors follow a consistent pattern:
```csharp
public ActionName(Being entity, object source, [specific params], int priority = 1)
```
The `source` parameter tracks which trait/system created the action for debugging.

## Creating a New Action

### Step-by-Step Guide

1. **Create the file** in `/entities/actions/` (e.g., `MyNewAction.cs`)

2. **Basic action template**:
```csharp
namespace VeilOfAges.Entities.Actions;

public class MyNewAction : EntityAction
{
    private readonly Vector2I _targetPosition;

    public MyNewAction(Being entity, object source, Vector2I targetPosition, int priority = 1)
        : base(entity, priority)
    {
        Source = source;
        _targetPosition = targetPosition;
    }

    public override bool Execute()
    {
        // Perform the action
        // Return true if successful, false if failed

        // Example: Try to interact with something at target
        // var world = Entity.GetTree().GetFirstNodeInGroup("World") as World;
        // return world?.InteractAt(_targetPosition) ?? false;

        return true;
    }
}
```

3. **Use from a trait**:
```csharp
public override EntityAction? SuggestAction(Vector2I currentPosition, Perception perception)
{
    return new MyNewAction(_owner!, this, targetPos, priority: 0);
}
```

### Key Considerations

- **Constructor signature**: Always follow `(Being entity, object source, [params], int priority = 1)`
- **Source tracking**: Set `Source = source` for debugging (shows which trait created the action)
- **Return value**: `true` = success, `false` = failure
- **Thread safety**: Actions execute on main thread, so Godot operations are safe here
- **Priority**: Lower = higher priority (negative for urgent actions)

### Common Action Types

**Movement action** (use existing `MoveAlongPathAction`):
```csharp
var pathfinder = new PathFinder();
pathfinder.SetPositionGoal(_owner, targetPos);
return new MoveAlongPathAction(_owner!, this, pathfinder, priority: 0);
```

**Idle action** (do nothing this tick):
```csharp
return new IdleAction(_owner!, this, priority: 1);
```

**Single-tile move** (to adjacent cell):
```csharp
return new MoveAction(_owner!, this, adjacentGridPos, priority: 1);
```

**Take from facility storage** (entity must be adjacent):
```csharp
return new TakeFromStorageAction(_owner!, this, facility, "wheat", 5, priority: 0);
```

**Deposit to facility storage** (entity must be adjacent):
```csharp
return new DepositToStorageAction(_owner!, this, facility, "bread", 3, priority: 0);
```

**Transfer using factory methods**:
```csharp
// From facility to inventory
return TransferBetweenStoragesAction.FromFacility(_owner!, this, facility, "flour", 10, priority: 0);

// From inventory to facility
return TransferBetweenStoragesAction.ToFacility(_owner!, this, facility, "wheat", 5, priority: 0);
```

**Consume from storage for crafting** (entity must be adjacent):
```csharp
return new ConsumeFromStorageAction(_owner!, this, facility, "flour", 5, priority: 0);
```

**Produce to storage for crafting** (entity must be adjacent):
```csharp
return new ProduceToStorageAction(_owner!, this, facility, "bread", 3, priority: 0);
```

**Consume from inventory for eating** (no adjacency required):
```csharp
return new ConsumeFromInventoryAction(_owner!, this, "bread", 1, priority: 0);
```

**Consume from facility storage by tag for eating** (entity must be adjacent):
```csharp
return new ConsumeFromStorageByTagAction(_owner!, this, homeFacility, "food", 1, priority: 0);
```

### Storage Action Safety
All storage transfer actions use the `IStorageContainer.TransferTo()` method which:
- Checks destination capacity before removing from source
- Only transfers what will fit (partial transfers are allowed)
- Never loses items - if destination can't accept, items stay in source
- Returns actual quantity transferred for verification

## Dependencies

### Depends On
- `VeilOfAges.Entities.EntityAction` - Base class
- `VeilOfAges.Entities.Being` - Entity being controlled
- `VeilOfAges.Core.Lib.PathFinder` - For path-based movement

### Depended On By
- `VeilOfAges.Entities.BeingTrait` - Movement helper methods create actions
- All trait implementations in `/entities/traits/`
- Command implementations in `/core/ui/commands/`
