# /entities/player

## Purpose

This directory contains the player entity implementation. The player is the necromancer character controlled by the user, with special features like a command queue for managing multiple orders.

## Files

### Player.cs
The player-controlled necromancer entity.

**Configuration:**
- Uses base attribute set (all 10s)
- Movement: 0.5 points/tick (fastest entity, 2 ticks per tile)
- Named "Lilith Galonadel" by default
- Maximum command queue: 7 commands

**Trait Composition:**
- `LivingTrait` (priority 0) - Living entity needs

**Command Queue System:**
- `ReorderableQueue<EntityCommand>` for pending commands
- Commands pulled from queue when `_currentCommand` is null
- `QueueCommand()` adds to queue (respects MAX_COMMAND_NUM)
- `GetCommandQueue()` returns queue reference for UI display

**Key Methods:**
- `Think()` - Overrides to pull from command queue
- `QueueCommand(EntityCommand)` - Add command to queue
- `HasAssignedCommand()` - Check if actively executing
- `GetAssignedCommand()` - Get current command

## Key Classes

| Class | Description |
|-------|-------------|
| `Player` | Player-controlled necromancer entity |

## Important Notes

### Command Queue vs Current Command
Two-level command system:
1. `_currentCommand` - Currently executing command
2. `_commandQueue` - Pending commands waiting to execute

When current command completes (returns null from SuggestAction), next command is dequeued.

### Think Override
The player's Think method:
```csharp
public override EntityAction Think(...)
{
    if (_commandQueue.Count > 0 && _currentCommand == null)
    {
        _currentCommand = _commandQueue.Dequeue();
    }
    return base.Think(currentPosition, observationData);
}
```

### Movement Speed
The player is the fastest entity:
- Player: 0.5 points/tick (2 ticks per tile)
- Skeleton: 0.39 points/tick (~2.5 ticks per tile)
- Villager: 0.33 points/tick (~3 ticks per tile)
- Zombie: 0.15 points/tick (~6.7 ticks per tile)

### Queue Limit
Maximum of 7 queued commands:
```csharp
public const uint MAX_COMMAND_NUM = 7;
```
Attempting to queue beyond this returns false.

### Trait Usage Example
Uses the simplified `AddTraitToQueue` method:
```csharp
selfAsEntity().AddTraitToQueue<LivingTrait>(0);
```
This is cleaner than the separate create-add-initialize pattern.

### Living Entity
Despite being a necromancer, the player is a living entity:
- Has hunger need (via LivingTrait)
- Uses standard body systems
- Will need food (but specific strategies not yet implemented)

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.LivingTrait` - Living needs
- `VeilOfAges.Core.Lib.ReorderableQueue` - Command queue
- `VeilOfAges.UI.EntityCommand` - Command system

### Depended On By
- Player controller input handling
- UI systems (command queue display)
- Dialogue system (player-initiated conversations)
