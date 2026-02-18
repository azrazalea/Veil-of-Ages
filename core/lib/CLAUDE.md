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
  - `Building`: Navigate to a building. Has `requireInterior` parameter (default `true`):
    - When `true`: Entity must reach a walkable interior position; no perimeter fallback
    - When `false`: Entity can reach either an interior position or an adjacent perimeter position
  - `Facility`: Navigate adjacent to a specific facility within a building

**Threading Model (Critical)**:

Path calculation runs on the **Think thread** (background), not the Execute thread (main). This is enforced by splitting the old `TryFollowPath()` into two methods:

- **`CalculatePathIfNeeded(entity, perception)`**: Call from Think thread. Does A* calculation if path is needed. **Requires perception parameter** - entity paths around entities it can see.
- **`FollowPath(entity)`**: Call from Execute thread. Only follows pre-calculated path, no A*.

The old `TryFollowPath()` has been removed - use the split methods.

**Correct Usage Pattern**:
```csharp
// In Activity.GetNextAction(position, perception) - runs on Think thread
_pathFinder.SetPositionGoal(owner, targetPos);
if (!_pathFinder.CalculatePathIfNeeded(owner, perception))
{
    return new IdleAction(owner, this, priority);  // Path failed
}
return new MoveAlongPathAction(owner, this, _pathFinder, priority);

// In MoveAlongPathAction.Execute() - runs on Main thread
return _pathFinder.FollowPath(entity);  // No A* here, just follows
```

- **Key Methods**:
  - `SetBuildingGoal(entity, building, requireInterior = true)`: Sets building navigation goal with interior/adjacency control. Now detects cross-area targets and defers to cross-area routing when entity and building are in different areas.
  - `CalculatePathIfNeeded(entity, perception)`: **Call from Think thread**. Calculates path if needed, avoiding perceived entities.
  - `FollowPath(entity)`: **Call from Execute thread**. Follows pre-calculated path only.
  - `IsGoalReached(entity)`: Checks if entity has reached goal, respects `requireInterior` flag for Building goals
  - `GetBuildingPerimeterPositions(buildingPos, buildingSize)`: Helper method that returns all positions one tile outside the building bounds
  - `SetFacilityGoal(entity, building, facilityId)`: Sets facility navigation goal. Takes entity parameter for cross-area detection; when entity and building are in different areas, defers to cross-area routing.
  - `NeedsAreaTransition` (bool property): True when PathFinder has reached a transition point and needs the caller to execute a ChangeAreaAction.
  - `PendingTransition` (TransitionPoint? property): The transition point to use when `NeedsAreaTransition` is true.
  - `CompleteTransition(entity)`: Call after ChangeAreaAction executes. Advances the cross-area route and clears transition state.
- **Features**:
  - Automatic path recalculation with cooldown (5 ticks)
  - Maximum 3 recalculation attempts before failure
  - Path length limit (100 tiles)
  - Handles moving targets by recalculating when target entity moves
  - **Perception-aware pathfinding**: Entities path around other entities they can currently see (via `additionalBlocked` parameter)
  - **Perception-limited pathfinding**: Non-village residents can only pathfind within their perception range (fog-of-war wall at border)
  - **Periodic perception refresh**: Every 5 successful steps, triggers path recalculation with fresh perception data. Handles "new entity appeared in my way" scenarios without constant recalculation.
- **Thread Safety Warning**: `CreateNewAStarGrid()` and `UpdateAStarGrid()` must NOT be called from Tasks/threads - they print errors and return early if `Task.CurrentId != null`.
- **Perception-Based Pathfinding**:
  - `CreatePathfindingGrid(entity)`: Creates a perception-aware grid for path calculation
  - Village residents (`entity.IsVillageResident == true`) use the full terrain grid
  - Non-residents (undead, wanderers) get a cloned grid with perception border marked as solid
  - The border of perception range acts as a "fog of war" wall, preventing paths beyond what they can see
  - Uses `entity.MaxSenseRange` to determine perception radius
  - `CloneAStarGrid(source)`: Helper that deep-clones an AStarGrid2D with all solid/weight states
- **Cross-Area Navigation**:
  - When a Building or Facility goal is in a different area than the entity, PathFinder internally plans the route using `WorldNavigator.FindRouteToArea(entity, ...)` (BDI-compliant — uses entity's known transitions only).
  - PathFinder walks to each transition point, then signals `NeedsAreaTransition = true`.
  - NavigationActivity checks this flag and returns a `ChangeAreaAction`, then calls `CompleteTransition()`.
  - All navigation activities (GoToLocation, GoToBuilding, GoToFacility) get cross-area support automatically with zero code changes to the activities themselves.
  - The `_finalGoalType` field stores the real goal while cross-area routing is in progress. After all transitions complete, the real goal is restored via `SetFinalGoal()`.

### L.cs
Static localization helper for non-Node classes that cannot call `Godot.Object.Tr()` directly.

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `L` (static)
- **Caching**: Uses `ConcurrentDictionary` for both translated strings and parsed `CompositeFormat` instances, so repeated lookups are fast.
- **Key Methods**:
  - `Tr(key)`: Translate a key via `TranslationServer.Translate()`
  - `Tr(key, context)`: Translate with a context string
  - `TrN(singularKey, pluralKey, n)`: Plural-aware translation via `TranslationServer.TranslatePlural()`
  - `Fmt(translatedFormat, args)`: Format an already-translated string using cached `CompositeFormat`
  - `TrFmt(key, args)`: Translate then format in one call (`Fmt(Tr(key), args)`)
  - `ClearCache()`: Clear both caches (e.g., when locale changes)
- **Thread Safety**: All lookups are thread-safe (ConcurrentDictionary with read-only TranslationServer access). Safe to call from entity think threads.
- **Usage**: Used by activities, definitions (LocalizedName/LocalizedDescription), and other non-Node code that needs translated strings.

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

### WorldNavigator.cs
Static utility for cross-area route planning using an entity's knowledge (no god knowledge).

- **Namespace**: `VeilOfAges.Core.Lib`
- **Class**: `WorldNavigator` (static)
- **Key Data Types**:
  - `NavigationStep` (abstract record): Base for plan steps
  - `GoToPositionStep(Area, Vector2I)`: Move within an area
  - `TransitionStep(TransitionPoint)`: Traverse to another area
  - `NavigationPlan`: Complete route with steps and target metadata
- **Key Methods**:
  - `FindRouteToArea(entity, sourceArea, targetArea)`: BFS pathfinding across area graph using entity's known transitions
  - `NavigateToPosition(entity, sourceArea, sourcePos, targetArea, targetPos)`: Create navigation plan to reach a position (potentially cross-area)
  - `NavigateToFacility(entity, facilityType)`: Find and navigate to nearest facility of type from SharedKnowledge/PersonalMemory
  - `NavigateToBuilding(entity, buildingType)`: Find and navigate to nearest building of type from SharedKnowledge
- **Design Principle**: NO GOD KNOWLEDGE - only uses entity's SharedKnowledge and PersonalMemory to discover routes. Entities only know what their knowledge sources tell them.
- **Cross-Area Penalty**: Applies 10000 distance penalty when comparing targets across areas to prefer same-area targets

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
| `L` | Static localization helper with cached translations |
| `Log` | Static logging utility with tick prefixes and entity debug file output |

## Important Notes

### GameTime Immutability
`GameTime` instances are immutable. Methods like `Advance()` return new instances rather than modifying the existing one. This is important for time comparisons and thread safety.

### Pathfinding Performance
- The pathfinder uses Godot's built-in `AStarGrid2D` which is efficient
- `DiagonalMode.OnlyIfNoObstacles` prevents cutting corners through walls
- `Heuristic.Octile` is used for both compute and estimate heuristics
- Terrain difficulty weights are applied via `SetPointWeightScale()`
- For non-village residents, a cloned grid is created each path calculation (perception-limited)
- The grid clone copies all cells' solid and weight states, which has O(n) complexity where n = grid size

### Thread Safety
- **CRITICAL**: AStarGrid2D modification cannot happen in background tasks
- PathFinder methods that modify the grid check `Task.CurrentId` and abort if called from a thread
- This is a Godot engine limitation - scene tree modifications must happen on main thread
- **Path calculation** happens on Think thread (background) via `CalculatePathIfNeeded()`
- **Path following** happens on Execute thread (main) via `FollowPath()`
- This separation ensures A* does not block the main thread

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
