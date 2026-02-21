# Core Module

## Purpose

The `/core` directory contains the central game systems responsible for game loop management, player input handling, and the simulation tick system. This is the heart of the game's runtime coordination.

## Files

### GameController.cs
The main game loop controller that manages simulation timing and tick processing.

- **Namespace**: `VeilOfAges.Core`
- **Class**: `GameController : Node`
- **Key Responsibilities**:
  - Controls the simulation tick rate (8 ticks per real second at 1.0 time scale)
  - Manages game time advancement via `GameTime`
  - Coordinates with `EntityThinkingSystem` to process entity AI each tick
  - Provides simulation pause/resume/speed controls
- **Exports**:
  - `TimeScale`: Adjustable speed multiplier (0.1 to 10.0)
  - `MaxPlayerActions`: Maximum concurrent player actions (default: 3)
- **Important**: The `ProcessNextTick()` method is async and uses `_processingTick` flag to prevent overlapping tick processing.

### PlayerInputController.cs
Handles all player input including mouse clicks, keyboard shortcuts, and context menus. (~470 lines of pure input handling)

- **Namespace**: `VeilOfAges.Core`
- **Class**: `PlayerInputController : Node`
- **Key Responsibilities**:
  - Mouse click handling for movement and entity interaction
  - Context menu generation and handling
  - Location selection mode for commands requiring position input
  - Simulation control hotkeys (pause, speed up/down)
  - Automation toggle (manual/automatic behavior)
  - Facility interaction (detects and shows facility dialogue)
  - Transition point interaction (detects and allows entering cellars/other areas)
- **Input Method**: Uses `_UnhandledInput` for input processing
- **Dependencies resolved via**: `Services.Get<GameController>()` and `Services.Get<Player>()` — no direct `GetNode` paths
- **Exports**: Only `_dialogueUI`, `_chooseLocationPrompt`, and `_contextMenu` — no HUD display element exports
- **Input Actions**: `interact`, `exit`, `toggle_simulation_pause`, `speed_up`, `slow_down`, `context_menu`, `toggle_automation`, `toggle_skills_panel`, `toggle_knowledge_panel`
- **Important**: Uses `_awaitingLocationSelection` flag for commands that need a target position (like MoveToCommand, GuardCommand).

### Services.cs
Static service locator for decoupled access to global singletons.

- **Namespace**: `VeilOfAges.Core`
- **Class**: `Services` (static)
- **Methods**: `Register<T>()`, `Get<T>()`, `TryGet<T>()`
- Used by UI panels and PlayerInputController to access GameController and Player without `GetNode` paths.

### GameEvents.cs
Static event bus for broadcasting game-wide events to decoupled listeners.

- **Namespace**: `VeilOfAges.Core`
- **Class**: `GameEvents` (static)
- **Events**: `UITickFired`, `SimulationPauseChanged`, `TimeScaleChanged`, `CommandQueueChanged`, `AutomationToggled`, `DialogueStateChanged`
- **UITickFired** fires every 2 simulation ticks from GameController. UI panels subscribe to this instead of running their own `_Process` timers.
- Instant events (pause, speed, etc.) provide responsive feedback for user actions.

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `GameController` | Main game loop, tick processing, time management |
| `PlayerInputController` | Player input handling (~470 lines of pure input handling) |
| `Services` | Static service locator for decoupled singleton access |
| `GameEvents` | Static event bus for game-wide broadcast events |

## Important Notes

### Tick System
- Simulation runs at `GameTime.SimulationTickRate` (8 ticks/second at normal speed)
- Each tick advances game time by `GameCentisecondsPerGameTick` (460 centiseconds)
- Tick processing is async to allow for entity AI computation

### Thread Safety
- `_processingTick` prevents concurrent tick processing
- Entity thinking system processes are awaited before continuing

### UI Integration
- PlayerInputController no longer has direct exports to HUD display elements. Its only scene exports are `_dialogueUI`, `_chooseLocationPrompt`, and `_contextMenu`.
- HUD panels (TopBarPanel, CharacterPanel, NeedsPanel, CommandQueuePanel) are self-contained: they subscribe to `GameEvents` and resolve dependencies via `Services`.
- Context menu dynamically populated based on click target (entity vs. empty tile)

## Dependencies

### This module depends on:
- `VeilOfAges.Core.Lib` - GameTime for time management
- `VeilOfAges.Entities` - Being, Player, EntityThinkingSystem
- `VeilOfAges.UI` - Dialogue system for entity interaction
- `VeilOfAges.UI.Commands` - MoveToCommand, GuardCommand for player actions
- `VeilOfAges.Grid` - Grid utilities and world navigation

### Depended on by:
- UI components that need game state (time, pause status)
- Entity systems that respond to simulation ticks
- World systems that update based on game time

## Subdirectories

- `debug/` - HTTP debug server for AI-assisted debugging (DEBUG builds only)
- `lib/` - Library utilities (GameTime, PathFinder, ReorderableQueue)
- `main/` - Empty (reserved for future main scene logic)
- `ui/` - UI systems and dialogue
