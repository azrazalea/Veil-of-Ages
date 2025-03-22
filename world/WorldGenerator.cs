using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities;
using VeilOfAges.Core;
using VeilOfAges.Grid;
using VeilOfAges;

public partial class WorldGenerator : Node
{
    [Export]
    public PackedScene TreeScene;

    [Export]
    public PackedScene BuildingScene;

    [Export]
    public int NumberOfTrees = 20;

    [Export]
    public PackedScene SkeletonScene;
    [Export]
    public PackedScene ZombieScene;

    private Node2D _entitiesContainer;
    private VeilOfAges.Grid.Area _activeGridArea;

    // Random number generator
    private RandomNumberGenerator _rng = new();

    private EntityThinkingSystem _entityThinkingSystem;

    // Main generation method
    public void Generate(World world)
    {
        _entityThinkingSystem = GetNode<EntityThinkingSystem>("/root/World/EntityThinkingSystem");
        _rng.Randomize();

        _activeGridArea = world.ActiveGridArea;
        _entitiesContainer = world.GetNode<Node2D>("Entities");

        // Ensure we have all required nodes
        if (_entitiesContainer == null)
        {
            GD.PrintErr("WorldGenerator: Missing required nodes!");
            return;
        }

        // Generate terrain
        GenerateTerrain();

        // Generate village first (higher priority)
        if (BuildingScene != null)
        {
            GenerateVillage();
        }

        // Then add trees in unoccupied spaces
        if (TreeScene != null)
        {
            GenerateTrees();
        }

        // // Add some decorative elements
        // GenerateDecorations();

        GD.Print("Done generating!");
    }

    // Generate basic terrain
    private void GenerateTerrain()
    {
        // Fill the ground with grass by default
        for (int x = 0; x < _activeGridArea.GridSize.X; x++)
        {
            for (int y = 0; y < _activeGridArea.GridSize.Y; y++)
            {
                _activeGridArea.SetGroundCell(new Vector2I(x, y), Area.GrassTile);
            }
        }

        // Add some dirt patches
        int numDirtPatches = _rng.RandiRange(3, 8);
        for (int i = 0; i < numDirtPatches; i++)
        {
            Vector2I patchCenter = new(
                _rng.RandiRange(5, _activeGridArea.GridSize.X - 5),
                _rng.RandiRange(5, _activeGridArea.GridSize.Y - 5)
            );

            int patchSize = _rng.RandiRange(2, 5);
            for (int x = -patchSize; x <= patchSize; x++)
            {
                for (int y = -patchSize; y <= patchSize; y++)
                {
                    // Create a roughly circular patch
                    if (x * x + y * y <= patchSize * patchSize)
                    {
                        Vector2I pos = new(patchCenter.X + x, patchCenter.Y + y);
                        if (pos.X >= 0 && pos.X < _activeGridArea.GridSize.X &&
                            pos.Y >= 0 && pos.Y < _activeGridArea.GridSize.Y)
                        {
                            _activeGridArea.SetGroundCell(pos, Area.DirtTile);
                        }
                    }
                }
            }
        }

        // Add a water feature (small pond or stream)
        Vector2I waterStart = new(
            _rng.RandiRange(15, _activeGridArea.GridSize.X - 25),
            _rng.RandiRange(15, _activeGridArea.GridSize.Y - 25)
        );

        // Simple pond
        int pondSize = _rng.RandiRange(3, 6);
        int waterTilesCount = 0;

        // Place water tiles and mark them as occupied in one pass
        for (int x = -pondSize; x <= pondSize; x++)
        {
            for (int y = -pondSize; y <= pondSize; y++)
            {
                // Create an oval pond
                if ((x * x) / (float)(pondSize * pondSize) + (y * y) / (float)(pondSize * pondSize) <= 1.0f)
                {
                    Vector2I pos = new(waterStart.X + x, waterStart.Y + y);
                    if (pos.X >= 0 && pos.X < _activeGridArea.GridSize.X &&
                        pos.Y >= 0 && pos.Y < _activeGridArea.GridSize.Y)
                    {
                        // Set the water tile visually
                        _activeGridArea.SetGroundCell(pos, Area.WaterTile);
                        waterTilesCount++;
                    }
                }
            }
        }

        GD.Print($"Added water pond at {waterStart} with size {pondSize}, created {waterTilesCount} water tiles");
    }

    private void GenerateTrees()
    {
        // Try to place the requested number of trees
        int treesPlaced = 0;
        int maxAttempts = NumberOfTrees * 3; // Allow some failed attempts
        int attempts = 0;

        while (treesPlaced < NumberOfTrees && attempts < maxAttempts)
        {
            attempts++;

            // Generate random grid position
            Vector2I gridPos = new(
                _rng.RandiRange(0, _activeGridArea.GridSize.X - 5), // Reduced by tree width
                _rng.RandiRange(0, _activeGridArea.GridSize.Y - 6)  // Reduced by tree height
            );

            // Create a "temporary" tree to get its size
            Node2D tempTree = TreeScene.Instantiate<Node2D>();
            Vector2I treeSize = new(1, 1); // Default size

            if (tempTree is Tree typedTree)
            {
                treeSize = typedTree.GridSize;
            }
            tempTree.QueueFree(); // Clean up the temporary instance

            // Check if the entire area needed for the tree is free
            bool areaIsFree = true;
            for (int x = 0; x < treeSize.X && areaIsFree; x++)
            {
                for (int y = 0; y < treeSize.Y && areaIsFree; y++)
                {
                    Vector2I checkPos = new(gridPos.X + x, gridPos.Y + y);

                    // Check for occupied cells or water
                    if (!_activeGridArea.IsCellWalkable(checkPos))
                    {
                        areaIsFree = false;
                        break;
                    }
                }
            }

            if (!areaIsFree)
            {
                continue; // Skip to next attempt if area isn't free
            }

            // Place tree at grid position by instancing the scene
            Node2D tree = TreeScene.Instantiate<Node2D>();
            _entitiesContainer.AddChild(tree);

            // Initialize the tree at this position
            if (tree is Tree typedTree2)
            {
                typedTree2.Initialize(_activeGridArea, gridPos);
            }
            else
            {
                // If for some reason it's not our Tree type, just position it
                tree.GlobalPosition = VeilOfAges.Grid.Utils.GridToWorld(gridPos);
            }
            _activeGridArea.AddEntity(gridPos, tree, treeSize);

            treesPlaced++;
        }

        GD.Print($"Placed {treesPlaced} trees after {attempts} attempts");
    }

    private void GenerateVillage()
    {
        // Define building sizes for each building type
        Dictionary<string, Vector2I> buildingSizes = new()
    {
        { "House", new Vector2I(2, 2) },
        { "Blacksmith", new Vector2I(3, 2) },
        { "Tavern", new Vector2I(3, 3) },
        { "Farm", new Vector2I(3, 2) },
        { "Well", new Vector2I(1, 1) },
        { "Graveyard", new Vector2I(7, 6) },
        { "Laboratory", new Vector2I(3, 2) }
    };

        // Define village center (somewhere near the middle of the map)
        Vector2I villageCenter = new(
            _activeGridArea.GridSize.X / 2,
            _activeGridArea.GridSize.Y / 2
        );

        // Make a village square (larger to ensure we have enough room around the center)
        int squareSize = 15; // Larger to account for building sizes
        int centralSquareSize = 2; // Size of the actual central square (dirt area)

        // Create the visual village square (dirt area)
        for (int x = -centralSquareSize; x <= centralSquareSize; x++)
        {
            for (int y = -centralSquareSize; y <= centralSquareSize; y++)
            {
                Vector2I pos = new(villageCenter.X + x, villageCenter.Y + y);
                if (pos.X >= 0 && pos.X < _activeGridArea.GridSize.X &&
                    pos.Y >= 0 && pos.Y < _activeGridArea.GridSize.Y)
                {
                    _activeGridArea.SetGroundCell(pos, Area.DirtTile);
                }
            }
        }

        // Place buildings around the village center
        string[] buildingTypes = ["Graveyard", "Graveyard", "Graveyard"];

        // Calculate minimum safe distance from center for building placement
        int minDistanceFromCenter = 15; // Based on largest building size + buffer

        for (int i = 0; i < buildingTypes.Length; i++)
        {
            string buildingType = buildingTypes[i];
            Vector2I buildingSize = buildingSizes[buildingType];

            // Calculate position in a circle with increased distance
            float angle = (float)i / buildingTypes.Length * Mathf.Tau;
            int distance = minDistanceFromCenter; // Distance from center in tiles

            bool foundValidPosition = false;
            int maxPlacementAttempts = 10;

            for (int attempt = 0; attempt < maxPlacementAttempts && !foundValidPosition; attempt++)
            {
                // Adjust distance slightly each attempt
                int adjustedDistance = distance + attempt * 2;

                Vector2I offset = new(
                    Mathf.RoundToInt(Mathf.Cos(angle) * adjustedDistance),
                    Mathf.RoundToInt(Mathf.Sin(angle) * adjustedDistance)
                );

                Vector2I buildingPos = villageCenter + offset;

                // Ensure position is within world bounds
                if (buildingPos.X < 0 || buildingPos.X + buildingSize.X >= _activeGridArea.GridSize.X ||
                    buildingPos.Y < 0 || buildingPos.Y + buildingSize.Y >= _activeGridArea.GridSize.Y)
                {
                    continue; // Skip this position if out of bounds
                }

                // Check if the entire building area is free
                bool areaIsFree = true;
                for (int x = 0; x < buildingSize.X && areaIsFree; x++)
                {
                    for (int y = 0; y < buildingSize.Y && areaIsFree; y++)
                    {
                        Vector2I checkPos = new(buildingPos.X + x, buildingPos.Y + y);

                        // Check for occupied cells
                        if (!_activeGridArea.IsCellWalkable(checkPos))
                        {
                            areaIsFree = false;
                            break;
                        }

                        // Extra check: ensure we're not too close to the village center
                        Vector2I relativeToCenter = checkPos - villageCenter;
                        if (Math.Abs(relativeToCenter.X) <= squareSize &&
                            Math.Abs(relativeToCenter.Y) <= squareSize)
                        {
                            areaIsFree = false;
                            break;
                        }
                    }
                }

                if (areaIsFree)
                {
                    foundValidPosition = true;

                    // Create building by instancing the scene
                    Node2D building = BuildingScene.Instantiate<Node2D>();
                    _entitiesContainer.AddChild(building);

                    // Initialize the building at this position
                    if (building is Building typedBuilding)
                    {
                        typedBuilding.Initialize(_activeGridArea, buildingPos, buildingType);

                        // If this is a graveyard, spawn a skeleton nearby
                        if (buildingType == "Graveyard" && SkeletonScene != null && ZombieScene != null)
                        {
                            PackedScene beingScene = null;
                            if (new RandomNumberGenerator().RandfRange(0f, 1f) < 0.5f)
                            {
                                beingScene = SkeletonScene;
                            }
                            else
                            {
                                beingScene = ZombieScene;
                            }

                            SpawnBeingNearBuilding(buildingPos, buildingSize, beingScene);
                        }
                    }
                    else
                    {
                        // If for some reason it's not our Building type, just position it
                        building.GlobalPosition = VeilOfAges.Grid.Utils.GridToWorld(buildingPos);
                    }

                    _activeGridArea.AddEntity(buildingPos, building, buildingSize);
                    GD.Print($"Placed {buildingType} at {buildingPos}");
                }
            }

            if (!foundValidPosition)
            {
                GD.PrintErr($"Failed to place {buildingType} after {maxPlacementAttempts} attempts");
            }
        }
    }

    private void GenerateDecorations()
    {
        // Add some small decorations to the decoration layer
        // This could be flowers, small rocks, etc.
        // For now, we'll just add placeholder decorations

        int numDecorations = _rng.RandiRange(30, 50);

        // Set source and atlas IDs based on your actual decoration tiles
        // int decorationSourceId = 0; // This would be your decoration tileset ID
        Vector2I[] decorationTiles = [new(0, 0), new(1, 0), new(2, 0)]; // Example atlas coords

        for (int i = 0; i < numDecorations; i++)
        {
            Vector2I gridPos = new(
                _rng.RandiRange(0, _activeGridArea.GridSize.X - 1),
                _rng.RandiRange(0, _activeGridArea.GridSize.Y - 1)
            );

            // Skip if cell is occupied by entities or is water
            if (!_activeGridArea.IsCellWalkable(gridPos))
            {
                continue;
            }

            // Choose a random decoration tile
            Vector2I tileCoords = decorationTiles[_rng.RandiRange(0, decorationTiles.Length - 1)];

            // Place decoration
            // _objectsLayer.SetCell(gridPos, decorationSourceId, tileCoords);
        }
    }

    // Add this method to WorldGenerator class
    private void SpawnBeingNearBuilding(Vector2I buildingPos, Vector2I buildingSize, PackedScene beingScene)
    {
        // Find a position in front of the graveyard
        Vector2I beingPos = FindPositionInFrontOfBuilding(buildingPos, buildingSize);

        // Ensure the position is valid and not occupied
        if (beingPos != buildingPos && _activeGridArea.IsCellWalkable(beingPos))
        {

            Node2D being = beingScene.Instantiate<Node2D>();
            GD.Print($"Spawning being of type {being.GetType().Name}");

            // Initialize the skeleton if it has the correct type
            if (being is Being typedBeing)
            {
                typedBeing.Initialize(_activeGridArea, beingPos);
                _entityThinkingSystem.RegisterEntity(typedBeing);
                GD.Print($"Spawned being at {beingPos} near graveyard at {buildingPos}");
            }
            else
            {
                // Fallback positioning if not the correct type
                being.GlobalPosition = VeilOfAges.Grid.Utils.GridToWorld(beingPos);
            }

            _activeGridArea.AddEntity(beingPos, being);
            _entitiesContainer.AddChild(being);
        }
        else
        {
            GD.PrintErr($"Could not find valid position to spawn being near graveyard at {buildingPos}");
        }
    }

    // Add this helper method to WorldGenerator class
    private Vector2I FindPositionInFrontOfBuilding(Vector2I buildingPos, Vector2I buildingSize)
    {
        // Try positions around the building perimeter (prioritize the front/entrance)
        Vector2I[] possiblePositions =
        [
            // Bottom (front) - most likely entrance
            new(buildingPos.X + buildingSize.X / 2, buildingPos.Y + buildingSize.Y + 1),
            
            // Right side
            new(buildingPos.X + buildingSize.X + 1, buildingPos.Y + buildingSize.Y / 2),
            
            // Top
            new(buildingPos.X + buildingSize.X / 2, buildingPos.Y - 1),
            
            // Left side
            new(buildingPos.X - 1, buildingPos.Y + buildingSize.Y / 2)
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
                    // Only check perimeter positions (exclude positions inside the building or in the inner rings)
                    if (xOffset == -radius || xOffset == buildingSize.X + radius ||
                        yOffset == -radius || yOffset == buildingSize.Y + radius)
                    {
                        Vector2I pos = new(buildingPos.X + xOffset, buildingPos.Y + yOffset);

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

    // Helper to check if a position is valid for spawning
    private bool IsValidSpawnPosition(Vector2I pos)
    {
        // Check bounds and occupancy
        if (pos.X >= 0 && pos.X < _activeGridArea.GridSize.X &&
            pos.Y >= 0 && pos.Y < _activeGridArea.GridSize.Y &&
            _activeGridArea.IsCellWalkable(pos))
        {
            return true;
        }

        return false;
    }
}
