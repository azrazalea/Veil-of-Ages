using System.Collections.Generic;
using System.Linq;
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

    // Transition registry: maps (area, position) to TransitionPoint
    private readonly Dictionary<(Grid.Area, Vector2I), TransitionPoint> _transitionRegistry = new ();

    // References
    private Player? _player;

    [Export]
    public Vector2I WorldSizeInTiles = new (200, 200);

    public override void _Ready()
    {
        Core.Lib.MemoryProfiler.Checkpoint("World _Ready start");

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

        Core.Lib.MemoryProfiler.Checkpoint("World _Ready after GridArea creation");

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

        Core.Lib.MemoryProfiler.Checkpoint("World InitializePlayer start");
        var gameController = GetNode<GameController>("GameController");
        var requestedPosition = new Vector2I(WorldSizeInTiles.X / 2, WorldSizeInTiles.Y / 2);

        // Find a walkable position, starting from the requested position
        var playerPosition = FindNearestWalkablePosition(requestedPosition);

        _player.Initialize(ActiveGridArea, playerPosition, gameController);
        ActiveGridArea.MakePlayerArea(_player, playerPosition);
        Core.Lib.MemoryProfiler.Checkpoint("World InitializePlayer end (after MakePlayerArea)");
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
    /// This ensures the player doesn't block building placement during generation,
    /// and that player traits are loaded (via Initialize) before SetHome is called.
    /// Player home is set here (not in VillageGenerator) because traits aren't ready
    /// until after Initialize() runs.
    /// </summary>
    private void InitializePlayerAfterGeneration()
    {
        InitializePlayer();

        // Assign player home after traits are initialized.
        // VillageGenerator already registered the player as a village resident so
        // GranaryTrait can detect them via HasScholarResident(); now we need to
        // actually set the home room so HomeTrait and ScholarJobTrait know where home is.
        if (_player != null && _gridGenerator != null)
        {
            var playerHouseRoom = _gridGenerator.PlayerHouseRoom;
            if (playerHouseRoom != null)
            {
                _player.SetHome(playerHouseRoom);
                Log.Print($"World: Assigned player home to {playerHouseRoom.Name}");
            }
            else
            {
                Log.Warn("World: No player house room found after generation — player has no home");
            }
        }
    }

    public Player? Player => _player;

    public SensorySystem? GetSensorySystem() => _sensorySystem;
    public EventSystem? GetEventSystem() => _eventSystem;

    /// <summary>
    /// Register a transition point in the global lookup.
    /// </summary>
    public void RegisterTransitionPoint(TransitionPoint point)
        => _transitionRegistry[(point.SourceArea, point.SourcePosition)] = point;

    /// <summary>
    /// Get a transition point at a specific area and position.
    /// </summary>
    public TransitionPoint? GetTransitionPointAt(Grid.Area area, Vector2I position)
        => _transitionRegistry.GetValueOrDefault((area, position));

    /// <summary>
    /// Register a grid area with the world (for areas created after initialization).
    /// </summary>
    public void RegisterGridArea(Grid.Area area)
    {
        if (!_gridAreas.Contains(area))
        {
            _gridAreas.Add(area);
        }
    }

    /// <summary>
    /// Transition an entity from its current area to a destination transition point.
    /// Must be called on the main thread.
    /// </summary>
    public void TransitionEntity(Being entity, TransitionPoint destination)
    {
        var oldArea = entity.GridArea;
        var newArea = destination.SourceArea;
        var destPos = destination.SourcePosition;

        if (oldArea == null || newArea == oldArea)
        {
            Log.Warn("TransitionEntity: Invalid transition (null area or same area)");
            return;
        }

        Log.Print($"TransitionEntity: {entity.Name} from {oldArea.AreaName} to {newArea.AreaName} at {destPos}");

        // 1. Remove entity from old area's grid
        oldArea.RemoveEntity(entity.GetCurrentGridPosition());

        // 2. Update entity's area reference
        entity.SetGridArea(newArea);

        // 3. Set entity's position in new area
        entity.SetGridPosition(destPos);

        // 4. Add entity to new area's grid
        newArea.AddEntity(destPos, entity);

        // 5. If this is the player, switch rendering
        if (entity is Player player)
        {
            ActiveGridArea = newArea;
            newArea.MakePlayerArea(player, destPos);

            // Toggle entity visibility based on which area the player is in
            UpdateEntityVisibility(newArea);
        }
    }

    /// <summary>
    /// Update visibility of all entities based on the player's current area.
    /// Entities not in the active area are hidden; entities in the active area are shown.
    /// </summary>
    private void UpdateEntityVisibility(Grid.Area playerArea)
    {
        foreach (var being in GetBeings())
        {
            // Don't change visibility of hidden entities (they handle their own state)
            if (being.IsHidden)
            {
                continue;
            }

            being.Visible = being.GridArea == playerArea;
        }
    }

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
    /// This includes facility storage (StorageTrait) in village rooms and being inventory (InventoryTrait).
    /// Facilities with RegenerationTrait (e.g., wells) handle their own regeneration.
    /// Called periodically (not every tick) for performance.
    /// </summary>
    /// <param name="tickMultiplier">Number of ticks since last decay processing.</param>
    public void ProcessDecay(int tickMultiplier)
    {
        // Process storage decay for all rooms in all villages
        foreach (Node entity in _entitiesContainer?.GetChildren() ?? [])
        {
            if (entity is Village village)
            {
                // Process storage decay for all rooms in the village
                foreach (var room in village.Rooms)
                {
                    room.GetStorage()?.ProcessDecay(tickMultiplier);
                }
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
