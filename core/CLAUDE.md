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
Handles all player input including mouse clicks, keyboard shortcuts, and context menus.

- **Namespace**: `VeilOfAges.Core`
- **Class**: `PlayerInputController : Node`
- **Key Responsibilities**:
  - Mouse click handling for movement and entity interaction
  - Context menu generation and handling
  - Location selection mode for commands requiring position input
  - HUD updates (name label, hunger bar, activity display, command queue)
  - Simulation control hotkeys (pause, speed up/down)
- **Input Actions**: `interact`, `exit`, `toggle_simulation_pause`, `speed_up`, `slow_down`, `context_menu`
- **Important**: Uses `_awaitingLocationSelection` flag for commands that need a target position (like MoveToCommand, GuardCommand).

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `GameController` | Main game loop, tick processing, time management |
| `PlayerInputController` | Player input handling, UI state management |

## Important Notes

### Tick System
- Simulation runs at `GameTime.SimulationTickRate` (8 ticks/second at normal speed)
- Each tick advances game time by `GameCentisecondsPerGameTick` (460 centiseconds)
- Tick processing is async to allow for entity AI computation

### Thread Safety
- `_processingTick` prevents concurrent tick processing
- Entity thinking system processes are awaited before continuing

### UI Integration
- PlayerInputController has direct exports to HUD elements (minimap, quick actions, dialogue)
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
