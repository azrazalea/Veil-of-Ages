# Core UI Module

## Purpose

The `/core/ui` directory contains the user interface systems, primarily focused on the dialogue system that enables player interaction with entities.

## Files

### Dialogue.cs
The main dialogue UI component that displays conversations and command options.

- **Namespace**: `VeilOfAges.UI`
- **Class**: `Dialogue : CanvasLayer`
- **Key Responsibilities**:
  - Display dialogue panel with entity name and text
  - Generate and display command options as buttons
  - Process option selections and command execution
  - Handle location selection for position-based commands
- **Exports**:
  - `_nameLabel`: RichTextLabel for entity name
  - `_dialogueText`: RichTextLabel for dialogue content
  - `_optionsContainer`: GridContainer for option buttons
  - `_minimap`, `_quickActions`: Panels hidden during dialogue
- **Important**: Checks `Being.WillRefuseCommand()` before showing dialogue and disables buttons for refused commands.

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `Dialogue` | Main dialogue UI panel controller |

## Important Notes

### Dialogue Flow
1. `ShowDialogue(speaker, target)` is called (typically from PlayerInputController)
2. Target entity's `WillRefuseCommand(TalkCommand)` is checked
3. If accepted, `target.BeginDialogue(speaker)` is called
4. Initial dialogue text generated via `target.GenerateInitialDialogue(speaker)`
5. Options generated via `DialogueController.GenerateOptionsFor()`
6. User selects option, command processed, follow-up options generated or dialogue closes

### Option Button States
- Buttons are disabled if the target would refuse the associated command
- Tooltip shows "I will refuse this command." for disabled options
- This provides player feedback before attempting commands

### Special Command Handling
`MoveToCommand` and `GuardCommand` trigger location selection mode:
- Dialogue closes immediately
- `PlayerInputController.StartLocationSelection()` is called
- Game pauses while awaiting click
- After location selected, command receives target position

## Dependencies

### This module depends on:
- `VeilOfAges.Core.PlayerInputController` - For location selection
- `VeilOfAges.Entities.Being` - Entity dialogue methods
- `VeilOfAges.UI.Commands` - TalkCommand, MoveToCommand, GuardCommand

### Depended on by:
- `VeilOfAges.Core.PlayerInputController` - Opens dialogue on entity interaction

## Subdirectories

- `dialogue/` - Dialogue system components (Controller, Option, EntityCommand)
  - `commands/` - Specific command implementations
