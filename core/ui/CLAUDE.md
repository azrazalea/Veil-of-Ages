# Core UI Module

## Purpose

The `/core/ui` directory contains the user interface systems, primarily focused on the dialogue system that enables player interaction with entities.

## Files

### Dialogue.cs
The main dialogue UI component that displays conversations and command options.

- **Namespace**: `VeilOfAges.UI`
- **Class**: `Dialogue : Control`
- **Key Responsibilities**:
  - Display dialogue panel with entity name and text
  - Generate and display command options as buttons
  - Process option selections and command execution
  - Handle location selection for position-based commands
  - Display facility interaction dialogues (non-conversation mode)
- **Exports**:
  - `_nameLabel`: RichTextLabel for entity name
  - `_dialogueText`: RichTextLabel for dialogue content
  - `_optionsContainer`: GridContainer for option buttons
- **Important**: Checks `Being.WillRefuseCommand()` before showing dialogue and disables buttons for refused commands. Fires `GameEvents.DialogueStateChanged` when opening/closing.
- **Facility Dialogue**: `ShowFacilityDialogue(speaker, facility)` shows facility interaction options with explicit enabled/disabled states and tooltips.

### NecromancerTheme.cs
Programmatic Godot Theme builder for the necromancer UI aesthetic.

- **Namespace**: `VeilOfAges.Core.UI`
- **Class**: `NecromancerTheme` (static)
- **Method**: `Build()` returns a configured `Theme`
- Dark palette: bg `#2d2d42`, border `#7b5aad`, text white, gold accents
- Type variations: `HeaderLabel`, `ValueLabel`, `DimLabel`
- Public colors: `ColorCritical`, `ColorGood` for panel use

### TopBarPanel.cs
Top bar displaying location, date, time of day, and speed controls.

- **Namespace**: `VeilOfAges.Core.UI`
- **Class**: `TopBarPanel : PanelContainer`
- Builds UI programmatically in `_Ready()`
- Subscribes to `UITickFired`, `SimulationPauseChanged`, `TimeScaleChanged`
- Speed buttons call `Services.Get<GameController>()`

### CharacterPanel.cs
Bottom-left character cluster with name, activity, automation indicator.

- **Namespace**: `VeilOfAges.Core.UI`
- **Class**: `CharacterPanel : PanelContainer`
- Shows portrait placeholder, player name, current activity, "MANUAL"/"AUTO" indicator
- Subscribes to `UITickFired`, `AutomationToggled`

### NeedsPanel.cs
Dynamic needs display showing 2-3 most critical needs.

- **Namespace**: `VeilOfAges.Core.UI`
- **Class**: `NeedsPanel : PanelContainer`
- Hides entirely when all needs satisfied (calm HUD)
- Uses `_Process` only for smooth bar lerping between UI tick updates
- Trend arrows: `↑` rising, `→` stable, `↓` declining
- Bar colors change: purple (normal), orange (low), red (critical)

### CommandQueuePanel.cs
Horizontal command queue strip with node pooling.

- **Namespace**: `VeilOfAges.Core.UI`
- **Class**: `CommandQueuePanel : PanelContainer`
- Current command highlighted in gold, queued commands in white
- Uses label pool (grow-only, hide unused) instead of destroy/recreate
- Subscribes to `CommandQueueChanged` and `UITickFired`

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `Dialogue` | Main dialogue UI panel controller (`Control`, fires `DialogueStateChanged`) |
| `NecromancerTheme` | Programmatic theme builder (dark palette, gold accents) |
| `TopBarPanel` | Top bar: location, date, time of day, speed controls |
| `CharacterPanel` | Bottom-left: name, activity, MANUAL/AUTO indicator |
| `NeedsPanel` | Dynamic critical needs display with trend arrows and color coding |
| `CommandQueuePanel` | Horizontal command queue strip with node pooling |

## Important Notes

### Dialogue Flow

**Entity Dialogue:**
1. `ShowDialogue(speaker, target)` is called (typically from PlayerInputController)
2. Target entity's `WillRefuseCommand(TalkCommand)` is checked
3. If accepted, `target.BeginDialogue(speaker)` is called
4. Initial dialogue text generated via `target.GenerateInitialDialogue(speaker)`
5. Options generated via `DialogueController.GenerateOptionsFor()`
6. User selects option, command processed, follow-up options generated or dialogue closes

**Facility Dialogue:**
1. `ShowFacilityDialogue(speaker, facility)` is called (typically from PlayerInputController context menu)
2. Facility's `GetInteractionOptions(speaker)` is called
3. Options displayed with explicit enabled/disabled state (not based on WillRefuseCommand)
4. User selects enabled option, command/callback executed, dialogue closes

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
- `VeilOfAges.Core.Services` - Resolves GameController and Player without `GetNode` paths
- `VeilOfAges.Core.GameEvents` - Subscribes to `UITickFired`, `SimulationPauseChanged`, `TimeScaleChanged`, `CommandQueueChanged`, `AutomationToggled`, `DialogueStateChanged`
- `VeilOfAges.Entities.Being` - Entity dialogue methods
- `VeilOfAges.UI.Commands` - TalkCommand, MoveToCommand, GuardCommand

### Depended on by:
- `VeilOfAges.Core.PlayerInputController` - Opens dialogue on entity interaction

## Subdirectories

- `dialogue/` - Dialogue system components (Controller, Option, EntityCommand)
  - `commands/` - Specific command implementations
