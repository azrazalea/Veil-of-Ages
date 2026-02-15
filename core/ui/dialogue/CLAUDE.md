# Dialogue System Module

## Purpose

The `/core/ui/dialogue` directory contains the dialogue system infrastructure including the controller, option representation, and the base command class. Commands issued through dialogue inherit from `EntityCommand`.

## Files

### Controller.cs
Manages dialogue option generation and command processing.

- **Namespace**: `VeilOfAges.UI`
- **Class**: `DialogueController`
- **Key Responsibilities**:
  - Generate initial dialogue options for entity interactions
  - Generate follow-up options after a choice is made
  - Process command assignments to entities
- **Default Options Generated**:
  - "Tell me about yourself." (simple info query)
  - "Follow me." (FollowCommand)
  - "Move to a location." (MoveToCommand)
  - "Guard an area." (GuardCommand)
  - "Return home." (ReturnHomeCommand)
  - "Goodbye." (closes dialogue)
- **Extensibility**: Entities can add custom options via `Being.AddDialogueOptions(speaker, options)`

### EntityCommand.cs
Abstract base class for all commands that can be assigned to entities.

- **Namespace**: `VeilOfAges.UI`
- **Class**: `EntityCommand` (abstract)
- **Constructor Parameters**:
  - `owner`: The Being receiving the command
  - `commander`: The Being issuing the command
  - `IsComplex`: Whether mindless entities can perform this (default: true)
- **Key Properties**:
  - `Parameters`: Dictionary for command-specific data (e.g., target position)
  - `MyPathfinder`: Each command has its own PathFinder instance
- **ISubActivityRunner**: EntityCommand implements `ISubActivityRunner`, enabling commands to drive sub-activities directly using the same `RunSubActivity` pattern as Activity. Adds `RunSubActivity(subActivity, position, perception, priority = -1)` wrapper and `InitializeSubActivity(subActivity)` helper for lazy sub-activity initialization.
- **Abstract Method**: `SuggestAction(Vector2I currentGridPos, Perception currentPerception)` - Returns the next action to perform, or null when complete

### Option.cs
Represents a single dialogue option with associated command and responses.

- **Namespace**: `VeilOfAges.UI`
- **Class**: `DialogueOption`
- **Properties**:
  - `Text`: Button label text
  - `Command`: Optional EntityCommand to execute
  - `_defaultSuccessResponse`, `_defaultFailureResponse`: Fallback text
  - `_isSimpleOption`: Skip entity-specific response lookup
  - `IsExplicitlyDisabled`: Facility options can be explicitly disabled (not based on WillRefuseCommand)
  - `DisabledReason`: Tooltip text explaining why option is disabled
  - `FacilityAction`: Callback action for facility options that don't use command system
- **Factory Method**:
  - `CreateFacilityOption(text, command, enabled, disabledReason, facilityAction)` - Create facility option with explicit disabled state
- **Response Resolution**:
  - Simple options use default responses directly
  - Complex options call `entity.GetSuccessResponse(command)` or `entity.GetFailureResponse(command)`

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `DialogueController` | Option generation and command routing |
| `EntityCommand` | Abstract base for all entity commands |
| `DialogueOption` | Single dialogue choice with responses |

## Important Notes

### Command Architecture
Commands follow a state machine pattern:
1. `SuggestAction()` is called each tick while command is active
2. Command maintains internal state (e.g., path progress)
3. Returns `EntityAction` to perform, or `null` when complete
4. Commands can drive sub-activities directly via `RunSubActivity()` instead of starting standalone activities
5. Actions use priority system (lower = higher priority):
   - `-10`: Crucial/emergency commands
   - `-1`: Normal command actions
   - `0`: Default command idle
   - `1`: Default trait actions

### IsComplex Flag
Commands marked as `IsComplex = true` (default) cannot be performed by entities with the "Mindless" trait. Set to `false` for simple movement commands.

### PathFinder Per Command
Each `EntityCommand` instance has its own `PathFinder` to track navigation state independently. This allows multiple commands to be queued without path conflicts.

### Response System
Entities can provide contextual responses:
- `entity.GetSuccessResponse(command)` - Custom acceptance text
- `entity.GetFailureResponse(command)` - Custom refusal text
- Falls back to option's default responses if entity returns null

## Dependencies

### This module depends on:
- `VeilOfAges.Core.Lib.PathFinder` - Navigation for commands
- `VeilOfAges.Entities` - Being, EntityAction
- `VeilOfAges.Entities.Sensory` - Perception for command decisions

### Depended on by:
- `VeilOfAges.UI.Dialogue` - Uses DialogueController and DialogueOption
- `VeilOfAges.Core.PlayerInputController` - Uses EntityCommand for player actions
- Entity AI systems consume EntityCommand instances

## Subdirectories

- `commands/` - Concrete command implementations (MoveToCommand, FollowCommand, etc.)
