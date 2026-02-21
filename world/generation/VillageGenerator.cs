using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.WorldGeneration;

namespace VeilOfAges.WorldGeneration;

public class VillageGenerator
{
    // Buildings are instantiated via BuildingManager
    // Entities are spawned via GenericBeing.CreateFromDefinition()
    private readonly Node _entitiesContainer;
    private readonly Area _gridArea;
    private readonly RandomNumberGenerator _rng = new ();
    private readonly BuildingManager? _buildingManager;
    private readonly EntityThinkingSystem _entityThinkingSystem;
    private readonly GameController? _gameController;

    // Track placed farms for assigning farmers (stores default room)
    private readonly List<Room> _placedFarms = [];

    // Track placed houses for assigning villagers (stores default room)
    private readonly List<Room> _placedHouses = [];

    // Track placed granary for distributor assignment (stores StampResult for GranaryTrait)
    private StampResult? _placedGranaryResult;

    // Track placed graveyard for player house placement (stores default room)
    private Room? _placedGraveyard;

    // Track player's house (stores default room)
    private Room? _playerHouseRoom;

    // Temporary: last StampResult from PlaceBuildingInLot, used to capture PlayerHouseStampResult
    private StampResult? _lastStampResult;

    /// <summary>
    /// Gets the player's house room, placed near the graveyard during generation.
    /// </summary>
    public Room? PlayerHouseRoom => _playerHouseRoom;

    /// <summary>
    /// Gets the player's house StampResult, needed for CellarGenerator.
    /// </summary>
    public StampResult? PlayerHouseStampResult { get; private set; }

    // Track spawned villagers for debug selection
    private readonly List<Being> _spawnedVillagers = [];

    // Debug villager selection - enable debug on first villager of each job type
    private readonly HashSet<string> _debugEnabledJobTypes = [];

    // The village being generated (tracks buildings and residents)
    private Village? _currentVillage;

    // Road network for lot-based building placement
    private RoadNetwork? _roadNetwork;

    // Reference to the player for home assignment
    private readonly Player? _player;

    public VillageGenerator(
        Area gridArea,
        Node entitiesContainer,
        EntityThinkingSystem entityThinkingSystem,
        Player? player = null,
        int? seed = null)
    {
        _gridArea = gridArea;
        _entitiesContainer = entitiesContainer;
        _entityThinkingSystem = entityThinkingSystem;
        _player = player;
        _buildingManager = BuildingManager.Instance; // Get the singleton instance

        // Look up GameController from the scene tree
        _gameController = entitiesContainer.GetNode<GameController>("/root/World/GameController");
        if (_gameController == null)
        {
            Log.Warn("VillageGenerator: Could not find GameController at /root/World/GameController");
        }

        Log.Print($"BuildingManager instance: {_buildingManager}");

        // Set the current area for BuildingManager
        _buildingManager?.SetCurrentArea(gridArea);

        // Initialize RNG with seed if provided
        if (seed.HasValue)
        {
            _rng.Seed = (ulong)seed.Value;
        }
        else
        {
            _rng.Randomize();
        }
    }

    /// <summary>
    /// Generate a village centered at the given position.
    /// </summary>
    /// <returns>The generated Village with all buildings and residents registered.</returns>
    public Village GenerateVillage(Vector2I villageCenter = default)
    {
        // Default to center of map if no position specified
        if (villageCenter == default)
        {
            villageCenter = new Vector2I(
                _gridArea.GridSize.X / 2,
                _gridArea.GridSize.Y / 2);
        }

        // Create and initialize the village
        _currentVillage = new Village();
        _currentVillage.Initialize("Main Village", villageCenter);
        _entitiesContainer.AddChild(_currentVillage);

        // Calculate optimal lot size from building templates
        int optimalLotSize = RoadNetwork.CalculateOptimalLotSize(_buildingManager);
        Log.Print($"VillageGenerator: Calculated optimal lot size: {optimalLotSize}");

        // Generate road network with lots
        _roadNetwork = new RoadNetwork(
            villageCenter,
            villageSquareRadius: 5,  // 11x11 central square
            roadWidth: 3,
            lotSize: optimalLotSize,
            lotsPerSide: 4);           // More lots for bigger village
        _roadNetwork.GenerateLayout();

        // Place village square and roads as dirt tiles
        PlaceVillageSquare(villageCenter);
        PlaceRoads();

        // Reset debug selection state
        _debugEnabledJobTypes.Clear();

        // Place the well in the center of the village square FIRST (before other buildings)
        PlaceWellInVillageCenter(villageCenter);

        // Place buildings using lot system
        PlaceBuildingsInLots();

        // Log which villager was selected for debug
        LogDebugVillagerSelection();

        Log.Print($"Village '{_currentVillage.VillageName}' created with {_currentVillage.Rooms.Count} rooms and {_currentVillage.Residents.Count} residents");

        return _currentVillage;
    }

    /// <summary>
    /// Logs which villager was selected for debug mode.
    /// Debug mode is enabled during initialization, this just confirms the selection.
    /// </summary>
    private void LogDebugVillagerSelection()
    {
        // Find the debug-enabled villager
        foreach (var villager in _spawnedVillagers)
        {
            if (villager.DebugEnabled)
            {
                Log.Print($"DEBUG MODE ENABLED for villager: {villager.Name}");
                return;
            }
        }

        Log.Warn("No villagers spawned with debug mode enabled");
    }

    /// <summary>
    /// Creates a central village square with dirt ground.
    /// Uses the road network's village square tiles if available.
    /// </summary>
    private void PlaceVillageSquare(Vector2I center)
    {
        if (_roadNetwork != null)
        {
            foreach (var pos in _roadNetwork.GetVillageSquareTiles())
            {
                if (IsPositionInWorldBounds(pos))
                {
                    _gridArea.SetGroundCell(pos, Area.PathTile);
                }
            }
        }
        else
        {
            // Fallback to original behavior
            int centralSquareSize = 2;

            for (int x = -centralSquareSize; x <= centralSquareSize; x++)
            {
                for (int y = -centralSquareSize; y <= centralSquareSize; y++)
                {
                    Vector2I pos = new (center.X + x, center.Y + y);
                    if (IsPositionInWorldBounds(pos))
                    {
                        _gridArea.SetGroundCell(pos, Area.PathTile);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Places road tiles from the road network.
    /// </summary>
    private void PlaceRoads()
    {
        if (_roadNetwork == null)
        {
            return;
        }

        foreach (var pos in _roadNetwork.GetAllRoadTiles())
        {
            if (IsPositionInWorldBounds(pos))
            {
                _gridArea.SetGroundCell(pos, Area.PathTile);
            }
        }
    }

    /// <summary>
    /// Places a well in the center of the village square.
    /// The well is placed FIRST, directly in the village square, not in a lot.
    /// </summary>
    private void PlaceWellInVillageCenter(Vector2I villageCenter)
    {
        if (_buildingManager == null || _currentVillage == null)
        {
            return;
        }

        // Get the well template
        var wellTemplate = _buildingManager.GetTemplate("Well");
        if (wellTemplate == null)
        {
            Log.Warn("VillageGenerator: Well template not found");
            return;
        }

        // Well dimensions: 2 wide x 3 tall
        var wellSize = new Vector2I(wellTemplate.Size[0], wellTemplate.Size[1]);

        // Calculate position to center the well on the village center
        // Center = villageCenter - (wellSize / 2)
        var wellPosition = new Vector2I(
            villageCenter.X - (wellSize.X / 2),
            villageCenter.Y - (wellSize.Y / 2));

        // Verify position is valid for building placement
        if (!IsPositionInWorldBounds(wellPosition, wellSize))
        {
            Log.Warn($"VillageGenerator: Well position {wellPosition} out of bounds");
            return;
        }

        // Place the well using TemplateStamper via BuildingManager
        var wellResult = _buildingManager.StampBuilding("Well", wellPosition, _gridArea);
        if (wellResult == null)
        {
            Log.Warn($"VillageGenerator: Failed to place well at {wellPosition}");
            return;
        }

        // Register the well's rooms with the village
        foreach (var room in wellResult.Rooms)
        {
            _currentVillage.AddRoom(room, _gridArea);
        }

        // Register storage tags from template
        RegisterStorageTagsFromStampResult(wellResult, wellTemplate);

        // Register well facilities directly with village knowledge
        // (well has no room/enclosure, so the RoomSystem won't pick it up automatically)
        foreach (var facility in wellResult.Facilities)
        {
            var facilityPos = facility.GetAbsolutePositions().FirstOrDefault();
            _currentVillage.Knowledge.RegisterFacility("Well", facility, _gridArea, facilityPos);
        }

        Log.Print($"VillageGenerator: Placed well at {wellPosition}, centered on village center {villageCenter}");
    }

    /// <summary>
    /// Places buildings in available lots from the road network.
    /// </summary>
    private void PlaceBuildingsInLots()
    {
        if (_roadNetwork == null)
        {
            return;
        }

        // Count available lots to determine how many farms to place
        int availableLots = _roadNetwork.GetAvailableLots().Count;

        // Place 1 farm for every 4 houses, with a minimum of 2 farms
        int farmCount = Math.Max(2, availableLots / 5);

        Log.Print($"VillageGenerator: Placing granary + {farmCount} farms in {availableLots} total lots");

        // Priority 0: Granary in corner lot (near center)
        var cornerLot = GetCornerLot();
        if (cornerLot != null)
        {
            PlaceBuildingInLot("Granary", cornerLot);
        }
        else
        {
            PlaceBuildingInAvailableLot("Granary"); // fallback
        }

        // Priority 1: Required buildings and features (farms, graveyard, pond)
        for (int i = 0; i < farmCount; i++)
        {
            PlaceBuildingInAvailableLot("Simple Farm");
        }

        // Graveyard in edge lot (far from center)
        var edgeLot = GetEdgeLot();
        if (edgeLot != null)
        {
            PlaceBuildingInLot("Graveyard", edgeLot);
        }
        else
        {
            PlaceBuildingInAvailableLot("Graveyard"); // fallback
        }

        // Place player's house near the graveyard (thematically appropriate for a necromancer)
        PlacePlayerHouseNearGraveyard();

        // Place pond in an available lot
        PlacePondInAvailableLot();

        // Priority 2: Fill remaining lots with houses
        while (true)
        {
            var lot = _roadNetwork.GetAvailableLot();
            if (lot == null)
            {
                break;
            }

            PlaceBuildingInLot("Simple House", lot);
        }

        // Initialize granary standing orders after all houses are placed
        InitializeGranaryOrders();
    }

    /// <summary>
    /// Places a pond in the next available lot.
    /// </summary>
    private void PlacePondInAvailableLot()
    {
        var lot = _roadNetwork?.GetAvailableLot();
        if (lot == null)
        {
            Log.Warn("No available lot for pond");
            return;
        }

        PlacePondInLot(lot);
    }

    /// <summary>
    /// Places a building in the next available lot.
    /// </summary>
    private void PlaceBuildingInAvailableLot(string buildingType)
    {
        var lot = _roadNetwork?.GetAvailableLot();
        if (lot == null)
        {
            Log.Warn($"No available lot for {buildingType}");
            return;
        }

        PlaceBuildingInLot(buildingType, lot);
    }

    /// <summary>
    /// Places the player's house near the graveyard (thematically appropriate for a necromancer).
    /// The player's house is a special house that will be assigned to the player.
    /// </summary>
    private void PlacePlayerHouseNearGraveyard()
    {
        if (_roadNetwork == null || _placedGraveyard == null)
        {
            Log.Warn("VillageGenerator: Cannot place player house - no road network or graveyard");
            return;
        }

        // Get a reference position from the graveyard room's tiles
        Vector2I graveyardPos = Vector2I.Zero;
        foreach (var tile in _placedGraveyard.Tiles)
        {
            graveyardPos = tile;
            break;
        }

        // Find the nearest available lot to the graveyard
        var availableLots = _roadNetwork.GetAvailableLots();
        if (availableLots.Count == 0)
        {
            Log.Warn("VillageGenerator: No available lot for player house");
            return;
        }

        // Sort by distance to graveyard and pick the closest
        var nearestLot = availableLots
            .OrderBy(l => (l.Position - graveyardPos).LengthSquared())
            .First();

        // Place the player's house (skip entity spawning - player lives alone)
        PlaceBuildingInLot("Scholar's House", nearestLot, skipEntitySpawn: true);

        // Get the player's house room directly from the lot
        _playerHouseRoom = nearestLot.OccupyingRoom;
        if (_playerHouseRoom != null)
        {
            // Store the StampResult for CellarGenerator
            // We need to find it — the lot's room was set by PlaceBuildingInLot
            // The StampResult is not stored on the lot, so we track it via a field set during PlaceBuildingInLot
            PlayerHouseStampResult = _lastStampResult;

            Log.Print($"VillageGenerator: Player house placed near graveyard at {graveyardPos}");

            // Register the player as a village resident so they get shared knowledge
            // and so GranaryTrait.InitializeOrdersFromVillage() can detect them via HasScholarResident().
            // NOTE: Player home assignment (SetHome) happens later in World.InitializePlayerAfterGeneration()
            // because player traits aren't loaded until Player.Initialize() runs.
            if (_player != null)
            {
                _currentVillage?.AddResident(_player);
                Log.Print($"VillageGenerator: Registered player as village resident (home will be set after player initialization)");
            }
        }
        else
        {
            Log.Warn("VillageGenerator: Failed to place player house");
        }
    }

    /// <summary>
    /// Gets an available corner lot (where AdjacentRoad is null).
    /// Corner lots are added last to AllLots and don't belong to a road segment.
    /// </summary>
    private VillageLot? GetCornerLot()
    {
        return _roadNetwork?.AllLots
            .Where(l => l.State == LotState.Available && l.AdjacentRoad == null)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets an available edge lot (furthest from village center).
    /// Edge lots are road lots (AdjacentRoad != null) sorted by distance from center.
    /// </summary>
    private VillageLot? GetEdgeLot()
    {
        var edgeLots = _roadNetwork?.AllLots
            .Where(l => l.State == LotState.Available && l.AdjacentRoad != null)
            .ToList();

        if (edgeLots == null || edgeLots.Count == 0)
        {
            return null;
        }

        // Pick the one furthest from village center
        return edgeLots.OrderByDescending(l =>
            (l.Position - _roadNetwork!.VillageCenter).LengthSquared())
            .FirstOrDefault();
    }

    /// <summary>
    /// Places a building in a specific lot.
    /// </summary>
    /// <param name="buildingType">The building type to place.</param>
    /// <param name="lot">The lot to place the building in.</param>
    /// <param name="skipEntitySpawn">If true, don't spawn entities for this building (e.g., player's house).</param>
    private void PlaceBuildingInLot(string buildingType, VillageLot lot, bool skipEntitySpawn = false)
    {
        if (_roadNetwork == null || lot == null || _buildingManager == null)
        {
            return;
        }

        var template = _buildingManager.GetTemplate(buildingType);
        if (template == null)
        {
            Log.Warn($"No template for {buildingType}");
            return;
        }

        var buildingSize = new Vector2I(template.Size[0], template.Size[1]);

        if (!lot.CanFitBuilding(buildingSize))
        {
            Log.Warn($"Building {buildingType} ({buildingSize}) doesn't fit in lot {lot.Id}");
            lot.State = LotState.Reserved;
            return;
        }

        var position = lot.GetBuildingPlacementPosition(buildingSize);

        // Verify position is valid for building placement
        if (!IsPositionInWorldBounds(position, buildingSize))
        {
            Log.Warn($"Position {position} out of bounds for {buildingType}");
            lot.State = LotState.Reserved;
            return;
        }

        var stampResult = _buildingManager.StampBuilding(buildingType, position, _gridArea);
        if (stampResult == null)
        {
            Log.Warn($"Failed to place {buildingType} at {position}");
            lot.State = LotState.Reserved;
            return;
        }

        // Track for PlayerHouseStampResult capture
        _lastStampResult = stampResult;

        // Use the first facility's containing room for lot tracking
        var primaryRoom = stampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);
        RoadNetwork.MarkLotOccupied(lot, primaryRoom);

        // Register all rooms with the village
        foreach (var room in stampResult.Rooms)
        {
            _currentVillage?.AddRoom(room, _gridArea);
        }

        // Register building storage tags from template (data-driven, not hardcoded)
        RegisterStorageTagsFromStampResult(stampResult, template);

        // Spawn entities based on building type (unless skipped, e.g., for player's house)
        if (!skipEntitySpawn)
        {
            SpawnEntitiesForStampResult(stampResult, buildingType);
        }

        Log.Print($"Placed {buildingType} in lot {lot.Id} at {position}");
    }

    /// <summary>
    /// Places a pond in a specific lot, filling the lot with water tiles.
    /// The pond is oval-shaped and fills as much of the lot as possible.
    /// </summary>
    private void PlacePondInLot(VillageLot lot)
    {
        if (_roadNetwork == null || lot == null)
        {
            return;
        }

        // Calculate pond center (center of the lot)
        Vector2I pondCenter = new (
            lot.Position.X + (lot.Size.X / 2),
            lot.Position.Y + (lot.Size.Y / 2));

        // Pond radius is half the lot size minus 1 for a margin
        // This ensures the pond fits within the lot bounds
        int pondRadiusX = (lot.Size.X / 2) - 1;
        int pondRadiusY = (lot.Size.Y / 2) - 1;

        int waterTilesCount = 0;

        // Place water tiles in an oval pattern
        for (int x = -pondRadiusX; x <= pondRadiusX; x++)
        {
            for (int y = -pondRadiusY; y <= pondRadiusY; y++)
            {
                // Create an oval pond using ellipse equation
                float normalizedX = (float)(x * x) / (pondRadiusX * pondRadiusX);
                float normalizedY = (float)(y * y) / (pondRadiusY * pondRadiusY);

                if (normalizedX + normalizedY <= 1.0f)
                {
                    Vector2I pos = new (pondCenter.X + x, pondCenter.Y + y);
                    if (IsPositionInWorldBounds(pos))
                    {
                        _gridArea.SetGroundCell(pos, Area.WaterTile);
                        waterTilesCount++;
                    }
                }
            }
        }

        // Mark the lot as occupied (no building, but used for the pond)
        lot.State = LotState.Occupied;

        Log.Print($"Placed pond in lot {lot.Id} at center {pondCenter}, created {waterTilesCount} water tiles");
    }

    /// <summary>
    /// Registers storage tags for each room in a StampResult from the template's facility definitions.
    /// This reads StorageTags from all facilities with storage configurations.
    /// </summary>
    private void RegisterStorageTagsFromStampResult(StampResult stampResult, BuildingTemplate template)
    {
        if (_currentVillage == null || template.Facilities == null)
        {
            return;
        }

        // Collect all tags from all facilities that have storage
        var allTags = new HashSet<string>();
        foreach (var facility in template.Facilities)
        {
            if (facility.Storage?.Tags != null)
            {
                foreach (var tag in facility.Storage.Tags)
                {
                    allTags.Add(tag);
                }
            }
        }

        // Register the collected tags for the room that contains the storage facility
        if (allTags.Count > 0)
        {
            var storageRoom = stampResult.Facilities
                .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
                ?.ContainingRoom;
            if (storageRoom != null)
            {
                _currentVillage.Knowledge.RegisterRoomStorageTags(storageRoom, allTags, _gridArea);
                Log.Print($"VillageGenerator: Registered {template.Name} with storage tags: [{string.Join(", ", allTags)}]");
            }
        }
    }

    /// <summary>
    /// Spawns appropriate entities for a stamped building based on its type.
    /// </summary>
    private void SpawnEntitiesForStampResult(StampResult stampResult, string buildingType)
    {
        Vector2I buildingPos = stampResult.Origin;
        Vector2I buildingSize = stampResult.Size;

        switch (buildingType)
        {
            case "Simple House":
            {
                // Home is the room containing the storage facility (where food is kept)
                var houseRoom = stampResult.Facilities
                    .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
                    ?.ContainingRoom
                    ?? stampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);

                // Track house for villager assignment
                if (houseRoom != null)
                {
                    _placedHouses.Add(houseRoom);
                }

                // Add initial bread to house storage (3-5 loaves)
                StockRoomWithFood(houseRoom, stampResult.TemplateName);

                // Spawn farmer if farms exist (distribute farmers across farms round-robin)
                if (_placedFarms.Count > 0)
                {
                    // Use house count to distribute farmers evenly across farms
                    int farmIndex = (_placedHouses.Count - 1) % _placedFarms.Count;
                    SpawnVillagerNearStamp(buildingPos, buildingSize,
                        home: houseRoom, job: "farmer", workplace: _placedFarms[farmIndex]);
                }
                else
                {
                    // No farms available, spawn regular villager
                    SpawnVillagerNearStamp(buildingPos, buildingSize,
                        home: houseRoom);
                }

                // Second villager: baker (works at home)
                SpawnVillagerNearStamp(buildingPos, buildingSize,
                    home: houseRoom, job: "baker", workplace: houseRoom);
                break;
            }

            case "Simple Farm":
            {
                // Farm workplace is the room containing any facility (crops and storage share a room)
                var farmRoom = stampResult.Facilities
                    .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
                    ?.ContainingRoom
                    ?? stampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);

                // Track farm for assigning farmers later
                if (farmRoom != null)
                {
                    _placedFarms.Add(farmRoom);
                }

                // Storage tags are registered from template by RegisterStorageTagsFromStampResult
                // Farm gets farmer assigned but farmer lives in house
                break;
            }

            case "Graveyard":
            {
                // Graveyard tracking uses the storage room (where corpses are kept)
                var graveyardRoom = stampResult.Facilities
                    .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
                    ?.ContainingRoom
                    ?? stampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);

                // Track graveyard for player house placement
                _placedGraveyard = graveyardRoom;

                // Storage tags are registered from template by RegisterStorageTagsFromStampResult

                // Stock graveyard with initial corpses
                StockRoomWithCorpses(graveyardRoom, stampResult.TemplateName);
                break;
            }

            case "Granary":
            {
                // Track granary for order initialization
                _placedGranaryResult = stampResult;

                // Granary stocking uses the storage room
                var granaryRoom = stampResult.Facilities
                    .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
                    ?.ContainingRoom
                    ?? stampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);

                // Storage tags are registered from template by RegisterStorageTagsFromStampResult

                // Stock granary with initial supplies
                StockRoomWithGranaryFood(granaryRoom, stampResult.TemplateName);

                // Distributor is spawned later in InitializeGranaryOrders() after houses are placed
                break;
            }
        }
    }

    /// <summary>
    /// Initialize granary standing orders based on placed households.
    /// Called after all buildings are placed so we know which houses have bakers.
    /// Also spawns the distributor now that houses are available.
    /// </summary>
    private void InitializeGranaryOrders()
    {
        if (_placedGranaryResult == null || _currentVillage == null)
        {
            return;
        }

        // Attach GranaryTrait to the granary's storage facility so distributors can find it
        // Find the storage facility directly from the stamp result facilities
        var granaryStorage = _placedGranaryResult.Facilities
            .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>());
        if (granaryStorage == null)
        {
            Log.Warn("VillageGenerator: Granary has no storage facility, cannot initialize orders");
            return;
        }

        var granaryTrait = new GranaryTrait();
        granaryStorage.SelfAsEntity().AddTrait(granaryTrait, 0);
        granaryTrait.InitializeOrdersFromVillage(_currentVillage);
        Log.Print($"VillageGenerator: Initialized granary orders: {granaryTrait.GetOrdersSummary()}");

        // Spawn distributor now that houses are available
        Vector2I granaryPos = _placedGranaryResult.Origin;
        Vector2I granarySize = _placedGranaryResult.Size;
        SpawnDistributorNearGranary(granaryPos, granarySize);
    }

    /// <summary>
    /// Spawns a distributor near the granary and assigns them to a house.
    /// The distributor will be assigned to the first house with space.
    /// </summary>
    private void SpawnDistributorNearGranary(Vector2I granaryPos, Vector2I granarySize)
    {
        if (_placedGranaryResult == null)
        {
            return;
        }

        // Find a house room for the distributor to live in
        Room? distributorHome = null;
        foreach (var houseRoom in _placedHouses)
        {
            // Each house has capacity 2, check if there's room
            if (houseRoom.Residents.Count < 2)
            {
                distributorHome = houseRoom;
                break;
            }
        }

        // If no house has space, use the first house (distributor will share)
        if (distributorHome == null && _placedHouses.Count > 0)
        {
            distributorHome = _placedHouses[0];
        }

        if (distributorHome == null)
        {
            Log.Warn("VillageGenerator: No house available for distributor");
            return;
        }

        // Workplace is the room containing the granary's storage facility
        var granaryRoom = _placedGranaryResult.Facilities
            .FirstOrDefault(f => f.SelfAsEntity().HasTrait<StorageTrait>())
            ?.ContainingRoom
            ?? _placedGranaryResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);

        // Spawn the distributor near the granary
        SpawnVillagerNearStamp(
            granaryPos,
            granarySize,
            home: distributorHome,
            job: "distributor",
            workplace: granaryRoom);
    }

    /// <summary>
    /// Spawns a building near another building with directional preference.
    /// </summary>
    /// <returns></returns>
    public bool SpawnBuildingNearBuilding(Vector2I baseBuildingPos, Vector2I baseBuildingSize,
        string newBuildingType, string newBuildingDirection = "right", int wiggleRoom = 2)
    {
        if (_buildingManager == null)
        {
            return false;
        }

        // Get template from BuildingManager
        var template = _buildingManager.GetTemplate(newBuildingType);
        if (template == null)
        {
            Log.Error($"Failed to find template for building type: {newBuildingType}");
            return false;
        }

        // Get size from template
        Vector2I newBuildingSize = template.Size;

        // Find valid position for the new building
        Vector2I newBuildingPos = FindPositionForBuildingNear(baseBuildingPos, baseBuildingSize,
            newBuildingSize, newBuildingDirection, wiggleRoom);

        // If no valid position was found, return false
        if (newBuildingPos == baseBuildingPos)
        {
            Log.Error($"Could not find valid position to spawn {newBuildingType} near building at {baseBuildingPos}");
            return false;
        }

        // Use BuildingManager to stamp the building
        var stampResult = _buildingManager.StampBuilding(newBuildingType, newBuildingPos, _gridArea);

        if (stampResult != null)
        {
            // Register rooms with village
            foreach (var room in stampResult.Rooms)
            {
                _currentVillage?.AddRoom(room, _gridArea);
            }

            Log.Print($"Placed {newBuildingType} at {newBuildingPos} near building at {baseBuildingPos}");
            return true;
        }
        else
        {
            Log.Error($"Failed to create building of type {newBuildingType} at {newBuildingPos}");
            return false;
        }
    }

    /// <summary>
    /// Spawns a villager near a stamped building with an optional job.
    /// Uses the data-driven BeingResourceManager system for entity creation.
    /// </summary>
    /// <param name="buildingPos">Position of the building to spawn near.</param>
    /// <param name="buildingSize">Size of the building.</param>
    /// <param name="home">Home room for the villager.</param>
    /// <param name="job">Job name: "farmer", "baker", "distributor", or null for no job.</param>
    /// <param name="workplace">Workplace room for the job (farm for farmer, home for baker).</param>
    private void SpawnVillagerNearStamp(
        Vector2I buildingPos,
        Vector2I buildingSize,
        Room? home,
        string? job = null,
        Room? workplace = null)
    {
        Vector2I beingPos = FindPositionInFrontOfBuilding(buildingPos, buildingSize);

        if (beingPos != buildingPos && _gridArea.IsCellWalkable(beingPos))
        {
            // Check if this villager should have debug enabled
            // Enable debug on the first villager of each job type for comprehensive logging
            bool isDebugVillager = false;
            string jobKey = job?.ToLowerInvariant() ?? "none";
            if (!_debugEnabledJobTypes.Contains(jobKey))
            {
                isDebugVillager = true;
                _debugEnabledJobTypes.Add(jobKey);
            }

            // Determine definition ID based on job
            string definitionId = job?.ToLowerInvariant() switch
            {
                "farmer" => "village_farmer",
                "baker" => "village_baker",
                "distributor" => "village_distributor",
                _ => "human_townsfolk"
            };

            // Pass home and workplace as runtime parameters
            // These are used by VillagerTrait.Configure() and job trait Configure() methods
            var runtimeParams = new Dictionary<string, object?>
            {
                { "home", home },
                { "workplace", workplace },
                { "farm", workplace } // FarmerJobTrait looks for "farm" or "workplace"
            };

            // Create the GenericBeing from definition
            var being = GenericBeing.CreateFromDefinition(definitionId, runtimeParams);
            if (being == null)
            {
                Log.Error($"VillageGenerator: Failed to create {definitionId} at {beingPos}");
                return;
            }

            // Initialize the being
            being.Initialize(_gridArea, beingPos, _gameController, debugEnabled: isDebugVillager);

            // Log the spawn
            if (job != null && workplace != null)
            {
                if (string.Equals(job, "distributor", StringComparison.OrdinalIgnoreCase))
                {
                    // Enable debug logging for distributor to verify behavior
                    being.DebugEnabled = true;
                    Log.Print($"Spawned {definitionId} at {beingPos}, working at {workplace.Name}, living at {home?.Name ?? "unknown"} (DEBUG ENABLED)");
                }
                else
                {
                    Log.Print($"Spawned {definitionId} at {beingPos}, assigned to {workplace.Name}");
                }
            }
            else
            {
                Log.Print($"Spawned {definitionId} at {beingPos} (no job)");
            }

            // Register as village resident (gives access to village knowledge)
            // This should be done BEFORE adding to scene tree so VillagerTrait can find it
            _currentVillage?.AddResident(being);

            // Add to scene tree (triggers _Ready which loads traits from definition)
            _gridArea.AddEntity(beingPos, being);
            _entitiesContainer.AddChild(being);
            _entityThinkingSystem.RegisterEntity(being);

            // Track villager for debug selection
            _spawnedVillagers.Add(being);
        }
        else
        {
            Log.Error($"Could not find valid position to spawn villager near building at {buildingPos}");
        }
    }

    /// <summary>
    /// Stock a house room with initial food supply.
    /// </summary>
    private void StockRoomWithFood(Room? room, string templateName)
    {
        var storage = room?.GetStorage();
        if (storage == null)
        {
            Log.Warn($"House '{templateName}' has no storage for initial food");
            return;
        }

        var breadDef = ItemResourceManager.Instance.GetDefinition("bread");
        if (breadDef == null)
        {
            Log.Error("VillageGenerator: bread item definition not found");
            return;
        }

        int breadCount = _rng.RandiRange(3, 5);
        var bread = new Item(breadDef, breadCount);

        if (storage.AddItem(bread))
        {
            Log.Print($"Stocked {templateName} with {breadCount} bread");
        }
    }

    /// <summary>
    /// Stock a granary room with initial food supplies for distribution.
    /// </summary>
    private void StockRoomWithGranaryFood(Room? room, string templateName)
    {
        var storage = room?.GetStorage();
        if (storage == null)
        {
            Log.Warn($"Granary '{templateName}' has no storage for initial food");
            return;
        }

        // Add bread for non-baker households
        var breadDef = ItemResourceManager.Instance.GetDefinition("bread");
        if (breadDef != null)
        {
            int breadCount = _rng.RandiRange(20, 30);
            var bread = new Item(breadDef, breadCount);
            storage.AddItem(bread);
            Log.Print($"Stocked {templateName} with {breadCount} bread");
        }

        // Add wheat for baker households
        var wheatDef = ItemResourceManager.Instance.GetDefinition("wheat");
        if (wheatDef != null)
        {
            int wheatCount = _rng.RandiRange(30, 50);
            var wheat = new Item(wheatDef, wheatCount);
            storage.AddItem(wheat);
            Log.Print($"Stocked {templateName} with {wheatCount} wheat");
        }
    }

    /// <summary>
    /// Stock a graveyard room with initial corpses.
    /// </summary>
    private void StockRoomWithCorpses(Room? room, string templateName)
    {
        var storage = room?.GetStorage();
        if (storage == null)
        {
            Log.Warn($"Graveyard '{templateName}' has no storage for corpses");
            return;
        }

        // Get corpse item definition
        var corpseDef = ItemResourceManager.Instance.GetDefinition("corpse");
        if (corpseDef == null)
        {
            Log.Error("VillageGenerator: corpse item definition not found");
            return;
        }

        // Add 5-10 corpses (zombies need to eat!)
        int corpseCount = _rng.RandiRange(5, 10);
        for (int i = 0; i < corpseCount; i++)
        {
            var corpse = new Item(corpseDef, 1);  // Corpses don't stack
            storage.AddItem(corpse);
        }

        Log.Print($"Stocked {templateName} with {corpseCount} corpses");
    }

    /// <summary>
    /// Find a position near a building suitable for a character.
    /// </summary>
    private Vector2I FindPositionInFrontOfBuilding(Vector2I buildingPos, Vector2I buildingSize)
    {
        // Try positions around the building perimeter (prioritize the front/entrance)
        Vector2I[] possiblePositions =
        [

            // Bottom (front) - most likely entrance
            new (buildingPos.X + (buildingSize.X / 2), buildingPos.Y + buildingSize.Y + 1),

            // Right side
            new (buildingPos.X + buildingSize.X + 1, buildingPos.Y + (buildingSize.Y / 2)),

            // Top
            new (buildingPos.X + (buildingSize.X / 2), buildingPos.Y - 1),

            // Left side
            new (buildingPos.X - 1, buildingPos.Y + (buildingSize.Y / 2))
        ];

        // Check each possible position
        foreach (Vector2I pos in possiblePositions)
        {
            if (IsValidSpawnPosition(pos))
            {
                return pos;
            }
        }

        // If no position directly adjacent works, try in a small radius
        for (int radius = 2; radius <= 3; radius++)
        {
            // Try positions in a square around the building
            for (int xOffset = -radius; xOffset <= buildingSize.X + radius; xOffset++)
            {
                for (int yOffset = -radius; yOffset <= buildingSize.Y + radius; yOffset++)
                {
                    // Only check perimeter positions
                    if (xOffset == -radius || xOffset == buildingSize.X + radius ||
                        yOffset == -radius || yOffset == buildingSize.Y + radius)
                    {
                        Vector2I pos = new (buildingPos.X + xOffset, buildingPos.Y + yOffset);

                        if (IsValidSpawnPosition(pos))
                        {
                            return pos;
                        }
                    }
                }
            }
        }

        // Could not find a valid position
        return buildingPos; // Return original position as fallback
    }

    /// <summary>
    /// Find a valid position for a building near another building.
    /// </summary>
    private Vector2I FindPositionForBuildingNear(Vector2I basePos, Vector2I baseSize,
        Vector2I newSize, string direction, int wiggleRoom)
    {
        // Calculate the ideal starting position based on direction preference
        Vector2I idealPos = CalculateIdealPosition(basePos, baseSize, newSize, direction);

        // If the ideal position works, return it immediately
        if (IsValidBuildingPosition(idealPos, newSize))
        {
            return idealPos;
        }

        // Try positions with increasing wiggles
        for (int attempt = 1; attempt <= wiggleRoom; attempt++)
        {
            // Try positions perpendicular to the preferred direction
            List<Vector2I> wigglePositions = GetWigglePositions(idealPos, newSize, direction, attempt);

            foreach (var pos in wigglePositions)
            {
                if (IsValidBuildingPosition(pos, newSize))
                {
                    return pos;
                }
            }
        }

        // If no position is found within wiggle room, try expanding outward
        for (int distance = 1; distance <= 5; distance++)
        {
            Vector2I expandedPos = ExpandPositionOutward(idealPos, newSize, direction, distance);
            if (IsValidBuildingPosition(expandedPos, newSize))
            {
                return expandedPos;
            }

            // Try with wiggle at the expanded distance
            for (int wiggle = 1; wiggle <= wiggleRoom; wiggle++)
            {
                List<Vector2I> expandedWigglePositions = GetWigglePositions(expandedPos, newSize, direction, wiggle);

                foreach (var pos in expandedWigglePositions)
                {
                    if (IsValidBuildingPosition(pos, newSize))
                    {
                        return pos;
                    }
                }
            }
        }

        // Return base position as fallback if no valid position found
        return basePos;
    }

    /// <summary>
    /// Calculate the ideal position based on direction.
    /// </summary>
    private static Vector2I CalculateIdealPosition(Vector2I basePos, Vector2I baseSize, Vector2I newSize, string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "right" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y + ((baseSize.Y - newSize.Y) / 2)),
            "left" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y + ((baseSize.Y - newSize.Y) / 2)),
            "top" or "up" => new Vector2I(basePos.X + ((baseSize.X - newSize.X) / 2), basePos.Y - newSize.Y - 1),
            "bottom" or "down" => new Vector2I(basePos.X + ((baseSize.X - newSize.X) / 2), basePos.Y + baseSize.Y + 1),
            "topleft" or "upperleft" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y - newSize.Y - 1),
            "topright" or "upperright" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y - newSize.Y - 1),
            "bottomleft" or "lowerleft" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y + baseSize.Y + 1),
            "bottomright" or "lowerright" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y + baseSize.Y + 1),
            _ => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y), // Default to right
        };
    }

    /// <summary>
    /// Get positions with wiggle room.
    /// </summary>
    private static List<Vector2I> GetWigglePositions(Vector2I basePos, Vector2I buildingSize, string direction, int wiggleAmount)
    {
        var positions = new List<Vector2I>();

        // Add wiggle based on direction
        if (direction is "right" or "left")
        {
            // For horizontal placement, wiggle vertically
            for (int y = -wiggleAmount; y <= wiggleAmount; y++)
            {
                positions.Add(new Vector2I(basePos.X, basePos.Y + y));
            }
        }
        else if (direction is "up" or "top" or "down" or "bottom")
        {
            // For vertical placement, wiggle horizontally
            for (int x = -wiggleAmount; x <= wiggleAmount; x++)
            {
                positions.Add(new Vector2I(basePos.X + x, basePos.Y));
            }
        }
        else
        {
            // For diagonal placements, wiggle in both directions
            for (int x = -wiggleAmount; x <= wiggleAmount; x++)
            {
                for (int y = -wiggleAmount; y <= wiggleAmount; y++)
                {
                    positions.Add(new Vector2I(basePos.X + x, basePos.Y + y));
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// Expand the position outward in the preferred direction.
    /// </summary>
    private static Vector2I ExpandPositionOutward(Vector2I basePos, Vector2I buildingSize, string direction, int distance)
    {
        return direction.ToLowerInvariant() switch
        {
            "right" => new Vector2I(basePos.X + distance, basePos.Y),
            "left" => new Vector2I(basePos.X - distance, basePos.Y),
            "top" or "up" => new Vector2I(basePos.X, basePos.Y - distance),
            "bottom" or "down" => new Vector2I(basePos.X, basePos.Y + distance),
            "topleft" or "upperleft" => new Vector2I(basePos.X - distance, basePos.Y - distance),
            "topright" or "upperright" => new Vector2I(basePos.X + distance, basePos.Y - distance),
            "bottomleft" or "lowerleft" => new Vector2I(basePos.X - distance, basePos.Y + distance),
            "bottomright" or "lowerright" => new Vector2I(basePos.X + distance, basePos.Y + distance),
            _ => new Vector2I(basePos.X + distance, basePos.Y),
        };
    }

    /// <summary>
    /// Check if a position is valid for spawning a character.
    /// </summary>
    private bool IsValidSpawnPosition(Vector2I pos)
    {
        // Check bounds and occupancy
        return IsPositionInWorldBounds(pos) && _gridArea.IsCellWalkable(pos) && !_gridArea.EntitiesGridSystem.IsCellOccupied(pos);
    }

    /// <summary>
    /// Check if a position is valid for placing a building.
    /// </summary>
    private bool IsValidBuildingPosition(Vector2I position, Vector2I buildingSize)
    {
        // Ensure position is within world bounds
        if (!IsPositionInWorldBounds(position, buildingSize))
        {
            return false;
        }

        // Check if the entire area needed for the building is free
        for (int x = 0; x < buildingSize.X; x++)
        {
            for (int y = 0; y < buildingSize.Y; y++)
            {
                Vector2I checkPos = new (position.X + x, position.Y + y);

                // Check for occupied cells
                if (!_gridArea.IsCellWalkable(checkPos))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a position is within world bounds.
    /// </summary>
    private bool IsPositionInWorldBounds(Vector2I position, Vector2I? size = null)
    {
        if (size == null)
        {
            return position.X >= 0 && position.X < _gridArea.GridSize.X &&
                   position.Y >= 0 && position.Y < _gridArea.GridSize.Y;
        }
        else
        {
            return position.X >= 0 && position.X + size.Value.X < _gridArea.GridSize.X &&
                   position.Y >= 0 && position.Y + size.Value.Y < _gridArea.GridSize.Y;
        }
    }

    /// <summary>
    /// Creates dirt paths connecting buildings to the village center.
    /// </summary>
    public void CreateVillagePaths(Vector2I villageCenter)
    {
        List<(Vector2I position, Vector2I entrance, string type)> buildingInfos = [];

        // Collect all rooms and their representative positions from the village
        if (_currentVillage != null)
        {
            foreach (var room in _currentVillage.Rooms)
            {
                // Use the first tile of the room as representative position
                Vector2I roomPos = Vector2I.Zero;
                foreach (var tile in room.Tiles)
                {
                    roomPos = tile;
                    break;
                }

                // Use the first door position as entrance, or room position
                Vector2I entrancePos = room.Doors.Count > 0
                    ? room.Doors[0].GridPosition
                    : roomPos;

                buildingInfos.Add((roomPos, entrancePos, room.Type ?? "Unknown"));
            }
        }

        // Create paths from each building to the village center
        foreach (var info in buildingInfos)
        {
            CreatePath(info.entrance, villageCenter);
        }

        // Optionally, create paths between related buildings
        ConnectRelatedBuildings(buildingInfos);
    }

    /// <summary>
    /// Creates a dirt path between two points.
    /// </summary>
    private void CreatePath(Vector2I start, Vector2I end)
    {
        // Use A* or simplified pathfinding to create a path between points
        List<Vector2I> path = FindPath(start, end);

        // Convert path points to dirt tiles
        foreach (var point in path)
        {
            _gridArea.SetGroundCell(point, Area.PathTile);
        }
    }

    /// <summary>
    /// Simple pathfinding between two points.
    /// </summary>
    private List<Vector2I> FindPath(Vector2I start, Vector2I end)
    {
        List<Vector2I> path = [];

        // This is a simplified approach - not true A*
        // For a more realistic path, implement A* pathfinding or use a navigation mesh
        Vector2I current = start;
        path.Add(current);

        // Create a path by moving one step at a time
        while (current != end)
        {
            // Determine direction to move (prefer X first, then Y)
            int dx = Mathf.Sign(end.X - current.X);
            int dy = Mathf.Sign(end.Y - current.Y);

            // Try horizontal movement first
            if (dx != 0)
            {
                Vector2I next = new (current.X + dx, current.Y);
                if (IsPositionInWorldBounds(next) && (_gridArea.IsCellWalkable(next) || next == end))
                {
                    current = next;
                    path.Add(current);
                    continue;
                }
            }

            // Try vertical movement
            if (dy != 0)
            {
                Vector2I next = new (current.X, current.Y + dy);
                if (IsPositionInWorldBounds(next) && (_gridArea.IsCellWalkable(next) || next == end))
                {
                    current = next;
                    path.Add(current);
                    continue;
                }
            }

            // If both direct moves are blocked, try a diagonal approach
            if (dx != 0 && dy != 0)
            {
                // Check a few tiles in each direction to find a path around obstacles
                bool foundPath = false;
                for (int i = 1; i <= 3 && !foundPath; i++)
                {
                    // Try horizontal offset then vertical
                    Vector2I detourH = new (current.X + (dx * i), current.Y);
                    if (IsPositionInWorldBounds(detourH) && _gridArea.IsCellWalkable(detourH))
                    {
                        current = detourH;
                        path.Add(current);
                        foundPath = true;
                        break;
                    }

                    // Try vertical offset then horizontal
                    Vector2I detourV = new (current.X, current.Y + (dy * i));
                    if (IsPositionInWorldBounds(detourV) && _gridArea.IsCellWalkable(detourV))
                    {
                        current = detourV;
                        path.Add(current);
                        foundPath = true;
                        break;
                    }
                }

                if (!foundPath)
                {
                    // If we can't find a path, break to avoid infinite loop
                    break;
                }
            }
            else
            {
                // If we can't move in any direction, break to avoid infinite loop
                break;
            }
        }

        return path;
    }

    /// <summary>
    /// Connects buildings that have logical relationships.
    /// </summary>
    private void ConnectRelatedBuildings(List<(Vector2I position, Vector2I entrance, string type)> buildings)
    {
        // Example: Connect church and graveyard
        var graveyards = buildings.Where(b => b.type == "Graveyard").ToList();
        var churches = buildings.Where(b => b.type == "Church").ToList();

        foreach (var graveyard in graveyards)
        {
            // Find the closest church
            if (churches.Count > 0)
            {
                var (position, entrance, type) = churches
                    .OrderBy(c => (c.position - graveyard.position).LengthSquared())
                    .First();

                CreatePath(graveyard.entrance, entrance);
            }
        }

        // Connect other related buildings as needed
        // Example: houses to tavern, blacksmith to houses, etc.
    }
}
