using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Terrain;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration;

public partial class GridGenerator : Node
{
    [Export]
    public PackedScene? TreeScene;

    [Export]
    public PackedScene? BuildingScene;

    [Export]
    public int NumberOfTrees = 20;

    [Export]
    public PackedScene? GenericBeingScene;

    private Node? _entitiesContainer;
    private Area? _activeGridArea;

    // Random number generator
    private RandomNumberGenerator _rng = new ();

    private EntityThinkingSystem? _entityThinkingSystem;
    private GameController? _gameController;

    /// <summary>
    /// Gets the player's house building, placed near the graveyard during village generation.
    /// Only valid after Generate() has been called.
    /// </summary>
    public Building? PlayerHouse { get; private set; }

    // Main generation method
    public void Generate(World world)
    {
        _entityThinkingSystem = GetNode<EntityThinkingSystem>("/root/World/EntityThinkingSystem");
        _gameController = GetNode<GameController>("/root/World/GameController");
        _rng.Randomize();

        _activeGridArea = world.ActiveGridArea;
        _entitiesContainer = world.GetNode<Node>("Entities");

        // Get the player from the entities container
        var player = _entitiesContainer?.GetNodeOrNull<Player>("Player");

        // Ensure we have all required nodes
        if (_entitiesContainer == null || _activeGridArea == null ||
            BuildingScene == null || GenericBeingScene == null)
        {
            Log.Error("WorldGenerator: Missing required nodes!");
            return;
        }

        // Generate terrain
        GenerateTerrain();

        var villageGenerator = new VillageGenerator(
            _activeGridArea,
            _entitiesContainer,
            BuildingScene,
            GenericBeingScene,
            _entityThinkingSystem,
            player);

        // Generate village at the center of the map
        // Player home is assigned during generation, before granary orders are initialized
        villageGenerator.GenerateVillage();

        // Store the player's house reference (already assigned to player in VillageGenerator)
        PlayerHouse = villageGenerator.PlayerHouse;

        // Generate cellar beneath player's house if it was placed
        if (PlayerHouse != null)
        {
            CellarGenerator.CreateCellar(world, PlayerHouse);
        }

        // Then add trees in unoccupied spaces
        if (TreeScene != null)
        {
            GenerateTrees();
        }

        // // Add some decorative elements
        // GenerateDecorations();
        Log.Print("Done generating!");
    }

    // Generate basic terrain
    private void GenerateTerrain()
    {
        if (_activeGridArea == null)
        {
            return;
        }

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
            Vector2I patchCenter = new (
                _rng.RandiRange(5, _activeGridArea.GridSize.X - 5),
                _rng.RandiRange(5, _activeGridArea.GridSize.Y - 5));

            int patchSize = _rng.RandiRange(2, 5);
            for (int x = -patchSize; x <= patchSize; x++)
            {
                for (int y = -patchSize; y <= patchSize; y++)
                {
                    // Create a roughly circular patch
                    if ((x * x) + (y * y) <= patchSize * patchSize)
                    {
                        Vector2I pos = new (patchCenter.X + x, patchCenter.Y + y);
                        if (pos.X >= 0 && pos.X < _activeGridArea.GridSize.X &&
                            pos.Y >= 0 && pos.Y < _activeGridArea.GridSize.Y)
                        {
                            _activeGridArea.SetGroundCell(pos, Area.DirtTile);
                        }
                    }
                }
            }
        }

        // Note: Pond/water feature is now placed by VillageGenerator in a lot
    }

    private void GenerateTrees()
    {
        if (_activeGridArea == null || TreeScene == null || _entitiesContainer == null)
        {
            return;
        }

        // Collect all building entrance positions to avoid blocking them
        var entrancePositions = GetAllBuildingEntrancePositions();

        // Try to place the requested number of trees
        int treesPlaced = 0;
        int maxAttempts = NumberOfTrees * 3; // Allow some failed attempts
        int attempts = 0;

        while (treesPlaced < NumberOfTrees && attempts < maxAttempts)
        {
            attempts++;

            // Generate random grid position
            Vector2I gridPos = new (
                _rng.RandiRange(0, _activeGridArea.GridSize.X - 5), // Reduced by tree width
                _rng.RandiRange(0, _activeGridArea.GridSize.Y - 6)); // Reduced by tree height

            // Create a "temporary" tree to get its size
            Node2D tempTree = TreeScene.Instantiate<Node2D>();
            Vector2I treeSize = new (1, 1); // Default size

            if (tempTree is Entities.Terrain.Tree typedTree)
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
                    Vector2I checkPos = new (gridPos.X + x, gridPos.Y + y);

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

            // Check if tree placement would be too close to any building entrance
            if (IsTooCloseToEntrance(gridPos, treeSize, entrancePositions, 2))
            {
                continue; // Skip to next attempt if too close to entrance
            }

            // Place tree at grid position by instancing the scene
            Node2D tree = TreeScene.Instantiate<Node2D>();
            _entitiesContainer.AddChild(tree);

            // Initialize the tree at this position
            if (tree is Entities.Terrain.Tree typedTree2)
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

        Log.Print($"Placed {treesPlaced} trees after {attempts} attempts");
    }

    /// <summary>
    /// Collects all building entrance positions from entities in the container.
    /// </summary>
    /// <returns>A list of absolute grid positions representing building entrances.</returns>
    private List<Vector2I> GetAllBuildingEntrancePositions()
    {
        var entrances = new List<Vector2I>();

        if (_entitiesContainer == null)
        {
            return entrances;
        }

        // Iterate through all children in the entities container
        foreach (Node child in _entitiesContainer.GetChildren())
        {
            if (child is Building building)
            {
                // Get all entrance positions from this building
                var buildingEntrances = building.GetEntrancePositions();
                entrances.AddRange(buildingEntrances);
            }
        }

        return entrances;
    }

    /// <summary>
    /// Checks if a tree placement would be too close to any building entrance.
    /// Returns true if any part of the tree is within minDistance tiles of an entrance.
    /// </summary>
    /// <param name="treeGridPos">The top-left grid position of the tree.</param>
    /// <param name="treeSize">The size of the tree in grid tiles.</param>
    /// <param name="entrancePositions">List of absolute entrance positions.</param>
    /// <param name="minDistance">Minimum distance to maintain from entrances (default 2 tiles).</param>
    /// <returns>True if too close to an entrance, false if safe to place.</returns>
    private static bool IsTooCloseToEntrance(Vector2I treeGridPos, Vector2I treeSize, List<Vector2I> entrancePositions, int minDistance)
    {
        // Check each tile of the tree against each entrance
        for (int tx = 0; tx < treeSize.X; tx++)
        {
            for (int ty = 0; ty < treeSize.Y; ty++)
            {
                Vector2I treePos = new (treeGridPos.X + tx, treeGridPos.Y + ty);

                // Check distance to each entrance
                foreach (Vector2I entrance in entrancePositions)
                {
                    // Calculate Manhattan distance (simple distance check)
                    int distanceX = System.Math.Abs(treePos.X - entrance.X);
                    int distanceY = System.Math.Abs(treePos.Y - entrance.Y);
                    int distance = distanceX + distanceY;

                    // If any part of the tree is too close to an entrance, reject this placement
                    if (distance <= minDistance)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void GenerateDecorations()
    {
        if (_activeGridArea == null)
        {
            return;
        }

        // Add some small decorations to the decoration layer
        // This could be flowers, small rocks, etc.
        // For now, we'll just add placeholder decorations
        int numDecorations = _rng.RandiRange(30, 50);

        // Set source and atlas IDs based on your actual decoration tiles
        // int decorationSourceId = 0; // This would be your decoration tileset ID
        Vector2I[] decorationTiles = [new (0, 0), new (1, 0), new (2, 0)]; // Example atlas coords

        for (int i = 0; i < numDecorations; i++)
        {
            Vector2I gridPos = new (
                _rng.RandiRange(0, _activeGridArea.GridSize.X - 1),
                _rng.RandiRange(0, _activeGridArea.GridSize.Y - 1));

            // Skip if cell is occupied by entities or is water
            if (!_activeGridArea.IsCellWalkable(gridPos))
            {
                continue;
            }

            // Choose a random decoration tile
            _ = decorationTiles[_rng.RandiRange(0, decorationTiles.Length - 1)];

            // Place decoration
            // _objectsLayer.SetCell(gridPos, decorationSourceId, tileCoords);
        }
    }
}
