using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities;
using VeilOfAges.Core;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration
{
    public partial class GridGenerator : Node
    {
        [Export]
        public PackedScene? TreeScene;

        [Export]
        public PackedScene? BuildingScene;

        [Export]
        public int NumberOfTrees = 20;

        [Export]
        public PackedScene? SkeletonScene;
        [Export]
        public PackedScene? ZombieScene;
        [Export]
        public PackedScene? TownsfolkScene;

        private Node? _entitiesContainer;
        private Area? _activeGridArea;

        // Random number generator
        private RandomNumberGenerator _rng = new();

        private EntityThinkingSystem? _entityThinkingSystem;

        // Main generation method
        public void Generate(World world)
        {
            _entityThinkingSystem = GetNode<EntityThinkingSystem>("/root/World/EntityThinkingSystem");
            _rng.Randomize();

            _activeGridArea = world.ActiveGridArea;
            _entitiesContainer = world.GetNode<Node>("Entities");

            // Ensure we have all required nodes
            if (_entitiesContainer == null || _activeGridArea == null || _entitiesContainer == null ||
                BuildingScene == null || SkeletonScene == null || ZombieScene == null || TownsfolkScene == null)
            {
                GD.PrintErr("WorldGenerator: Missing required nodes!");
                return;
            }

            // Generate terrain
            GenerateTerrain();

            var villageGenerator = new VillageGenerator(
                _activeGridArea,
                _entitiesContainer,
                BuildingScene,
                SkeletonScene,
                ZombieScene,
                TownsfolkScene,
                _entityThinkingSystem
            );

            // Generate village at the center of the map
            villageGenerator.GenerateVillage();


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
            if (_activeGridArea == null) return;

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
            if (_activeGridArea == null || TreeScene == null || _entitiesContainer == null) return;

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

        private void GenerateDecorations()
        {
            if (_activeGridArea == null) return;

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
            if (_activeGridArea == null || _entityThinkingSystem == null || _entitiesContainer == null) return;

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
                    GD.Print($"Spawned being at {beingPos} near building at {buildingPos}");
                }
                else
                {
                    // Fallback positioning if not the correct type
                    being.Position = Utils.GridToWorld(beingPos);
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
            if (_activeGridArea == null) return false;

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
}
