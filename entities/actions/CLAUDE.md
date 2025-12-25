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
- Used for wandering and simple movements

### MoveAlongPathAction.cs
Pathfinding-based movement action using lazy path calculation.
- Takes a `PathFinder` instance with a pre-set goal
- Delegates to `PathFinder.TryFollowPath()`
- PathFinder calculates path on-demand (lazy evaluation)
- Used for complex multi-tile navigation
- Supports position goals, entity proximity goals, and area goals

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `IdleAction` | No-op action, entity stands still |
| `InteractAction` | Interaction with world objects (WIP) |
| `MoveAction` | Single-tile movement to adjacent position |
| `MoveAlongPathAction` | Multi-tile pathfinding movement |

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

### Constructor Patterns
All action constructors follow a consistent pattern:
```csharp
public ActionName(Being entity, object source, [specific params], int priority = 1)
```
The `source` parameter tracks which trait/system created the action for debugging.

## Dependencies

### Depends On
- `VeilOfAges.Entities.EntityAction` - Base class
- `VeilOfAges.Entities.Being` - Entity being controlled
- `VeilOfAges.Core.Lib.PathFinder` - For path-based movement

### Depended On By
- `VeilOfAges.Entities.BeingTrait` - Movement helper methods create actions
- All trait implementations in `/entities/traits/`
- Command implementations in `/core/ui/commands/`
