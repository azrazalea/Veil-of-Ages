# /entities/player

## Purpose

This directory contains the player entity implementation. The player is the necromancer character (starting as a village scholar) controlled by the user, with Sims-like autonomous behavior when no commands are queued.

## Files

### Player.cs
The player-controlled necromancer entity.

**Configuration:**
- Uses base attribute set (all 10s)
- Movement: 0.5 points/tick (fastest entity, 2 ticks per tile)
- Named "Lilith Galonadel" by default
- Maximum command queue: 7 commands
- Definition ID: "player" (loaded from `resources/entities/definitions/player.json`)

**Trait Composition:**
- `ScheduleTrait` (priority 0) - Sleep scheduling with allowNightWork=true
- `ScholarJobTrait` (priority -1) - Daytime study activity at home
- `NecromancyStudyJobTrait` (priority -1) - Nighttime necromancy study
- `ItemConsumptionBehaviorTrait` (priority 1) - Food consumption
- `AutomationTrait` (priority 4) - Manual/auto toggle

**Home System:**
- `_home` - Reference to player's house building
- `Home` property - Public getter for home building
- `SetHome(Building)` - Sets home, registers as resident, notifies traits

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
- `SetHome(Building)` - Assign home building and register as resident

## Key Classes

| Class | Description |
|-------|-------------|
| `Player` | Player-controlled necromancer entity |

## Autonomous Behavior System

### Sims-Like Behavior
When the player has no commands queued, they behave autonomously:

1. **Hunger Satisfaction**: `ItemConsumptionBehaviorTrait`
   - Checks inventory and home storage for food
   - Critical hunger (priority -2) interrupts commands
   - Uses `ConsumeItemActivity` when food available
   - Uses `CheckHomeStorageActivity` to observe storage when memory empty

2. **Sleep Schedule**: `ScheduleTrait`
   - During Dusk/Night, if energy is low, navigates home and sleeps
   - Defers to night jobs (NecromancyStudyJobTrait) when `allowNightWork` is true
   - Critical energy forces sleep regardless of time/jobs
   - Uses `SleepActivity` to restore energy

3. **Scholar Work**: `ScholarJobTrait`
   - During Dawn/Day phases, studies at home
   - Uses `StudyActivity` for scholarly work
   - Spends energy while studying (mentally taxing)

4. **Necromancy Study**: `NecromancyStudyJobTrait`
   - During Night phase, studies at necromancy altar
   - ScheduleTrait defers sleep to allow night work

5. **Idle Fallback**: `Being.Think()`
   - When no trait suggests an action, defaults to IdleAction
   - Commands always take precedence via priority system

### Priority System
The existing action priority system handles interruption:
- Commands return priority `-1` to `0`
- `ItemConsumptionBehaviorTrait` returns priority `-2` when hunger is critical
- `ScheduleTrait` returns priority `-1` for critical energy sleep, `0` for normal sleep
- Lower priority wins, so critical hunger interrupts commands automatically

### Deference to Commands
Commands get priority -1 in the priority queue, so they win over trait suggestions
at priority 0-1. The priority system makes explicit command-queue checking unnecessary.

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

### Home Assignment
The player's home is assigned during village generation:
1. `VillageGenerator.PlacePlayerHouseNearGraveyard()` places house near graveyard
2. Calls `Player.SetHome()` which:
   - Sets `_home` reference
   - Registers player as resident of the building
   - Notifies `ScholarJobTrait`
3. This happens BEFORE `InitializeGranaryOrders()` so scholar food priority works

### Scholar Food Priority
Because the player has `ScholarJobTrait` (can't produce their own food):
- Their household gets priority `-1` for bread delivery (higher than normal priority `0`)
- This is detected in `GranaryTrait.InitializeOrdersFromVillage()` via `HasScholarResident()`

### Living Entity
Despite being a necromancer, the player is a living entity:
- Has hunger need (via LivingTrait, inherited from living_base)
- Has energy need for sleep (via LivingTrait)
- Uses standard body systems

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.ScheduleTrait` - Sleep scheduling
- `VeilOfAges.Entities.Traits.ScholarJobTrait` - Daytime work
- `VeilOfAges.Entities.Building` - Home building type
- `VeilOfAges.Core.Lib.ReorderableQueue` - Command queue
- `VeilOfAges.UI.EntityCommand` - Command system

### Depended On By
- Player controller input handling
- UI systems (command queue display)
- Dialogue system (player-initiated conversations)
- `VillageGenerator` - Assigns player home during generation
