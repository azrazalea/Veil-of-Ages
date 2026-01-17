using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Reactions;

namespace VeilOfAges.Core;

public partial class GameController : Node
{
    [Export]
    public float TimeScale { get; set; } = 1.0f; // Can be adjusted for speed
    [Export]
    public uint MaxPlayerActions { get; set; } = 3;

    /// <summary>
    /// How often to process item decay (every N ticks). Decay rates are multiplied by this value.
    /// </summary>
    private const int DECAYTICKINTERVAL = 32;

    /// <summary>
    /// Gets global tick counter, incremented each simulation tick.
    /// </summary>
    public static uint CurrentTick { get; private set; }

    private static float TickInterval => 1f / GameTime.SimulationTickRate;
    private float _timeSinceLastTick;
    private bool _simulationPaused;
    private bool _processingTick;

    private World? _world;
    private Player? _player;
    private EntityThinkingSystem? _thinkingSystem;

    // Start time at exactly 100 years from beginning of calendar
    public GameTime CurrentGameTime { get; private set; } = new GameTime(158_212_454_400UL);

    public override void _Ready()
    {
        // Initialize resource managers (centralized initialization)
        TileResourceManager.Instance.Initialize();
        ItemResourceManager.Instance.Initialize();
        ReactionResourceManager.Instance.Initialize();

        _world = GetTree().GetFirstNodeInGroup("World") as World;
        _player = GetNode<Player>("/root/World/Entities/Player");
        _thinkingSystem = GetNode<EntityThinkingSystem>("/root/World/EntityThinkingSystem");
        GD.Print($"Current time: {CurrentGameTime.GetTimeDescription()}");
        GD.Print($"Actual time we want: {CurrentGameTime.Value - GameTime.CENTISECONDSPERYEAR}");
        if (_world == null || _player == null || _thinkingSystem == null)
        {
            Log.Error("GameController: Failed to find required nodes!");
        }
    }

    public override void _Process(double delta)
    {
        if (_simulationPaused || _processingTick)
        {
            return;
        }

        _timeSinceLastTick += (float)delta * TimeScale;

        if (_timeSinceLastTick >= TickInterval)
        {
            _timeSinceLastTick -= TickInterval;
            CurrentTick++;
            _ = ProcessNextTick();
            CurrentGameTime = CurrentGameTime.Advance();
        }
    }

    private async Task ProcessNextTick()
    {
        _processingTick = true;

        // Process decay for all storage containers (buildings and beings)
        // Only runs every DECAY_TICK_INTERVAL ticks for performance
        if (CurrentTick % DECAYTICKINTERVAL == 0)
        {
            _world?.ProcessDecay(DECAYTICKINTERVAL);
        }

        // Process the tick with all entities (including player)
        if (_thinkingSystem != null)
        {
            await _thinkingSystem.ProcessGameTick();
        }

        // Update world state, UI, and advance time
        // _world.UpdateWorldState();
        // _world.UpdateUI();
        // _world.AdvanceTime();
        _processingTick = false;
    }

    // Simulation control
    public void PauseSimulation()
    {
        _simulationPaused = true;
    }

    public void ResumeSimulation()
    {
        _simulationPaused = false;
    }

    public void ToggleSimulationPause()
    {
        _simulationPaused = !_simulationPaused;
    }

    public bool SimulationPaused()
    {
        return _simulationPaused;
    }

    public void SetTimeScale(float scale)
    {
        TimeScale = Mathf.Clamp(scale, 0.1f, 25f);
    }
}
