#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Autonomy;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Core.Debug;

/// <summary>
/// HTTP debug server for AI-assisted debugging and game state inspection.
/// Runs as a Godot autoload, capturing state snapshots each frame and processing commands on the main thread.
/// </summary>
public partial class DebugServer : Node
{
    private const int Port = 8765;
    private const int MaxStepTicks = 100;

    private TcpListener? _listener;
    private bool _isRunning;

    // State snapshot captured once per frame on main thread
    private GameStateSnapshot? _currentSnapshot;
    private readonly object _snapshotLock = new ();

    // Commands queued from HTTP thread, executed on main thread
    private readonly ConcurrentQueue<DebugCommand> _commandQueue = new ();

    // Pending step ticks to process
    private int _pendingStepTicks;

    // References cached at ready (re-acquired after scene reload)
    private GameController? _gameController;
    private World? _world;

    public override void _Ready()
    {
        Core.Lib.MemoryProfiler.Checkpoint("DebugServer _Ready start");
        RefreshReferences();
        StartServer();
        Core.Lib.MemoryProfiler.Checkpoint("DebugServer _Ready end");
    }

    private void RefreshReferences()
    {
        _gameController = GetNodeOrNull<GameController>("/root/World/GameController");
        _world = GetTree().GetFirstNodeInGroup("World") as World;
    }

    private bool EnsureValidReferences()
    {
        // Check if references are still valid (they become invalid after scene reload)
        if (_gameController == null || !IsInstanceValid(_gameController) ||
            _world == null || !IsInstanceValid(_world))
        {
            RefreshReferences();
        }

        return _gameController != null && _world != null;
    }

    public override void _ExitTree()
    {
        StopServer();
    }

    private void StartServer()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _isRunning = true;

            // Start accepting connections asynchronously
            _ = AcceptConnectionsAsync();

            Log.Print($"DebugServer: Listening on http://127.0.0.1:{Port}/");
        }
        catch (Exception ex)
        {
            Log.Error($"DebugServer: Failed to start server - {ex.Message}");
        }
    }

    private void StopServer()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener = null;
    }

    public override void _Process(double delta)
    {
        // Capture state snapshot on main thread
        CaptureStateSnapshot();

        // Process pending commands on main thread
        ProcessCommandQueue();

        // Process pending step ticks (fire-and-forget)
        _ = ProcessStepTicks();
    }

    private void CaptureStateSnapshot()
    {
        if (!EnsureValidReferences())
        {
            return;
        }

        var snapshot = new GameStateSnapshot
        {
            Tick = GameController.CurrentTick,
            GameTime = _gameController!.CurrentGameTime.GetTimeDescription(),
            IsPaused = _gameController.SimulationPaused()
        };

        // Capture entities
        var beings = _world!.GetBeings();
        snapshot.EntityCount = beings.Count;

        foreach (var being in beings)
        {
            var entitySnapshot = CreateEntitySnapshot(being);
            snapshot.Entities.Add(entitySnapshot);
        }

        // Capture grid
        snapshot.Grid = CreateGridSnapshot();

        lock (_snapshotLock)
        {
            _currentSnapshot = snapshot;
        }
    }

    private static EntitySnapshot CreateEntitySnapshot(Being being)
    {
        var snapshot = new EntitySnapshot
        {
            Name = being.Name,
            Type = being.GetType().Name,
            Position = being.GetCurrentGridPosition(),
            IsMoving = being.IsMoving(),
            IsInQueue = being.IsInQueue,
            IsHidden = being.IsHidden
        };

        // Definition ID for GenericBeing
        if (being is GenericBeing genericBeing)
        {
            snapshot.DefinitionId = genericBeing.DefinitionId;
        }

        // Activity and command
        var activity = being.GetCurrentActivity();
        if (activity != null)
        {
            snapshot.Activity = activity.GetType().Name;
            snapshot.ActivityDisplayName = activity.DisplayName;
        }

        var command = being.GetCurrentCommand();
        if (command != null)
        {
            snapshot.Command = command.GetType().Name;
        }

        // Attributes
        var attrs = being.Attributes;
        snapshot.Attributes = new AttributeSnapshot
        {
            Strength = attrs.strength,
            Dexterity = attrs.dexterity,
            Constitution = attrs.constitution,
            Intelligence = attrs.intelligence,
            Willpower = attrs.willpower,
            Wisdom = attrs.wisdom,
            Charisma = attrs.charisma
        };

        // Needs
        if (being.NeedsSystem != null)
        {
            foreach (var need in being.NeedsSystem.GetAllNeeds())
            {
                snapshot.Needs[need.Id] = need.Value;
            }
        }

        // Skills
        if (being.SkillSystem != null)
        {
            foreach (var skill in being.SkillSystem.GetAllSkills())
            {
                snapshot.Skills.Add(new SkillSnapshot
                {
                    Id = skill.Definition.Id ?? string.Empty,
                    Name = skill.Definition.Name ?? string.Empty,
                    Level = skill.Level,
                    CurrentXp = skill.CurrentXp,
                    XpToNextLevel = skill.XpToNextLevel,
                    Progress = skill.LevelProgress
                });
            }
        }

        // Health
        snapshot.Health = new HealthSnapshot
        {
            Percentage = being.GetHealthPercentage(),
            Status = being.GetHealthStatus(),
            Efficiency = being.GetEfficiency()
        };

        // Traits
        foreach (var trait in being.Traits)
        {
            snapshot.Traits.Add(trait.GetType().Name);
        }

        // Inventory
        var inventory = being.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            foreach (var item in inventory.GetAllItems())
            {
                snapshot.Inventory.Add(new ItemSnapshot
                {
                    Id = item.Definition.Id ?? string.Empty,
                    Name = item.Definition.Name ?? string.Empty,
                    Quantity = item.Quantity
                });
            }
        }

        // Village
        if (being.Village != null)
        {
            snapshot.Village = being.Village.VillageName;
        }

        // Autonomy rules (player only)
        if (being is Player player)
        {
            snapshot.AutonomyRules = [];
            foreach (var rule in player.AutonomyConfig.Rules)
            {
                var ruleSnapshot = new AutonomyRuleSnapshot
                {
                    Id = rule.Id,
                    DisplayName = rule.DisplayName,
                    TraitType = rule.TraitType,
                    Priority = rule.Priority,
                    Enabled = rule.Enabled,
                    ActiveDuringPhases = rule.ActiveDuringPhases?.Select(p => p.ToString()).ToArray(),
                    Parameters = rule.Parameters.Count > 0 ? rule.Parameters : null
                };
                snapshot.AutonomyRules.Add(ruleSnapshot);
            }
        }

        return snapshot;
    }

    private GridSnapshot? CreateGridSnapshot()
    {
        var gridArea = _world?.ActiveGridArea;
        if (gridArea == null)
        {
            return null;
        }

        var snapshot = new GridSnapshot
        {
            Width = _world!.WorldSizeInTiles.X,
            Height = _world.WorldSizeInTiles.Y
        };

        // Capture buildings (they're children of the GridArea, not Entities)
        foreach (Node child in gridArea.GetChildren())
        {
            if (child is Building building)
            {
                var buildingSnapshot = new BuildingSnapshot
                {
                    Name = building.Name,
                    Type = building.BuildingType,
                    Position = building.GetCurrentGridPosition(),
                    Size = building.GridSize
                };

                // Populate rooms and their facilities
                foreach (var room in building.Rooms)
                {
                    var roomSnapshot = new RoomSnapshot
                    {
                        Id = room.Id,
                        Name = room.Name,
                        Purpose = room.Purpose,
                        IsSecret = room.IsSecret,
                        ResidentCount = room.Residents.Count
                    };

                    foreach (var facility in room.Facilities)
                    {
                        var facilitySnapshot = new FacilitySnapshot
                        {
                            Id = facility.Id,
                            Position = facility.GetCurrentGridPosition(),
                            IsWalkable = facility.IsWalkable
                        };

                        var storageTrait = facility.SelfAsEntity().GetTrait<StorageTrait>();
                        if (storageTrait != null)
                        {
                            facilitySnapshot.HasStorage = true;
                            facilitySnapshot.StorageContents = [];
                            foreach (var item in storageTrait.GetAllItems())
                            {
                                facilitySnapshot.StorageContents.Add(new ItemSnapshot
                                {
                                    Id = item.Definition.Id ?? string.Empty,
                                    Name = item.Definition.Name ?? string.Empty,
                                    Quantity = item.Quantity
                                });
                            }
                        }

                        roomSnapshot.Facilities.Add(facilitySnapshot);
                    }

                    buildingSnapshot.Rooms.Add(roomSnapshot);
                }

                snapshot.Buildings.Add(buildingSnapshot);
            }
        }

        return snapshot;
    }

    private void ProcessCommandQueue()
    {
        while (_commandQueue.TryDequeue(out var command))
        {
            if (_gameController != null)
            {
                bool success = command.Execute(_gameController);
                if (success && command is StepCommand stepCmd)
                {
                    _pendingStepTicks = Math.Min(stepCmd.Ticks, MaxStepTicks);
                }
            }
        }
    }

    private async Task ProcessStepTicks()
    {
        if (_pendingStepTicks <= 0 || _gameController == null)
        {
            return;
        }

        // Process one tick per frame while stepping
        _pendingStepTicks--;
        await ProcessSingleTickAsync();
    }

    private async Task ProcessSingleTickAsync()
    {
        if (!EnsureValidReferences())
        {
            return;
        }

        // Increment tick and process
        var thinkingSystem = _world!.GetNodeOrNull<EntityThinkingSystem>("EntityThinkingSystem");
        if (thinkingSystem != null)
        {
            await thinkingSystem.ProcessGameTick();
        }
    }

    private async Task AcceptConnectionsAsync()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
            catch (ObjectDisposedException)
            {
                // Server stopped
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Log.Error($"DebugServer: Error accepting connection - {ex.Message}");
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using var reader = new StreamReader(stream, utf8NoBom);
            using var writer = new StreamWriter(stream, utf8NoBom) { AutoFlush = true };

            // Read HTTP request line
            var requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine))
            {
                return;
            }

            // Parse request: "GET /path HTTP/1.1"
            var parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                await SendResponse(writer, 400, "text/plain", "Bad Request");
                return;
            }

            var method = parts[0];
            var path = parts[1];

            // Read headers (we don't need them, but must consume them)
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                // Skip headers
            }

            // Route the request
            await RouteRequest(writer, method, path);
        }
        catch (Exception ex)
        {
            Log.Error($"DebugServer: Error handling client - {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private async Task RouteRequest(StreamWriter writer, string method, string path)
    {
        // Parse path and query string
        var queryIndex = path.IndexOf('?');
        var pathOnly = queryIndex >= 0 ? path[..queryIndex] : path;
        var queryString = queryIndex >= 0 ? path[(queryIndex + 1) ..] : string.Empty;
        var queryParams = ParseQueryString(queryString);

        // Route based on method and path
        if (method == "GET")
        {
            switch (pathOnly)
            {
                case "/ping":
                    await HandlePing(writer);
                    break;
                case "/state":
                    await HandleState(writer);
                    break;
                case "/entities":
                    await HandleEntities(writer);
                    break;
                case "/grid":
                    await HandleGrid(writer);
                    break;
                case "/events":
                    await HandleEvents(writer, queryParams);
                    break;
                case "/player/autonomy":
                    await HandleGetAutonomy(writer);
                    break;
                default:
                    // Check for /entity/{name} pattern
                    if (pathOnly.StartsWith("/entity/", StringComparison.Ordinal))
                    {
                        var entityName = Uri.UnescapeDataString(pathOnly["/entity/".Length..]);
                        await HandleEntity(writer, entityName);
                    }
                    else
                    {
                        await SendResponse(writer, 404, "text/plain", "Not Found");
                    }

                    break;
            }
        }
        else if (method == "POST")
        {
            switch (pathOnly)
            {
                case "/pause":
                    await HandlePause(writer);
                    break;
                case "/resume":
                    await HandleResume(writer);
                    break;
                case "/step":
                    await HandleStep(writer, queryParams);
                    break;
                case "/restart":
                    await HandleRestart(writer);
                    break;
                case "/quit":
                    await HandleQuit(writer);
                    break;
                default:
                    if (pathOnly.StartsWith("/player/", StringComparison.Ordinal))
                    {
                        await HandlePlayerCommand(writer, pathOnly, queryParams);
                    }
                    else
                    {
                        await SendResponse(writer, 404, "text/plain", "Not Found");
                    }

                    break;
            }
        }
        else
        {
            await SendResponse(writer, 405, "text/plain", "Method Not Allowed");
        }
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(queryString))
        {
            return result;
        }

        foreach (var pair in queryString.Split('&'))
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length == 2)
            {
                result[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
            }
            else if (keyValue.Length == 1)
            {
                result[Uri.UnescapeDataString(keyValue[0])] = string.Empty;
            }
        }

        return result;
    }

    // GET /ping
    private static Task HandlePing(StreamWriter writer)
    {
        return SendJsonResponse(writer, 200, new { pong = true });
    }

    // GET /state
    private async Task HandleState(StreamWriter writer)
    {
        GameStateSnapshot? snapshot;
        lock (_snapshotLock)
        {
            snapshot = _currentSnapshot;
        }

        if (snapshot == null)
        {
            await SendResponse(writer, 503, "text/plain", "State not available yet");
            return;
        }

        await SendJsonResponse(writer, 200, snapshot);
    }

    // GET /entities
    private async Task HandleEntities(StreamWriter writer)
    {
        GameStateSnapshot? snapshot;
        lock (_snapshotLock)
        {
            snapshot = _currentSnapshot;
        }

        if (snapshot == null)
        {
            await SendResponse(writer, 503, "text/plain", "State not available yet");
            return;
        }

        await SendJsonResponse(writer, 200, snapshot.Entities);
    }

    // GET /entity/{name}
    private async Task HandleEntity(StreamWriter writer, string name)
    {
        GameStateSnapshot? snapshot;
        lock (_snapshotLock)
        {
            snapshot = _currentSnapshot;
        }

        if (snapshot == null)
        {
            await SendResponse(writer, 503, "text/plain", "State not available yet");
            return;
        }

        var entity = snapshot.Entities.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
        {
            await SendResponse(writer, 404, "text/plain", $"Entity '{name}' not found");
            return;
        }

        await SendJsonResponse(writer, 200, entity);
    }

    // GET /grid
    private async Task HandleGrid(StreamWriter writer)
    {
        GameStateSnapshot? snapshot;
        lock (_snapshotLock)
        {
            snapshot = _currentSnapshot;
        }

        if (snapshot?.Grid == null)
        {
            await SendResponse(writer, 503, "text/plain", "Grid not available yet");
            return;
        }

        var ascii = snapshot.Grid.ToAscii(snapshot.Entities);
        await SendResponse(writer, 200, "text/plain", ascii);
    }

    // GET /events?count=N
    private static Task HandleEvents(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        // Events not yet implemented - return empty array
        return SendJsonResponse(writer, 200, Array.Empty<object>());
    }

    // POST /pause
    private Task HandlePause(StreamWriter writer)
    {
        _commandQueue.Enqueue(new PauseCommand());
        return SendJsonResponse(writer, 200, new { success = true, message = "Pause command queued" });
    }

    // POST /resume
    private Task HandleResume(StreamWriter writer)
    {
        _commandQueue.Enqueue(new ResumeCommand());
        return SendJsonResponse(writer, 200, new { success = true, message = "Resume command queued" });
    }

    // POST /step?ticks=N
    private async Task HandleStep(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        int ticks = 1;
        if (queryParams.TryGetValue("ticks", out var ticksStr) && int.TryParse(ticksStr, out var parsedTicks))
        {
            ticks = Math.Clamp(parsedTicks, 1, MaxStepTicks);
        }

        // Check if paused
        if (_gameController != null && !_gameController.SimulationPaused())
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Simulation must be paused to step" });
            return;
        }

        _commandQueue.Enqueue(new StepCommand(ticks));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Step {ticks} tick(s) queued" });
    }

    // POST /restart
    private async Task HandleRestart(StreamWriter writer)
    {
        Log.Print("DebugServer: Restart requested, reloading scene...");
        await SendJsonResponse(writer, 200, new { success = true, message = "Restarting scene..." });

        // Use CallDeferred to reload after response is sent
        GetTree().CallDeferred("reload_current_scene");
    }

    // POST /quit
    private async Task HandleQuit(StreamWriter writer)
    {
        Log.Print("DebugServer: Quit requested, shutting down...");
        await SendJsonResponse(writer, 200, new { success = true, message = "Quitting game..." });

        // Shutdown logging and quit
        Log.Shutdown();
        GetTree().CallDeferred("quit");
    }

    // GET /player/autonomy
    private async Task HandleGetAutonomy(StreamWriter writer)
    {
        GameStateSnapshot? snapshot;
        lock (_snapshotLock)
        {
            snapshot = _currentSnapshot;
        }

        if (snapshot == null)
        {
            await SendResponse(writer, 503, "text/plain", "State not available yet");
            return;
        }

        var playerEntity = snapshot.Entities.FirstOrDefault(e => e.Type == "Player");
        if (playerEntity?.AutonomyRules == null)
        {
            await SendResponse(writer, 404, "text/plain", "Player not found or has no autonomy rules");
            return;
        }

        await SendJsonResponse(writer, 200, playerEntity.AutonomyRules);
    }

    // POST /player/* dispatch
    private async Task HandlePlayerCommand(StreamWriter writer, string pathOnly, Dictionary<string, string> queryParams)
    {
        switch (pathOnly)
        {
            case "/player/move":
                await HandlePlayerMove(writer, queryParams);
                break;
            case "/player/follow":
                await HandlePlayerFollow(writer, queryParams);
                break;
            case "/player/cancel":
                await HandlePlayerCancel(writer);
                break;
            case "/player/autonomy/enable":
                await HandleAutonomySetEnabled(writer, queryParams, true);
                break;
            case "/player/autonomy/disable":
                await HandleAutonomySetEnabled(writer, queryParams, false);
                break;
            case "/player/autonomy/reorder":
                await HandleAutonomyReorder(writer, queryParams);
                break;
            case "/player/autonomy/add":
                await HandleAutonomyAdd(writer, queryParams);
                break;
            case "/player/autonomy/remove":
                await HandleAutonomyRemove(writer, queryParams);
                break;
            case "/player/autonomy/reapply":
                await HandleAutonomyReapply(writer);
                break;
            case "/player/use_transition":
                await HandlePlayerUseTransition(writer);
                break;
            default:
                await SendResponse(writer, 404, "text/plain", "Not Found");
                break;
        }
    }

    // POST /player/move?x=N&y=N
    private async Task HandlePlayerMove(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        if (!queryParams.TryGetValue("x", out var xStr) || !int.TryParse(xStr, out var x) ||
            !queryParams.TryGetValue("y", out var yStr) || !int.TryParse(yStr, out var y))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing or invalid 'x' and 'y' parameters" });
            return;
        }

        _commandQueue.Enqueue(new PlayerMoveToCommand(x, y));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Move to ({x}, {y}) queued" });
    }

    // POST /player/follow?entity=NAME
    private async Task HandlePlayerFollow(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        if (!queryParams.TryGetValue("entity", out var entityName) || string.IsNullOrEmpty(entityName))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'entity' parameter" });
            return;
        }

        _commandQueue.Enqueue(new PlayerFollowCommand(entityName));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Follow '{entityName}' queued" });
    }

    // POST /player/cancel
    private Task HandlePlayerCancel(StreamWriter writer)
    {
        _commandQueue.Enqueue(new PlayerCancelCommand());
        return SendJsonResponse(writer, 200, new { success = true, message = "Cancel command queued" });
    }

    // POST /player/autonomy/enable or /player/autonomy/disable
    private async Task HandleAutonomySetEnabled(StreamWriter writer, Dictionary<string, string> queryParams, bool enabled)
    {
        if (!queryParams.TryGetValue("rule", out var ruleId) || string.IsNullOrEmpty(ruleId))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'rule' parameter" });
            return;
        }

        _commandQueue.Enqueue(new AutonomySetEnabledCommand(ruleId, enabled));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Rule '{ruleId}' {(enabled ? "enable" : "disable")} queued" });
    }

    // POST /player/autonomy/reorder?rule=ID&priority=N
    private async Task HandleAutonomyReorder(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        if (!queryParams.TryGetValue("rule", out var ruleId) || string.IsNullOrEmpty(ruleId))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'rule' parameter" });
            return;
        }

        if (!queryParams.TryGetValue("priority", out var priorityStr) || !int.TryParse(priorityStr, out var priority))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing or invalid 'priority' parameter" });
            return;
        }

        _commandQueue.Enqueue(new AutonomyReorderCommand(ruleId, priority));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Reorder rule '{ruleId}' to priority {priority} queued" });
    }

    // POST /player/autonomy/add?id=ID&name=NAME&trait=TYPE&priority=N&phases=Dawn,Day
    private async Task HandleAutonomyAdd(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        if (!queryParams.TryGetValue("id", out var id) || string.IsNullOrEmpty(id))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'id' parameter" });
            return;
        }

        if (!queryParams.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'name' parameter" });
            return;
        }

        if (!queryParams.TryGetValue("trait", out var traitType) || string.IsNullOrEmpty(traitType))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'trait' parameter" });
            return;
        }

        if (!queryParams.TryGetValue("priority", out var priorityStr) || !int.TryParse(priorityStr, out var priority))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing or invalid 'priority' parameter" });
            return;
        }

        DayPhaseType[] ? phases = null;
        if (queryParams.TryGetValue("phases", out var phasesStr) && !string.IsNullOrEmpty(phasesStr))
        {
            var phaseNames = phasesStr.Split(',');
            var parsed = new List<DayPhaseType>();
            foreach (var phaseName in phaseNames)
            {
                if (Enum.TryParse<DayPhaseType>(phaseName.Trim(), ignoreCase: true, out var phase))
                {
                    parsed.Add(phase);
                }
                else
                {
                    await SendJsonResponse(writer, 400, new { success = false, message = $"Invalid phase '{phaseName.Trim()}'. Valid: Dawn, Day, Dusk, Night" });
                    return;
                }
            }

            phases = parsed.ToArray();
        }

        Dictionary<string, object?>? parameters = null;
        if (queryParams.TryGetValue("params", out var paramsStr) && !string.IsNullOrEmpty(paramsStr))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(paramsStr, JsonOptions.Default);
            }
            catch (JsonException)
            {
                await SendJsonResponse(writer, 400, new { success = false, message = "Invalid JSON in 'params' parameter" });
                return;
            }
        }

        _commandQueue.Enqueue(new AutonomyAddRuleCommand(id, name, traitType, priority, phases, parameters));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Add rule '{id}' queued" });
    }

    // POST /player/autonomy/remove?rule=ID
    private async Task HandleAutonomyRemove(StreamWriter writer, Dictionary<string, string> queryParams)
    {
        if (!queryParams.TryGetValue("rule", out var ruleId) || string.IsNullOrEmpty(ruleId))
        {
            await SendJsonResponse(writer, 400, new { success = false, message = "Missing 'rule' parameter" });
            return;
        }

        _commandQueue.Enqueue(new AutonomyRemoveRuleCommand(ruleId));
        await SendJsonResponse(writer, 200, new { success = true, message = $"Remove rule '{ruleId}' queued" });
    }

    // POST /player/autonomy/reapply
    private Task HandleAutonomyReapply(StreamWriter writer)
    {
        _commandQueue.Enqueue(new AutonomyReapplyCommand());
        return SendJsonResponse(writer, 200, new { success = true, message = "Reapply autonomy queued" });
    }

    // POST /player/use_transition
    private Task HandlePlayerUseTransition(StreamWriter writer)
    {
        _commandQueue.Enqueue(new PlayerUseTransitionCommand());
        return SendJsonResponse(writer, 200, new { success = true, message = "Use transition queued" });
    }

    private static Task SendResponse(StreamWriter writer, int statusCode, string contentType, string body)
    {
        var statusText = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            _ => "Unknown"
        };

        var response = new StringBuilder();
        response.AppendLine(CultureInfo.InvariantCulture, $"HTTP/1.1 {statusCode} {statusText}");
        response.AppendLine(CultureInfo.InvariantCulture, $"Content-Type: {contentType}");
        response.AppendLine(CultureInfo.InvariantCulture, $"Content-Length: {Encoding.UTF8.GetByteCount(body)}");
        response.AppendLine("Connection: close");
        response.AppendLine("Access-Control-Allow-Origin: *");
        response.AppendLine();
        response.Append(body);

        return writer.WriteAsync(response.ToString());
    }

    private static Task SendJsonResponse(StreamWriter writer, int statusCode, object data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions.WithGodotTypes);
        return SendResponse(writer, statusCode, "application/json", json);
    }
}
#else
namespace VeilOfAges.Core.Debug;

/// <summary>
/// Stub class for non-DEBUG builds. Does nothing.
/// </summary>
public partial class DebugServer : Godot.Node
{
    // Empty stub - debug server only runs in DEBUG builds
}
#endif
