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
- `EntitySnapshot` - Single entity state (position, activity, needs, traits)
- `GridSnapshot` - Grid visualization with `ToAscii()` method
- `BuildingSnapshot` - Building info (name, type, position, size)

### DebugCommand.cs
Command classes for server control:
- `DebugCommand` - Abstract base with `Description` and `Execute(GameController)`
- `PauseCommand` - Pause simulation
- `ResumeCommand` - Resume simulation
- `StepCommand` - Step N ticks (requires paused state)

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

### Write Endpoints (POST)

| Endpoint | Description |
|----------|-------------|
| `/pause` | Pause simulation |
| `/resume` | Resume simulation |
| `/step?ticks=N` | Step N ticks (1-100, default 1, requires paused) |
| `/restart` | Reload current scene (server stays up, tick counter persists) |
| `/quit` | Quit the game cleanly |

### Example Usage

**Use `jq` for parsing JSON responses** - it's more reliable than grep/python for JSON.

```bash
# Check server is running
curl -s http://localhost:8765/ping

# Get game time and tick
curl -s http://localhost:8765/state | jq '{tick: .tick, time: .gameTime}'

# Get all entities with key info (first 15)
curl -s http://localhost:8765/entities | jq -r '.[:15] | .[] | "\(.name[:30]) \(.position) \(.activity[:20]) blk=\(.isBlocked) q=\(.isInQueue)"'

# Get specific entity (URL-encode spaces)
curl -s "http://localhost:8765/entity/Lilith%20Galonadel" | jq .

# Count entities by activity type
curl -s http://localhost:8765/entities | jq 'group_by(.activity) | map({activity: .[0].activity, count: length})'

# Find blocked or queued entities
curl -s http://localhost:8765/entities | jq '.[] | select(.isBlocked or .isInQueue)'

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
```

### ASCII Grid Characters

| Char | Meaning |
|------|---------|
| `.` | Empty/grass |
| `#` | Building |
| `@` | Idle entity |
| `W` | Walking entity |
| `B` | Blocked entity |
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
| `EntitySnapshot` | Entity state (position, activity, needs, traits) |
| `GridSnapshot` | Grid with ASCII rendering |
| `BuildingSnapshot` | Building information |

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
- `VeilOfAges.Entities.Needs` - Need system for entity needs
- `System.Net.Sockets` - TCP server
- `System.Collections.Concurrent` - Thread-safe queue
- `System.Text.Json` - JSON serialization

### Depended on by:
- External tools (curl, Claude Code, custom scripts)
- AI-assisted debugging workflows
