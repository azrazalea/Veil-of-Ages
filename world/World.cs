using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.WorldGeneration;

namespace VeilOfAges;

public partial class World : Node2D
{
    // Global world properties
    [Export]
    public int WorldSeed { get; set; }
    [Export]
    public float GlobalTimeScale { get; set; } = 1.0f;
    [Export]
    public bool GenerateOnReady = true;
    public Grid.Area? ActiveGridArea;
    private readonly List<Grid.Area> _gridAreas = [];

    // Entities container
    private Node? _entitiesContainer;
    private GridGenerator? _gridGenerator;
    private SensorySystem? _sensorySystem;
    private EventSystem? _eventSystem;

    // References
    private Player? _player;

    [Export]
    public Vector2I WorldSizeInTiles = new (100, 100);

    public override void _Ready()
    {
        // Get references to nodes
        _entitiesContainer = GetNode<Node>("Entities");
        _gridGenerator = GetNode<GridGenerator>("GridGenerator");
        var gridAreasContainer = GetNode<Node>("GridAreas");

        // Initialize grid system with world bounds
        ActiveGridArea = new Grid.Area(WorldSizeInTiles);
        _gridAreas.Add(ActiveGridArea);
        gridAreasContainer.AddChild(ActiveGridArea);

        // Get reference to the Player scene instance
        _player = GetNode<Player>("Entities/Player");

        _sensorySystem = new SensorySystem(this);
        _eventSystem = new EventSystem();

        // Register the player with the grid system
        if (_player != null)
        {
            var gameController = GetNode<GameController>("GameController");
            _player.Initialize(ActiveGridArea, new Vector2I(50, 50), gameController);
            ActiveGridArea.MakePlayerArea(_player, new Vector2I(50, 50));
        }
        else
        {
            Log.Error("Player node not found! Make sure you've instanced Player.tscn as a child of Entities.");
        }

        if (GenerateOnReady)
        {
            _gridGenerator.CallDeferred(GridGenerator.MethodName.Generate, this);
        }
    }

    public SensorySystem? GetSensorySystem() => _sensorySystem;
    public EventSystem? GetEventSystem() => _eventSystem;

    public void PrepareForTick()
    {
        GetSensorySystem()?.PrepareForTick();

        // Update needs for all beings
        foreach (var being in GetBeings())
        {
            being.NeedsSystem?.UpdateNeeds();
        }
    }

    public List<Being> GetBeings()
    {
        var entities = new List<Being>();
        foreach (Node entity in _entitiesContainer?.GetChildren() ?? [])
        {
            if (entity is Being being)
            {
                entities.Add(being);
            }
        }

        return entities;
    }

    /// <summary>
    /// Process decay for all storage containers in the world.
    /// This includes building storage (StorageTrait) and being inventory (InventoryTrait).
    /// Called periodically (not every tick) for performance.
    /// </summary>
    /// <param name="tickMultiplier">Number of ticks since last decay processing.</param>
    public void ProcessDecay(int tickMultiplier)
    {
        foreach (Node entity in _entitiesContainer?.GetChildren() ?? [])
        {
            if (entity is Building building)
            {
                // Process building storage decay
                building.GetStorage()?.ProcessDecay(tickMultiplier);
            }
            else if (entity is Being being)
            {
                // Process being inventory decay
                being.SelfAsEntity().GetTrait<InventoryTrait>()?.ProcessDecay(tickMultiplier);
            }
        }
    }

    /// <summary>
    /// Process memory cleanup for all entities and villages.
    /// Removes expired personal memories and invalid references from shared knowledge.
    /// Called periodically (not every tick) for performance.
    /// </summary>
    public void ProcessMemoryCleanup()
    {
        // Clean up personal memories for all beings
        foreach (Node entity in _entitiesContainer?.GetChildren() ?? [])
        {
            if (entity is Being being)
            {
                being.Memory?.CleanupExpiredMemories();
            }
            else if (entity is Village village)
            {
                // Clean up invalid building/resident references in villages
                village.CleanupInvalidReferences();
            }
        }
    }
}
