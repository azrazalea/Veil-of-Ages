# Entity Commands Module

## Purpose

The `/core/ui/dialogue/commands` directory contains concrete implementations of `EntityCommand` that can be assigned to entities through the dialogue system or programmatically. Each command defines behavior through the `SuggestAction()` method.

## Files

### Fully Implemented Commands

#### MoveToCommand.cs
Moves the entity to a specific position or toward another entity.

- **Namespace**: `VeilOfAges.UI.Commands`
- **Class**: `MoveToCommand : EntityCommand`
- **IsComplex**: `false` (mindless entities can perform)
- **Parameters**:
  - `targetPos` (Vector2I): Destination grid position
  - `targetEntity` (Being): Entity to approach
- **Behavior**: Uses PathFinder to navigate. Idles if no target set. Completes when goal reached.

#### FollowCommand.cs
Follows a target entity, searching if they leave line of sight.

- **Namespace**: `VeilOfAges.UI.Commands`
- **Class**: `FollowCommand : EntityCommand`
- **IsComplex**: `false`
- **Constants**:
  - `SEARCH_TIMEOUT`: 100 ticks before giving up
  - `SEARCH_RADIUS_MAX`: 8 tiles maximum search
- **Behavior**:
  1. If commander visible: Follow within 1 tile proximity
  2. If commander lost: Move to last known position
  3. If at last known position: Expand search radius, pick random points to explore
  4. If timeout exceeded: Command ends

#### TalkCommand.cs
Dummy command used to check if an entity will accept dialogue.

- **Namespace**: `VeilOfAges.UI.Commands`
- **Class**: `TalkCommand : EntityCommand`
- **IsComplex**: `false`
- **Behavior**: Always returns `null`. Exists solely so entities can refuse dialogue via `WillRefuseCommand()`.

#### FetchCorpseCommand.cs
Fetches a corpse from the nearest graveyard and brings it to the necromancy altar.

- **Namespace**: `VeilOfAges.UI.Commands`
- **Class**: `FetchCorpseCommand : EntityCommand`
- **IsComplex**: `false` (mindless entities can perform this)
- **Parameters**:
  - `altarBuilding` (Building): The altar building to deposit the corpse at
- **Behavior**:
  - Resolves altar building from Parameters on first call
  - Finds graveyard via SharedKnowledge building lookup (FindNearestBuildingOfType)
  - Creates FetchResourceActivity as a sub-activity and drives it via RunSubActivity
  - FetchResourceActivity handles the full go→take→return→deposit pattern including cross-area navigation
  - Command completes when sub-activity completes or fails

### Stub Commands (Not Yet Implemented)

These commands exist with the interface but return `null` from `SuggestAction()`:

| Command | Description | IsComplex |
|---------|-------------|-----------|
| `GuardCommand` | Guard a specific position | false |
| `ReturnHomeCommand` | Return to home building | false |
| `PatrolCommand` | Patrol between waypoints | true (default) |
| `DefendPositionCommand` | Defend a location aggressively | true (default) |
| `GatherCommand` | Gather resources | true (default) |
| `BuildCommand` | Construct buildings | true (default) |
| `RestCommand` | Rest to recover | true (default) |
| `AttackTargetCommand` | Attack a specific entity | false |
| `CancelCommand` | Cancel current command | false |

## Key Classes/Interfaces

| Class | Status | Description |
|-------|--------|-------------|
| `MoveToCommand` | Implemented | Position/entity navigation |
| `FollowCommand` | Implemented | Follow with search behavior |
| `TalkCommand` | Implemented | Dialogue permission check |
| `FetchCorpseCommand` | Implemented | Fetch corpse from graveyard to altar |
| `GuardCommand` | Stub | Area defense |
| `ReturnHomeCommand` | Stub | Navigate to home |
| `PatrolCommand` | Stub | Patrol routes |
| `DefendPositionCommand` | Stub | Aggressive defense |
| `GatherCommand` | Stub | Resource collection |
| `BuildCommand` | Stub | Construction |
| `RestCommand` | Stub | Recovery |
| `AttackTargetCommand` | Stub | Combat |
| `CancelCommand` | Stub | Command cancellation |

## Important Notes

### Action Priority System
Commands should use these priority levels in `SuggestAction()`:
- `-10`: Emergency/crucial actions that override everything
- `-1`: Normal command execution priority
- `0`: Idle waiting for parameters
- `1`: Default trait actions (commands should generally be lower)

### PathFinder Usage Pattern
```csharp
// Set goal once
if (!GoalSet)
{
    MyPathfinder.SetPositionGoal(_owner, targetPos);
    GoalSet = true;
}

// Check completion
if (MyPathfinder.IsGoalReached(_owner)) return null;

// Return movement action
return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
```

### FollowCommand Search Algorithm
1. Track `_lastKnownPosition` and `_lastSeenTick`
2. When commander not visible, move to last known position
3. At last known position, enter search mode
4. Search mode: pick random points in expanding radius
5. Give up after `SEARCH_TIMEOUT` ticks

### Parameter Injection
Commands receive parameters via `WithParameter()` after construction:
```csharp
var cmd = new MoveToCommand(entity, player);
cmd.WithParameter("targetPos", gridPosition);
```

### Location Selection Commands
`MoveToCommand` and `GuardCommand` trigger the UI's location selection mode, which pauses the game and waits for player to click a destination.

## Creating a New Command

### Step-by-Step Guide

1. **Create the file** in `/core/ui/dialogue/commands/` (e.g., `HarvestCommand.cs`)

2. **Basic command template**:
```csharp
namespace VeilOfAges.UI.Commands;

public class HarvestCommand : EntityCommand
{
    private bool _goalSet = false;

    public HarvestCommand(Being owner, Being commander)
        : base(owner, commander, IsComplex: true)  // true = sapient only
    {
    }

    public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
    {
        // Get parameters (injected via WithParameter after construction)
        if (!Parameters.TryGetValue("targetPos", out var targetObj))
            return new IdleAction(_owner, this, priority: 0);  // Wait for params

        var targetPos = (Vector2I)targetObj;

        // Set pathfinder goal once
        if (!_goalSet)
        {
            MyPathfinder.SetPositionGoal(_owner, targetPos);
            _goalSet = true;
        }

        // Check if we've arrived
        if (MyPathfinder.IsGoalReached(_owner))
        {
            // Do the harvest action
            GD.Print($"{_owner.Name} harvests at {targetPos}");
            return null;  // Command complete
        }

        // Still moving
        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
    }
}
```

3. **Register in DialogueController** (if it should appear in dialogue):
```csharp
// In DialogueController.GenerateOptionsFor():
options.Add(new DialogueOption(
    "Harvest resources.",
    new HarvestCommand(target, speaker),
    "I'll gather what I can.",
    "I cannot do that."
));
```

4. **Handle location selection** (if command needs a target position):
```csharp
// In Dialogue.cs ProcessSelectedOption():
if (command is HarvestCommand)
{
    _playerInputController.StartLocationSelection(command, "Select harvest location");
    Hide();
    return;
}
```

### Key Considerations

- **IsComplex flag**:
  - `true` (default) = Only sapient entities can perform
  - `false` = Mindless entities can also perform (simple commands)

- **Action priorities in commands**:
  - `-10`: Emergency override
  - `-1`: Normal command execution
  - `0`: Idle/waiting for parameters
  - `1`: Default (commands should be lower than this)

- **PathFinder per command**: Each `EntityCommand` has its own `MyPathfinder` instance

- **Parameter injection pattern**:
```csharp
var cmd = new HarvestCommand(entity, player);
cmd.WithParameter("targetPos", gridPosition);
cmd.WithParameter("resourceType", "wood");
```

- **Command completion**: Return `null` from `SuggestAction()` when done

### Command Types Reference

| IsComplex | Who can perform | Examples |
|-----------|-----------------|----------|
| `false` | All entities | MoveTo, Follow, Talk, Guard, Attack |
| `true` | Sapient only | Patrol, Gather, Build, Rest |

## Dependencies

### This module depends on:
- `VeilOfAges.UI.EntityCommand` - Base class
- `VeilOfAges.Core.Lib.PathFinder` - Navigation
- `VeilOfAges.Entities` - Being, EntityAction
- `VeilOfAges.Entities.Actions` - MoveAlongPathAction, IdleAction
- `VeilOfAges.Entities.Sensory` - Perception for awareness

### Depended on by:
- `VeilOfAges.UI.DialogueController` - Creates command instances
- `VeilOfAges.Core.PlayerInputController` - Uses MoveToCommand for player movement
- Entity AI decision making
