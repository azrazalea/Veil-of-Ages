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

        if (_player == null)
        {
            Log.Error("Player node not found! Make sure you've instanced Player.tscn as a child of Entities.");
        }

        if (GenerateOnReady)
        {
            // Generate terrain and buildings first, then initialize player
            // This prevents the player from blocking building placement
            _gridGenerator.CallDeferred(GridGenerator.MethodName.Generate, this);
            CallDeferred(MethodName.InitializePlayerAfterGeneration);
        }
        else
        {
            // If not generating, initialize player immediately
            InitializePlayer();
        }
    }

    /// <summary>
    /// Initialize the player entity and register with the grid system.
    /// Called after world generation to prevent blocking building placement.
    /// </summary>
    private void InitializePlayer()
    {
        if (_player == null || ActiveGridArea == null)
        {
            return;
        }

        var gameController = GetNode<GameController>("GameController");
        var requestedPosition = new Vector2I(50, 50);

        // Find a walkable position, starting from the requested position
        var playerPosition = FindNearestWalkablePosition(requestedPosition);

        _player.Initialize(ActiveGridArea, playerPosition, gameController);
        ActiveGridArea.MakePlayerArea(_player, playerPosition);
    }

    /// <summary>
    /// Find the nearest walkable tile to the requested position.
    /// If the requested position is walkable, return it.
    /// Otherwise, search expanding outward in a square pattern until a walkable tile is found.
    /// </summary>
    /// <param name="requestedPosition">The desired spawn position.</param>
    /// <returns>A walkable position at or near the requested position.</returns>
    private Vector2I FindNearestWalkablePosition(Vector2I requestedPosition)
    {
        if (ActiveGridArea == null)
        {
            return requestedPosition;
        }

        // Check if the requested position is walkable
        if (ActiveGridArea.IsCellWalkable(requestedPosition))
        {
            return requestedPosition;
        }

        // Search expanding outward in concentric squares
        int searchRadius = 1;
        int maxSearchRadius = 50; // Prevent infinite loop

        while (searchRadius <= maxSearchRadius)
        {
            // Check all cells in the current square ring
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    // Only check cells on the perimeter of the current radius
                    if (Mathf.Abs(dx) != searchRadius && Mathf.Abs(dy) != searchRadius)
                    {
                        continue;
                    }

                    var candidatePosition = new Vector2I(
                        requestedPosition.X + dx,
                        requestedPosition.Y + dy);

                    // Check bounds
                    if (candidatePosition.X < 0 || candidatePosition.Y < 0 ||
                        candidatePosition.X >= WorldSizeInTiles.X || candidatePosition.Y >= WorldSizeInTiles.Y)
                    {
                        continue;
                    }

                    // Check if walkable
                    if (ActiveGridArea.IsCellWalkable(candidatePosition))
                    {
                        return candidatePosition;
                    }
                }
            }

            searchRadius++;
        }

        // Fallback: return requested position if no walkable position found
        Log.Warn($"No walkable position found near {requestedPosition}. Using fallback position.");
        return requestedPosition;
    }

    /// <summary>
    /// Deferred player initialization that runs after world generation.
    /// This ensures the player doesn't block building placement during generation.
    /// Note: Player home is assigned during village generation (VillageGenerator.PlacePlayerHouseNearGraveyard)
    /// so that the player is registered as a resident before granary standing orders are initialized.
    /// </summary>
    private void InitializePlayerAfterGeneration()
    {
        InitializePlayer();
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
    /// Process decay and regeneration for all storage containers in the world.
    /// This includes building storage (StorageTrait) and being inventory (InventoryTrait).
    /// Buildings with regeneration configured (e.g., wells) will also regenerate their items.
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

                // Process building regeneration (e.g., wells regenerating water)
                building.ProcessRegeneration(tickMultiplier);
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
