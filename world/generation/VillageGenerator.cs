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
    // Buildings are instantiated via BuildingManager, scene kept for future direct use
    // private readonly PackedScene _buildingScene;
    private readonly PackedScene _skeletonScene;
    private readonly PackedScene _zombieScene;
    private readonly PackedScene _townsfolkScene;
    private readonly Node _entitiesContainer;
    private readonly Area _gridArea;
    private readonly RandomNumberGenerator _rng = new ();
    private readonly BuildingManager? _buildingManager;
    private readonly EntityThinkingSystem _entityThinkingSystem;
    private readonly GameController? _gameController;

    // Track placed farms for assigning farmers
    private readonly List<Building> _placedFarms = new ();

    // Track placed houses for assigning villagers
    private readonly List<Building> _placedHouses = new ();

    // Track spawned villagers for debug selection
    private readonly List<Being> _spawnedVillagers = new ();

    // Debug villager selection - specifically target bakers for debugging
    private bool _debugVillagerSelected;
#pragma warning disable CS0649 // Field never assigned (intentional: set to job name for debugging)
    private readonly string? _debugTargetJob; // Set to a job name (e.g., "baker") to target, or leave unset for random
#pragma warning restore CS0649

    // The village being generated (tracks buildings and residents)
    private Village? _currentVillage;

    // Road network for lot-based building placement
    private RoadNetwork? _roadNetwork;

    public VillageGenerator(
        Area gridArea,
        Node entitiesContainer,
        PackedScene buildingScene,
        PackedScene skeletonScene,
        PackedScene zombieScene,
        PackedScene townsfolkScene,
        EntityThinkingSystem entityThinkingSystem,
        int? seed = null)
    {
        _gridArea = gridArea;
        _entitiesContainer = entitiesContainer;
        _ = buildingScene; // Kept for API compatibility, buildings use BuildingManager
        _skeletonScene = skeletonScene;
        _zombieScene = zombieScene;
        _townsfolkScene = townsfolkScene;
        _entityThinkingSystem = entityThinkingSystem;
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
            villageSquareRadius: 3,  // 7x7 central square
            roadWidth: 2,
            lotSize: optimalLotSize,
            lotsPerSide: 3);           // 24 total lots
        _roadNetwork.GenerateLayout();

        // Place village square and roads as dirt tiles
        PlaceVillageSquare(villageCenter);
        PlaceRoads();

        // Reset debug selection state
        _debugVillagerSelected = false;

        // Place buildings using lot system
        PlaceBuildingsInLots();

        // Log which villager was selected for debug
        LogDebugVillagerSelection();

        Log.Print($"Village '{_currentVillage.VillageName}' created with {_currentVillage.Buildings.Count} buildings and {_currentVillage.Residents.Count} residents");

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
        // Formula: for every 4 houses we need 1 farm = 5 lots total
        // So: farmCount = availableLots / 5 (integer division)
        // This leaves: availableLots - 1 (graveyard) - farmCount (farms) = houses
        int farmCount = Math.Max(2, availableLots / 5);

        Log.Print($"VillageGenerator: Placing {farmCount} farms in {availableLots} total lots");

        // Priority 1: Required buildings (farms, graveyard)
        for (int i = 0; i < farmCount; i++)
        {
            PlaceBuildingInAvailableLot("Simple Farm");
        }

        PlaceBuildingInAvailableLot("Graveyard");

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
    /// Places a building in a specific lot.
    /// </summary>
    private void PlaceBuildingInLot(string buildingType, VillageLot lot)
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

        var building = _buildingManager.PlaceBuilding(buildingType, position, _gridArea);
        if (building == null)
        {
            Log.Warn($"Failed to place {buildingType} at {position}");
            lot.State = LotState.Reserved;
            return;
        }

        RoadNetwork.MarkLotOccupied(lot, building);
        _currentVillage?.AddBuilding(building);

        // Spawn entities based on building type
        SpawnEntitiesForBuilding(building, buildingType);

        Log.Print($"Placed {buildingType} in lot {lot.Id} at {position}");
    }

    /// <summary>
    /// Spawns appropriate entities for a building based on its type.
    /// </summary>
    private void SpawnEntitiesForBuilding(Building building, string buildingType)
    {
        Vector2I buildingPos = building.GetCurrentGridPosition();
        Vector2I buildingSize = building.GridSize;

        switch (buildingType)
        {
            case "Simple House":
                // Track house for villager assignment
                _placedHouses.Add(building);

                // Add initial bread to house storage (3-5 loaves)
                StockHouseWithFood(building);

                // Spawn farmer if farms exist (distribute farmers across farms round-robin)
                if (_placedFarms.Count > 0)
                {
                    // Use house count to distribute farmers evenly across farms
                    int farmIndex = (_placedHouses.Count - 1) % _placedFarms.Count;
                    SpawnVillagerNearBuilding(buildingPos, buildingSize, _townsfolkScene,
                        home: building, job: "farmer", workplace: _placedFarms[farmIndex]);
                }
                else
                {
                    // No farms available, spawn regular villager
                    SpawnVillagerNearBuilding(buildingPos, buildingSize, _townsfolkScene,
                        home: building);
                }

                // Second villager: baker (works at home)
                SpawnVillagerNearBuilding(buildingPos, buildingSize, _townsfolkScene,
                    home: building, job: "baker", workplace: building);
                break;

            case "Simple Farm":
                // Track farm for assigning farmers later
                _placedFarms.Add(building);

                // Farm gets farmer assigned but farmer lives in house
                break;

            case "Graveyard":
                // Stock graveyard with initial corpses
                StockGraveyardWithCorpses(building);

                // Spawn undead near the Graveyard and set as their home
                SpawnUndeadNearBuilding(buildingPos, buildingSize, _skeletonScene, building);
                SpawnUndeadNearBuilding(buildingPos, buildingSize, _zombieScene, building);
                break;
        }
    }

    /// <summary>
    /// Spawns a building near another building with directional preference.
    /// </summary>
    /// <returns></returns>
    public bool SpawnBuildingNearBuilding(Vector2I baseBuilingPos, Vector2I baseBuildingSize,
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
        Vector2I newBuildingPos = FindPositionForBuildingNear(baseBuilingPos, baseBuildingSize,
            newBuildingSize, newBuildingDirection, wiggleRoom);

        // If no valid position was found, return false
        if (newBuildingPos == baseBuilingPos)
        {
            Log.Error($"Could not find valid position to spawn {newBuildingType} near building at {baseBuilingPos}");
            return false;
        }

        // Use BuildingManager to place the building
        Building? typedBuilding = _buildingManager.PlaceBuilding(newBuildingType, newBuildingPos, _gridArea);

        if (typedBuilding != null)
        {
            // Register building with village
            _currentVillage?.AddBuilding(typedBuilding);

            Log.Print($"Placed {newBuildingType} at {newBuildingPos} near building at {baseBuilingPos}");
            return true;
        }
        else
        {
            Log.Error($"Failed to create building of type {newBuildingType} at {newBuildingPos}");
            return false;
        }
    }

    /// <summary>
    /// Spawns a villager near a building with an optional job.
    /// </summary>
    /// <param name="buildingPos">Position of the building to spawn near.</param>
    /// <param name="buildingSize">Size of the building.</param>
    /// <param name="beingScene">Scene to instantiate.</param>
    /// <param name="home">Home building for the villager.</param>
    /// <param name="job">Job name: "farmer", "baker", or null for no job.</param>
    /// <param name="workplace">Workplace building for the job (farm for farmer, home for baker).</param>
    private void SpawnVillagerNearBuilding(
        Vector2I buildingPos,
        Vector2I buildingSize,
        PackedScene beingScene,
        Building? home,
        string? job = null,
        Building? workplace = null)
    {
        Vector2I beingPos = FindPositionInFrontOfBuilding(buildingPos, buildingSize);

        if (beingPos != buildingPos && _gridArea.IsCellWalkable(beingPos))
        {
            Node2D being = beingScene.Instantiate<Node2D>();

            if (being is Being typedBeing)
            {
                // Check if this villager should have debug enabled
                // If targeting a specific job, only that job gets debug. Otherwise, first villager.
                bool isDebugVillager = false;
                if (!_debugVillagerSelected)
                {
                    if (_debugTargetJob == null)
                    {
                        // No target job: debug the first villager
                        isDebugVillager = true;
                        _debugVillagerSelected = true;
                    }
                    else if (string.Equals(job, _debugTargetJob, StringComparison.OrdinalIgnoreCase))
                    {
                        // Target job matches: debug this villager
                        isDebugVillager = true;
                        _debugVillagerSelected = true;
                    }
                }

                typedBeing.Initialize(_gridArea, beingPos, _gameController, debugEnabled: isDebugVillager);

                // Set pending home before adding to scene tree (HumanTownsfolk._Ready will use it)
                if (home != null && typedBeing is HumanTownsfolk townsfolk)
                {
                    townsfolk.PendingHome = home;
                }

                // Assign job trait based on job name (before adding to scene, so it gets queued)
                if (job != null && workplace != null)
                {
                    switch (job.ToLowerInvariant())
                    {
                        case "farmer":
                            var farmerTrait = new FarmerJobTrait(workplace);
                            typedBeing.SelfAsEntity().AddTraitToQueue(farmerTrait, priority: -1);
                            Log.Print($"Spawned farmer at {beingPos}, assigned to {workplace.BuildingName}");
                            break;

                        case "baker":
                            var bakerTrait = new BakerJobTrait(workplace);
                            typedBeing.SelfAsEntity().AddTraitToQueue(bakerTrait, priority: -1);
                            Log.Print($"Spawned baker at {beingPos}, working at {workplace.BuildingName}");
                            break;

                        default:
                            Log.Warn($"Unknown job type: {job}");
                            break;
                    }
                }
                else if (job == null)
                {
                    Log.Print($"Spawned villager at {beingPos} (no job)");
                }

                // Track villager for debug selection
                _spawnedVillagers.Add(typedBeing);

                // Register as village resident (gives access to village knowledge)
                _currentVillage?.AddResident(typedBeing);

                _gridArea.AddEntity(beingPos, being);
                _entitiesContainer.AddChild(being);
                _entityThinkingSystem.RegisterEntity(typedBeing);
            }
            else
            {
                being.Position = Utils.GridToWorld(beingPos);
                _gridArea.AddEntity(beingPos, being);
                _entitiesContainer.AddChild(being);
            }
        }
        else
        {
            Log.Error($"Could not find valid position to spawn villager near building at {buildingPos}");
        }
    }

    /// <summary>
    /// Stock a house with initial food supply.
    /// </summary>
    private void StockHouseWithFood(Building house)
    {
        var storage = house.GetStorage();
        if (storage == null)
        {
            Log.Warn($"House {house.BuildingName} has no storage for initial food");
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
            Log.Print($"Stocked {house.BuildingName} with {breadCount} bread");
        }
    }

    /// <summary>
    /// Stock a graveyard with initial corpses.
    /// </summary>
    private void StockGraveyardWithCorpses(Building graveyard)
    {
        var storage = graveyard.GetStorage();
        if (storage == null)
        {
            Log.Warn($"Graveyard {graveyard.BuildingName} has no storage for corpses");
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

        Log.Print($"Stocked {graveyard.BuildingName} with {corpseCount} corpses");
    }

    /// <summary>
    /// Spawns an undead near a graveyard and sets it as their home.
    /// </summary>
    private void SpawnUndeadNearBuilding(Vector2I buildingPos, Vector2I buildingSize, PackedScene beingScene, Building homeGraveyard)
    {
        // Find a position in front of the building
        Vector2I beingPos = FindPositionInFrontOfBuilding(buildingPos, buildingSize);

        // Ensure the position is valid and not occupied
        if (beingPos != buildingPos && _gridArea.IsCellWalkable(beingPos))
        {
            Node2D being = beingScene.Instantiate<Node2D>();

            // Initialize the being if it has the correct type
            if (being is Being typedBeing)
            {
                typedBeing.Initialize(_gridArea, beingPos, _gameController);

                // Set home graveyard for zombies
                var zombieTrait = typedBeing.SelfAsEntity().GetTrait<ZombieTrait>();
                zombieTrait?.SetHomeGraveyard(homeGraveyard);

                Log.Print($"Spawned {typedBeing.GetType().Name} at {beingPos} (home: {homeGraveyard.BuildingName})");
            }
            else
            {
                // Fallback positioning if not the correct type
                being.Position = Utils.GridToWorld(beingPos);
            }

            _gridArea.AddEntity(beingPos, being);
            _entitiesContainer.AddChild(being);

            if (being is Being typedBeingForRegister)
            {
                _entityThinkingSystem.RegisterEntity(typedBeingForRegister);
            }
        }
        else
        {
            Log.Error($"Could not find valid position to spawn undead near building at {buildingPos}");
        }
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
        return IsPositionInWorldBounds(pos) && _gridArea.IsCellWalkable(pos);
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
        List<(Vector2I position, Vector2I entrance, string type)> buildings = new ();

        // Collect all buildings and their entrance positions
        foreach (var entity in _entitiesContainer.GetChildren())
        {
            if (entity is Building building)
            {
                Vector2I buildingPos = building.GetCurrentGridPosition();

                // Get entrance positions directly from the building
                var entrancePositions = building.GetEntrancePositions();

                // Use the first entrance position if available, otherwise use building position
                Vector2I entrancePos = entrancePositions.Count > 0 ? entrancePositions[0] : buildingPos;

                buildings.Add((buildingPos, entrancePos, building.BuildingType));
            }
        }

        // Create paths from each building to the village center
        foreach (var building in buildings)
        {
            CreatePath(building.entrance, villageCenter);
        }

        // Optionally, create paths between related buildings
        ConnectRelatedBuildings(buildings);
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
        List<Vector2I> path = new ();

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
                // Try to go around obstacles
                _ = new // Try to go around obstacles
                Vector2I(current.X + dx, current.Y);

                _ = new Vector2I(current.X, current.Y + dy);

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
