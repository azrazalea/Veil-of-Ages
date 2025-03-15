using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGenerator : Node
{
    [Export]
    public PackedScene TreeScene;

    [Export]
    public PackedScene BuildingScene;

    [Export]
    public int NumberOfTrees = 20;

    [Export]
    public bool GenerateOnReady = true;

    // References to TileMapLayers
    private TileMapLayer _groundLayer;
    private TileMapLayer _objectsLayer;
    private TileMapLayer _entitiesLayer;

    // Reference to the grid system and entities container
    private GridSystem _gridSystem;
    private Node2D _entitiesContainer;

    // Random number generator
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // Terrain tile IDs (these will need to be set based on your actual TileSet)
    [Export]
    public int GrassTileId = 0;

    [Export]
    public int DirtTileId = 1;

    [Export]
    public int WaterTileId = 2;

    public override void _Ready()
    {
        _rng.Randomize();

        // Find required nodes
        var world = GetTree().GetFirstNodeInGroup("World") as World;
        if (world == null)
        {
            GD.PrintErr("WorldGenerator: Could not find World node!");
            return;
        }

        _groundLayer = world.GetNode<TileMapLayer>("GroundLayer");
        _objectsLayer = world.GetNode<TileMapLayer>("ObjectsLayer");
        _entitiesLayer = world.GetNode<TileMapLayer>("EntitiesLayer");
        _gridSystem = world.GetNode<GridSystem>("GridSystem");
        _entitiesContainer = world.GetNode<Node2D>("Entities");

        // Ensure we have all required nodes
        if (_groundLayer == null || _gridSystem == null || _entitiesContainer == null)
        {
            GD.PrintErr("WorldGenerator: Missing required nodes!");
            return;
        }

        if (GenerateOnReady)
        {
            Generate();
        }
    }

    // Main generation method
    public void Generate()
    {
        // Clear existing world
        ClearWorld();

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

        // Add some decorative elements
        GenerateDecorations();
    }

    // Clear the existing world
    private void ClearWorld()
    {
        // Clear the TileMapLayers
        _groundLayer.Clear();
        _objectsLayer.Clear();
        _entitiesLayer.Clear();

        // Remove all entities except the player
        foreach (Node child in _entitiesContainer.GetChildren())
        {
            if (child is Player)
            {
                continue; // Don't remove the player
            }
            child.QueueFree();
        }

        // Reset the grid system
        for (int x = 0; x < _gridSystem.GridSize.X; x++)
        {
            for (int y = 0; y < _gridSystem.GridSize.Y; y++)
            {
                _gridSystem.SetCellOccupied(new Vector2I(x, y), false);
            }
        }

        // Reserve the player's position
        if (_entitiesContainer.HasNode("Player"))
        {
            var player = _entitiesContainer.GetNode<Player>("Player");
            Vector2I playerPos = _gridSystem.WorldToGrid(player.GlobalPosition);
            _gridSystem.SetCellOccupied(playerPos, true);
        }
    }

    // Generate basic terrain
    private void GenerateTerrain()
    {
        // Fill the ground with grass by default
        for (int x = 0; x < _gridSystem.GridSize.X; x++)
        {
            for (int y = 0; y < _gridSystem.GridSize.Y; y++)
            {
                _groundLayer.SetCell(new Vector2I(x, y), GrassTileId, Vector2I.Zero);
            }
        }

        // Add some dirt patches
        int numDirtPatches = _rng.RandiRange(3, 8);
        for (int i = 0; i < numDirtPatches; i++)
        {
            Vector2I patchCenter = new Vector2I(
                _rng.RandiRange(5, _gridSystem.GridSize.X - 5),
                _rng.RandiRange(5, _gridSystem.GridSize.Y - 5)
            );

            int patchSize = _rng.RandiRange(2, 5);
            for (int x = -patchSize; x <= patchSize; x++)
            {
                for (int y = -patchSize; y <= patchSize; y++)
                {
                    // Create a roughly circular patch
                    if (x * x + y * y <= patchSize * patchSize)
                    {
                        Vector2I pos = new Vector2I(patchCenter.X + x, patchCenter.Y + y);
                        if (pos.X >= 0 && pos.X < _gridSystem.GridSize.X &&
                            pos.Y >= 0 && pos.Y < _gridSystem.GridSize.Y)
                        {
                            _groundLayer.SetCell(pos, DirtTileId, Vector2I.Zero);
                        }
                    }
                }
            }
        }

        // Add a water feature (small pond or stream)
        Vector2I waterStart = new Vector2I(
            _rng.RandiRange(5, _gridSystem.GridSize.X - 15),
            _rng.RandiRange(5, _gridSystem.GridSize.Y - 15)
        );

        // Simple pond
        int pondSize = _rng.RandiRange(3, 6);
        for (int x = -pondSize; x <= pondSize; x++)
        {
            for (int y = -pondSize; y <= pondSize; y++)
            {
                // Create an oval pond
                if ((x * x) / (float)(pondSize * pondSize) + (y * y) / (float)(pondSize * pondSize) <= 1.0f)
                {
                    Vector2I pos = new Vector2I(waterStart.X + x, waterStart.Y + y);
                    if (pos.X >= 0 && pos.X < _gridSystem.GridSize.X &&
                        pos.Y >= 0 && pos.Y < _gridSystem.GridSize.Y)
                    {
                        _groundLayer.SetCell(pos, WaterTileId, Vector2I.Zero);

                        // Mark water as impassable in the grid system
                        _gridSystem.SetCellOccupied(pos, true);
                    }
                }
            }
        }
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
            Vector2I gridPos = new Vector2I(
                _rng.RandiRange(0, _gridSystem.GridSize.X - 1),
                _rng.RandiRange(0, _gridSystem.GridSize.Y - 1)
            );

            // Skip if tile is already occupied
            if (_gridSystem.IsCellOccupied(gridPos))
            {
                continue;
            }

            // Skip if it's a water tile
            var tileData = _groundLayer.GetCellTileData(gridPos);
            if (tileData != null && _groundLayer.GetCellSourceId(gridPos) == WaterTileId)
            {
                continue;
            }

            // Place tree at grid position by instancing the scene
            Node2D tree = TreeScene.Instantiate<Node2D>();
            _entitiesContainer.AddChild(tree);

            // Initialize the tree at this position
            if (tree is Tree typedTree)
            {
                typedTree.Initialize(_gridSystem, gridPos);
            }
            else
            {
                // If for some reason it's not our Tree type, just position it
                tree.GlobalPosition = _gridSystem.GridToWorld(gridPos);
                _gridSystem.SetCellOccupied(gridPos, true);
            }

            treesPlaced++;
        }

        GD.Print($"Placed {treesPlaced} trees after {attempts} attempts");
    }

    private void GenerateVillage()
    {
        // Define village center (somewhere near the middle of the map)
        Vector2I villageCenter = new Vector2I(
            _gridSystem.GridSize.X / 2,
            _gridSystem.GridSize.Y / 2
        );

        // Mark center as village square (no buildings here)
        _gridSystem.SetCellOccupied(villageCenter, true);

        // Clear grass in village center to make a small square
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2I pos = new Vector2I(villageCenter.X + x, villageCenter.Y + y);
                if (pos.X >= 0 && pos.X < _gridSystem.GridSize.X &&
                    pos.Y >= 0 && pos.Y < _gridSystem.GridSize.Y)
                {
                    _groundLayer.SetCell(pos, DirtTileId, Vector2I.Zero);
                }
            }
        }

        // Place buildings around the village center
        string[] buildingTypes = { "House", "House", "House", "Blacksmith", "Tavern", "Farm", "Well" };

        // Place in a rough circle around the center
        int numBuildings = buildingTypes.Length;

        for (int i = 0; i < numBuildings; i++)
        {
            // Calculate position in a circle
            float angle = (float)i / numBuildings * Mathf.Tau;
            int distance = 5; // Distance from center in tiles

            Vector2I offset = new Vector2I(
                Mathf.RoundToInt(Mathf.Cos(angle) * distance),
                Mathf.RoundToInt(Mathf.Sin(angle) * distance)
            );

            Vector2I buildingPos = villageCenter + offset;

            // Ensure position is within world bounds
            buildingPos.X = Mathf.Clamp(buildingPos.X, 0, _gridSystem.GridSize.X - 1);
            buildingPos.Y = Mathf.Clamp(buildingPos.Y, 0, _gridSystem.GridSize.Y - 1);

            // Find a nearby free cell if this one is occupied
            if (_gridSystem.IsCellOccupied(buildingPos))
            {
                buildingPos = _gridSystem.FindNearestFreeCell(buildingPos);
            }

            // Create building by instancing the scene
            Node2D building = BuildingScene.Instantiate<Node2D>();
            _entitiesContainer.AddChild(building);

            // Initialize the building at this position
            if (building is Building typedBuilding)
            {
                typedBuilding.Initialize(_gridSystem, buildingPos, buildingTypes[i]);
            }
            else
            {
                // If for some reason it's not our Building type, just position it
                building.GlobalPosition = _gridSystem.GridToWorld(buildingPos);
                _gridSystem.SetCellOccupied(buildingPos, true);
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
        int decorationSourceId = 0; // This would be your decoration tileset ID
        Vector2I[] decorationTiles = { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(2, 0) }; // Example atlas coords

        for (int i = 0; i < numDecorations; i++)
        {
            Vector2I gridPos = new Vector2I(
                _rng.RandiRange(0, _gridSystem.GridSize.X - 1),
                _rng.RandiRange(0, _gridSystem.GridSize.Y - 1)
            );

            // Skip if cell is occupied by entities or is water
            if (_gridSystem.IsCellOccupied(gridPos))
            {
                continue;
            }

            var tileData = _groundLayer.GetCellTileData(gridPos);
            if (tileData != null && _groundLayer.GetCellSourceId(gridPos) == WaterTileId)
            {
                continue;
            }

            // Choose a random decoration tile
            Vector2I tileCoords = decorationTiles[_rng.RandiRange(0, decorationTiles.Length - 1)];

            // Place decoration
            _objectsLayer.SetCell(gridPos, decorationSourceId, tileCoords);
        }
    }
}
