# Core Library Module

## Purpose

The `/core/lib` directory contains shared utility classes and core algorithms used throughout the game. These are foundational systems that provide time management, pathfinding, and data structures.

## Files

### GameTime.cs
Comprehensive game time system with a custom base-56 calendar structure.

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `GameTime`
- **Calendar Structure**:
  - 100 centiseconds per second
  - 56 seconds per minute
  - 56 minutes per hour
  - 14 hours per day
  - 28 days per month
  - 13 months per year (364 days)
- **Month Names**: Seedweave, Marketbloom, Tradewind, Goldtide, Growthsong, Marketfire, Tradebounty, Goldfall, Mistmarket, Frostfair, Deepmarket, Starbarter, Thawcraft
- **Seasons**: Spring (months 1-3), Summer (4-6), Autumn (7-9), Winter (10-13)
- **Simulation Constants**:
  - `SimulationTickRate`: 8 ticks per real second
  - `GameCentisecondsPerRealSecond`: 3680 (36.8 game seconds per real second)
  - `GameCentisecondsPerGameTick`: 460

### Pathfinder.cs
A* pathfinding system that wraps Godot's `AStarGrid2D`.

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `PathFinder`
- **Goal Types**:
  - `Position`: Navigate to exact grid position
  - `EntityProximity`: Get within range of a moving entity
  - `Area`: Reach any position within a circular area
  - `Building`: Navigate to adjacent position of a multi-tile building
- **Features**:
  - Automatic path recalculation with cooldown (5 ticks)
  - Maximum 3 recalculation attempts before failure
  - Path length limit (100 tiles)
  - Handles moving targets by recalculating when target entity moves
- **Thread Safety Warning**: `CreateNewAStarGrid()` and `UpdateAStarGrid()` must NOT be called from Tasks/threads - they print errors and return early if `Task.CurrentId != null`.

### ReorderableQueue.cs
A generic queue implementation backed by `LinkedList<T>` that supports dynamic reordering.

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `ReorderableQueue<T>`
- **Operations**:
  - `Enqueue(T)`: Add to end
  - `Dequeue()`: Remove from front
  - `FindCommand(Predicate<T>)`: Search by predicate
  - `MoveToBefore/MoveToAfter/MoveToFront/MoveToLast`: Reposition elements
- **Use Case**: Used for entity command queues where command priority may change.

### Log.cs
Logging utility that prefixes messages with the current game tick. Supports both console output and file-based entity debug logging.

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `Log` (static)
- **Basic Methods**:
  - `Print(string)`: Log message to console with tick prefix
  - `Error(string)`: Push error with tick prefix
  - `Warn(string)`: Push warning with tick prefix
  - `PrintRich(string)`: Log with BBCode formatting support
- **Entity Debug System**:
  - `EntityDebug(entityName, category, message, tickInterval)`: Logs debug messages for specific entities to both console and file
  - **Rate Limiting**: Uses `tickInterval` parameter (default: 100 ticks) to prevent log spam
  - **Rate Limit Key**: `"{entityName}:{category}"` - each entity+category combination has independent rate limiting
  - **File Output**: `user://logs/entities/<entityName>.log` (Godot user directory)
  - **File Format**: `[HH:mm:ss.fff] [Tick N] [CATEGORY] message`
  - `Shutdown()`: Closes all log file writers, clears `_debugWriters` and `_lastLogTicks` dictionaries, resets initialization flag
- **Internal State**:
  - `_debugWriters`: Dictionary mapping entity names to StreamWriter instances
  - `_lastLogTicks`: Dictionary tracking last log tick per entity+category for rate limiting
  - `_logDirectory`: Cached path to entity log directory
  - `_debugInitialized`: Flag indicating if debug system has been initialized
- **Initialization Behavior**:
  - Lazy initialization on first `EntityDebug()` call
  - Clears all existing `.log` files in the entity log directory on startup
  - Creates log directory if it doesn't exist

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `GameTime` | Immutable time value with calendar parsing and formatting |
| `PathFinder` | A* pathfinding with multiple goal types |
| `ReorderableQueue<T>` | Dynamic priority queue |
| `Log` | Static logging utility with tick prefixes and entity debug file output |

## Important Notes

### GameTime Immutability
`GameTime` instances are immutable. Methods like `Advance()` return new instances rather than modifying the existing one. This is important for time comparisons and thread safety.

### Pathfinding Performance
- The pathfinder uses Godot's built-in `AStarGrid2D` which is efficient
- `DiagonalMode.OnlyIfNoObstacles` prevents cutting corners through walls
- `Heuristic.Octile` is used for both compute and estimate heuristics
- Terrain difficulty weights are applied via `SetPointWeightScale()`

### Thread Safety
- **CRITICAL**: AStarGrid2D modification cannot happen in background tasks
- PathFinder methods that modify the grid check `Task.CurrentId` and abort if called from a thread
- This is a Godot engine limitation - scene tree modifications must happen on main thread

### Time Conversion
- `GameToRealTime()`: Convert game centiseconds to real seconds
- `RealToGameTime()`: Convert real seconds to game centiseconds
- These are useful for UI display and scheduling

## Dependencies

### This module depends on:
- `Godot` - Vector2I, AStarGrid2D, GD print utilities
- `VeilOfAges.Entities` - Being class for pathfinding targets
- `VeilOfAges.Grid` - Area class for walkability checks

### Depended on by:
- `VeilOfAges.Core.GameController` - Uses GameTime for simulation
- `VeilOfAges.UI.EntityCommand` - Commands use PathFinder for navigation
- Entity AI systems for movement decisions
