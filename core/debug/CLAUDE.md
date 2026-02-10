# Debug Module

## Purpose

The `/core/debug` directory contains the HTTP debug server for AI-assisted debugging and game state inspection. The server allows external tools (like curl or Claude Code) to query game state and issue commands.

## Process Management

### Building the Project

**IMPORTANT: Always build before running to ensure C# changes are compiled.**

```bash
cd "C:\Users\azraz\Documents\Veil of Ages" && dotnet build
```

### Starting the Game

The Godot executable is located at `C:\Users\azraz\Godot\` with a version-specific name.

```powershell
# Start the game (PowerShell)
powershell.exe -Command "Start-Process -FilePath 'C:\Users\azraz\Godot\Godot_v4.6-stable_mono_win64.exe' -ArgumentList '--path \"C:\Users\azraz\Documents\Veil of Ages\"'"
```

### Killing the Game

```powershell
# Kill by process name
powershell.exe -Command "Stop-Process -Name 'Godot*' -Force -ErrorAction SilentlyContinue"
```

### Full Restart (Kill + Start)

```powershell
# Build, kill any existing instance, wait, then start fresh
cd "C:\Users\azraz\Documents\Veil of Ages" && dotnet build && powershell.exe -Command "Stop-Process -Name 'Godot*' -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500; Start-Process -FilePath 'C:\Users\azraz\Godot\Godot_v4.6-stable_mono_win64.exe' -ArgumentList '--path \"C:\Users\azraz\Documents\Veil of Ages\"'"
```

### Checking if Running

```powershell
# Check if Godot is running
powershell.exe -Command "Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue | Select-Object Name, Id"

# Check if debug server is responding (wait ~8 seconds after start)
curl -s http://localhost:8765/ping
```

**Note**: The debug server only runs in DEBUG builds. When running from the Godot editor or via command line without export flags, it runs in debug mode by default.

**Performance Note**: API responses are nearly instant - no need for sleeps between commands. The only exception is after starting the game, where you should wait ~8 seconds for the debug server to initialize.

## Reading Logs

**IMPORTANT: Always prefer file-based log access over HTTP endpoints.**

### Log File Location

```
C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\game.log
```

### Reading Logs

```powershell
# Read entire log
powershell.exe -Command "Get-Content 'C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\game.log'"

# Read last 50 lines
powershell.exe -Command "Get-Content 'C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\game.log' -Tail 50"

# Search for specific patterns (e.g., errors, debug server messages)
powershell.exe -Command "Get-Content 'C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\game.log' | Select-String -Pattern 'ERROR|DebugServer'"

# Watch log in real-time
powershell.exe -Command "Get-Content 'C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\game.log' -Wait -Tail 20"
```

### Entity Debug Logs

Per-entity debug logs are at:
```
C:\Users\azraz\AppData\Roaming\Godot\app_userdata\Veil of Ages\logs\entities\<EntityName>.log
```

## Files

### DebugServer.cs
Main HTTP server autoload. Handles:
- TcpListener on localhost:8765
- HTTP/1.1 request parsing and routing
- State snapshot capture (once per frame)
- Command queue processing on main thread
- Wrapped in `#if DEBUG` with empty stub for release builds

### DebugSnapshot.cs
State snapshot models for JSON serialization:
- `GameStateSnapshot` - Full game state (tick, time, paused, entities, grid)
- `EntitySnapshot` - Comprehensive entity state (see Entity Snapshot Fields below)
- `SkillSnapshot` - Skill state (id, name, level, xp, progress)
- `AttributeSnapshot` - All 7 attribute values
- `HealthSnapshot` - Health percentage, status string, efficiency
- `ItemSnapshot` - Inventory item (id, name, quantity)
- `GridSnapshot` - Grid visualization with `ToAscii()` method
- `BuildingSnapshot` - Building info (name, type, position, size)
- `AutonomyRuleSnapshot` - Autonomy rule state (id, displayName, traitType, priority, enabled, activeDuringPhases)

### DebugCommand.cs
Command classes for server control:
- `DebugCommand` - Abstract base with `Description` and `Execute(GameController)`
- `PauseCommand` - Pause simulation
- `ResumeCommand` - Resume simulation
- `StepCommand` - Step N ticks (requires paused state)
- `PlayerMoveToCommand` - Move player to grid position
- `PlayerFollowCommand` - Make player follow entity
- `PlayerCancelCommand` - Cancel player command
- `AutonomySetEnabledCommand` - Enable/disable autonomy rule
- `AutonomyReorderCommand` - Change rule priority
- `AutonomyAddRuleCommand` - Add new autonomy rule
- `AutonomyRemoveRuleCommand` - Remove autonomy rule
- `AutonomyReapplyCommand` - Force reapply all rules

## HTTP API

### Read Endpoints (GET)

| Endpoint | Description |
|----------|-------------|
| `/ping` | Health check, returns `{"pong":true}` |
| `/state` | Full game state snapshot (JSON) |
| `/entities` | List all entities with summary info |
| `/entity/{name}` | Detailed info for specific entity (URL-encode spaces as %20) |
| `/grid` | ASCII grid visualization (plain text) |
| `/events` | Recent game events (not yet implemented) |
| `/player/autonomy` | Get all player autonomy rules (JSON) |

### Write Endpoints (POST)

| Endpoint | Description |
|----------|-------------|
| `/pause` | Pause simulation |
| `/resume` | Resume simulation |
| `/step?ticks=N` | Step N ticks (1-100, default 1, requires paused) |
| `/restart` | Reload current scene (server stays up, tick counter persists) |
| `/quit` | Quit the game cleanly |
| `/player/move?x=N&y=N` | Move player to grid position |
| `/player/follow?entity=NAME` | Make player follow named entity (URL-encode name) |
| `/player/cancel` | Cancel player's current command |
| `/player/autonomy/enable?rule=ID` | Enable an autonomy rule and reapply |
| `/player/autonomy/disable?rule=ID` | Disable an autonomy rule and reapply |
| `/player/autonomy/reorder?rule=ID&priority=N` | Change rule priority and reapply |
| `/player/autonomy/add?id=ID&name=NAME&trait=TYPE&priority=N&phases=Dawn,Day` | Add new rule (phases optional) and reapply |
| `/player/autonomy/remove?rule=ID` | Remove a rule and reapply |
| `/player/autonomy/reapply` | Force reapply all autonomy rules |

### Entity Snapshot Fields

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Entity instance name |
| `type` | string | C# class name (e.g., "GenericBeing") |
| `definitionId` | string? | Being definition ID (e.g., "player", "villager") |
| `position` | Vector2I | Current grid position |
| `activity` | string | Activity class name (or "Idle") |
| `activityDisplayName` | string? | Human-readable activity name (e.g., "Going to study", "Working at Farm") |
| `command` | string? | Active player command class name (null if none) |
| `needs` | dict | Need ID to value (0-100) mapping |
| `skills` | array | Skill snapshots with id, name, level, xp, progress |
| `attributes` | object | All 7 attributes (strength, dexterity, constitution, intelligence, willpower, wisdom, charisma) |
| `health` | object | Health percentage (0-1), status string, efficiency (0-1) |
| `inventory` | array | Items in entity's inventory (id, name, quantity) |
| `traits` | array | Trait class names |
| `village` | string? | Village name if entity belongs to one |
| `isMoving` | bool | Currently moving |
| `isInQueue` | bool | Waiting in a queue |
| `isHidden` | bool | Entity is hidden/dormant |
| `autonomyRules` | array? | Autonomy rule snapshots (player only): id, displayName, traitType, priority, enabled, activeDuringPhases |

### Example Usage

**Use `jq` for parsing JSON responses** - it's more reliable than grep/python for JSON.

```bash
# Check server is running
curl -s http://localhost:8765/ping

# Get game time and tick
curl -s http://localhost:8765/state | jq '{tick: .tick, time: .gameTime}'

# Get all entities with key info (first 15)
curl -s http://localhost:8765/entities | jq -r '.[:15] | .[] | "\(.name[:30]) \(.position) \(.activityDisplayName // .activity)"'

# Get specific entity with full details (URL-encode spaces)
curl -s "http://localhost:8765/entity/Player-abc12345" | jq .

# Get entity skills
curl -s "http://localhost:8765/entity/Player-abc12345" | jq '.skills[] | "\(.name) L\(.level) (\(.progress * 100 | floor)%)"'

# Get entity attributes
curl -s "http://localhost:8765/entity/Player-abc12345" | jq '.attributes'

# Get entity health status
curl -s "http://localhost:8765/entity/Player-abc12345" | jq '{health: .health.status, efficiency: .health.efficiency}'

# Get entity inventory
curl -s "http://localhost:8765/entity/Player-abc12345" | jq '.inventory[] | "\(.quantity)x \(.name)"'

# Count entities by activity type
curl -s http://localhost:8765/entities | jq 'group_by(.activity) | map({activity: .[0].activity, count: length})'

# Find entities with commands
curl -s http://localhost:8765/entities | jq '.[] | select(.command != null) | {name, command, activity: .activityDisplayName}'

# Find queued or hidden entities
curl -s http://localhost:8765/entities | jq '.[] | select(.isInQueue or .isHidden)'

# View ASCII grid (first 20 lines)
curl -s http://localhost:8765/grid | head -20

# Get just the tick count (grep fallback)
curl -s http://localhost:8765/state | grep -o '"tick":[0-9]*'

# Pause, step 5 ticks, resume
curl -s -X POST http://localhost:8765/pause
curl -s -X POST "http://localhost:8765/step?ticks=5"
curl -s -X POST http://localhost:8765/resume

# Restart the scene
curl -s -X POST http://localhost:8765/restart

# Quit the game
curl -s -X POST http://localhost:8765/quit

# View player autonomy rules
curl -s http://localhost:8765/player/autonomy | jq .

# Move player to position
curl -s -X POST "http://localhost:8765/player/move?x=50&y=50"

# Follow an entity
curl -s -X POST "http://localhost:8765/player/follow?entity=Aelar%20Vossian"

# Cancel current command
curl -s -X POST http://localhost:8765/player/cancel

# Disable necromancy study
curl -s -X POST "http://localhost:8765/player/autonomy/disable?rule=study_necromancy"

# Enable necromancy study
curl -s -X POST "http://localhost:8765/player/autonomy/enable?rule=study_necromancy"

# Force reapply all autonomy rules
curl -s -X POST http://localhost:8765/player/autonomy/reapply

# Check player entity snapshot includes autonomy
curl -s "http://localhost:8765/entity/Lilith%20Galonadel" | jq '.autonomyRules'
```

### ASCII Grid Characters

| Char | Meaning |
|------|---------|
| `.` | Empty/grass |
| `#` | Building |
| `@` | Idle entity |
| `W` | Walking entity |
| `H` | Hidden entity |
| `Q` | Queued entity |
| `~` | Water |

## Key Classes/Interfaces

| Class | Description |
|-------|-------------|
| `DebugServer` | HTTP server autoload (DEBUG builds only) |
| `DebugCommand` | Abstract base for debug commands |
| `PauseCommand` | Pauses simulation |
| `ResumeCommand` | Resumes simulation |
| `StepCommand` | Steps N ticks while paused |
| `GameStateSnapshot` | Full game state for JSON serialization |
| `EntitySnapshot` | Entity state (position, activity, needs, skills, attributes, health, inventory, traits) |
| `GridSnapshot` | Grid with ASCII rendering |
| `BuildingSnapshot` | Building information |
| `AutonomyRuleSnapshot` | Autonomy rule state for serialization |
| `PlayerMoveToCommand` | Move player to grid position |
| `PlayerFollowCommand` | Make player follow entity |
| `PlayerCancelCommand` | Cancel player's current command |
| `AutonomySetEnabledCommand` | Enable/disable autonomy rule |
| `AutonomyReorderCommand` | Change autonomy rule priority |
| `AutonomyAddRuleCommand` | Add new autonomy rule |
| `AutonomyRemoveRuleCommand` | Remove autonomy rule |
| `AutonomyReapplyCommand` | Force reapply all rules |

## Important Notes

### Thread Safety
- State snapshot captured once per frame on main thread
- Commands queued from HTTP thread via `ConcurrentQueue`
- Commands executed on main thread in `_Process()`
- Restart/quit use `CallDeferred` to execute after response

### Security
- Server code wrapped in `#if DEBUG` preprocessor directive
- Binds only to `127.0.0.1` (localhost)
- Step command limited to 100 ticks max

### Logging
All `Log.Print()`, `Log.Error()`, and `Log.Warn()` calls output to:
1. Godot console (GD.Print/GD.PushError/GD.PushWarning)
2. File at `user://logs/game.log` (see path above)

The log file is truncated on each game start and properly closed on quit.

### Restart vs Quit
- `/restart` reloads the scene, resets the tick counter to 0, and regenerates the world (fresh game state)
- `/quit` cleanly shuts down the game, closes log files, and exits the process

## Dependencies

### This module depends on:
- `VeilOfAges.Core.GameController` - Simulation control
- `VeilOfAges.Core.Lib.Log` - Logging with file output
- `VeilOfAges.Core.Lib.JsonOptions` - JSON serialization with Godot types
- `VeilOfAges.Entities` - Being and entity data
- `VeilOfAges.Entities.Beings` - GenericBeing for DefinitionId
- `VeilOfAges.Entities.Items` - Item data for inventory
- `VeilOfAges.Entities.Needs` - Need system for entity needs
- `VeilOfAges.Entities.Skills` - Skill system for entity skills
- `VeilOfAges.Entities.Traits` - Trait system (InventoryTrait for inventory access)
- `System.Net.Sockets` - TCP server
- `System.Collections.Concurrent` - Thread-safe queue
- `System.Text.Json` - JSON serialization

### Depended on by:
- External tools (curl, Claude Code, custom scripts)
- AI-assisted debugging workflows
