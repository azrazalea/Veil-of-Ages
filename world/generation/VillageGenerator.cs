using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core;
using VeilOfAges.Entities;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration
{
    public class VillageGenerator
    {
        private readonly PackedScene _buildingScene;
        private readonly PackedScene _skeletonScene;
        private readonly PackedScene _zombieScene;
        private readonly PackedScene _townsfolkScene;
        private readonly Node _entitiesContainer;
        private readonly Area _gridArea;
        private readonly RandomNumberGenerator _rng = new();
        private readonly EntityThinkingSystem _entityThinkingSystem;
        private readonly BuildingManager? _buildingManager; // Added BuildingManager reference

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
            _buildingScene = buildingScene;
            _skeletonScene = skeletonScene;
            _zombieScene = zombieScene;
            _townsfolkScene = townsfolkScene;
            _entityThinkingSystem = entityThinkingSystem;
            _buildingManager = BuildingManager.Instance; // Get the singleton instance

            GD.Print(_buildingManager);
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
        /// Generate a village centered at the given position
        /// </summary>
        public void GenerateVillage(Vector2I villageCenter = default)
        {
            // Default to center of map if no position specified
            if (villageCenter == default)
            {
                villageCenter = new Vector2I(
                    _gridArea.GridSize.X / 2,
                    _gridArea.GridSize.Y / 2
                );
            }

            // Create the visual village square (dirt area)
            CreateVillageSquare(villageCenter);

            GD.Print("Hello my baby");

            // Place various buildings around the village
            PlaceVillageBuildings(villageCenter);

            CreateVillagePaths(villageCenter);
        }

        /// <summary>
        /// Creates a central village square with dirt ground
        /// </summary>
        private void CreateVillageSquare(Vector2I center)
        {
            int centralSquareSize = 2; // Size of the actual central square

            for (int x = -centralSquareSize; x <= centralSquareSize; x++)
            {
                for (int y = -centralSquareSize; y <= centralSquareSize; y++)
                {
                    Vector2I pos = new(center.X + x, center.Y + y);
                    if (IsPositionInWorldBounds(pos))
                    {
                        _gridArea.SetGroundCell(pos, Area.PathTile);
                    }
                }
            }
        }

        /// <summary>
        /// Places the various buildings around the village center
        /// </summary>
        private void PlaceVillageBuildings(Vector2I center)
        {
            if (_buildingManager == null) return;

            GD.Print("Hello my darling");
            // Define the building types to place
            string[] buildingTypes = ["Simple Farm", "Graveyard", "Simple House", "Simple House"];

            // Calculate minimum safe distance from center for building placement
            int minDistanceFromCenter = 15; // Based on largest building size + buffer
            int squareSize = 15; // Buffer around village center

            for (int i = 0; i < buildingTypes.Length; i++)
            {
                string buildingType = buildingTypes[i];

                // Get template from BuildingManager instead of using static dictionary
                var template = _buildingManager.GetTemplate(buildingType);
                if (template == null)
                {
                    GD.PrintErr($"Failed to find template for building type: {buildingType}");
                    continue;
                }

                Vector2I buildingSize = template.Size;
                // Calculate position in a circle with increased distance
                float angle = (float)i / buildingTypes.Length * Mathf.Tau;
                int distance = minDistanceFromCenter;

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

                    Vector2I buildingPos = center + offset;

                    // Check if position is valid
                    if (!IsPositionInWorldBounds(buildingPos, buildingSize))
                    {
                        continue;
                    }

                    // Check if the entire building area is free
                    bool areaIsFree = IsValidBuildingPosition(buildingPos, buildingSize);

                    // Extra check: ensure we're not too close to the village center
                    if (areaIsFree)
                    {
                        for (int x = 0; x < buildingSize.X && areaIsFree; x++)
                        {
                            for (int y = 0; y < buildingSize.Y && areaIsFree; y++)
                            {
                                Vector2I checkPos = new(buildingPos.X + x, buildingPos.Y + y);
                                Vector2I relativeToCenter = checkPos - center;
                                if (Math.Abs(relativeToCenter.X) <= squareSize &&
                                    Math.Abs(relativeToCenter.Y) <= squareSize)
                                {
                                    areaIsFree = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (areaIsFree)
                    {
                        foundValidPosition = true;

                        // Use BuildingManager to place the building
                        Building? typedBuilding = _buildingManager.PlaceBuilding(buildingType, buildingPos, _gridArea);

                        if (typedBuilding != null)
                        {
                            // Special handling based on building type
                            switch (buildingType)
                            {
                                case "Graveyard":
                                    // If possible, place a Church next to the Graveyard
                                    SpawnBuildingNearBuilding(buildingPos, buildingSize, "Church", "right", 2);

                                    // Spawn undead near the Graveyard
                                    SpawnBeingNearBuilding(buildingPos, buildingSize, _skeletonScene);
                                    SpawnBeingNearBuilding(buildingPos, buildingSize, _zombieScene);
                                    break;

                                case "Simple House":
                                    // Spawn townsfolk near houses
                                    SpawnBeingNearBuilding(buildingPos, buildingSize, _townsfolkScene);
                                    SpawnBeingNearBuilding(buildingPos, buildingSize, _townsfolkScene);
                                    break;
                            }

                            GD.Print($"Placed {buildingType} at {buildingPos}");
                        }
                        else
                        {
                            GD.PrintErr($"Failed to create building of type {buildingType} at {buildingPos}");
                        }
                    }
                }

                if (!foundValidPosition)
                {
                    GD.PrintErr($"Failed to place {buildingType} after {maxPlacementAttempts} attempts");
                }
            }
        }

        /// <summary>
        /// Spawns a building near another building with directional preference
        /// </summary>
        public bool SpawnBuildingNearBuilding(Vector2I baseBuilingPos, Vector2I baseBuildingSize,
            string newBuildingType, string newBuildingDirection = "right", int wiggleRoom = 2)
        {
            if (_buildingManager == null) return false;

            // Get template from BuildingManager
            var template = _buildingManager.GetTemplate(newBuildingType);
            if (template == null)
            {
                GD.PrintErr($"Failed to find template for building type: {newBuildingType}");
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
                GD.PrintErr($"Could not find valid position to spawn {newBuildingType} near building at {baseBuilingPos}");
                return false;
            }

            // Use BuildingManager to place the building
            Building? typedBuilding = _buildingManager.PlaceBuilding(newBuildingType, newBuildingPos, _gridArea);

            if (typedBuilding != null)
            {
                GD.Print($"Placed {newBuildingType} at {newBuildingPos} near building at {baseBuilingPos}");
                return true;
            }
            else
            {
                GD.PrintErr($"Failed to create building of type {newBuildingType} at {newBuildingPos}");
                return false;
            }
        }
        /// <summary>
        /// Spawns a character near a building
        /// </summary>
        private void SpawnBeingNearBuilding(Vector2I buildingPos, Vector2I buildingSize, PackedScene beingScene)
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
                    typedBeing.Initialize(_gridArea, beingPos);
                    _entityThinkingSystem.RegisterEntity(typedBeing);
                    GD.Print($"Spawned {typedBeing.GetType().Name} at {beingPos} near building at {buildingPos}");
                }
                else
                {
                    // Fallback positioning if not the correct type
                    being.Position = Utils.GridToWorld(beingPos);
                }

                _gridArea.AddEntity(beingPos, being);
                _entitiesContainer.AddChild(being);
            }
            else
            {
                GD.PrintErr($"Could not find valid position to spawn being near building at {buildingPos}");
            }
        }

        /// <summary>
        /// Find a position near a building suitable for a character
        /// </summary>
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
                        // Only check perimeter positions
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

        /// <summary>
        /// Find a valid position for a building near another building
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
        /// Calculate the ideal position based on direction
        /// </summary>
        private Vector2I CalculateIdealPosition(Vector2I basePos, Vector2I baseSize, Vector2I newSize, string direction)
        {
            return direction.ToLower() switch
            {
                "right" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y + (baseSize.Y - newSize.Y) / 2),
                "left" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y + (baseSize.Y - newSize.Y) / 2),
                "top" or "up" => new Vector2I(basePos.X + (baseSize.X - newSize.X) / 2, basePos.Y - newSize.Y - 1),
                "bottom" or "down" => new Vector2I(basePos.X + (baseSize.X - newSize.X) / 2, basePos.Y + baseSize.Y + 1),
                "topleft" or "upperleft" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y - newSize.Y - 1),
                "topright" or "upperright" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y - newSize.Y - 1),
                "bottomleft" or "lowerleft" => new Vector2I(basePos.X - newSize.X - 1, basePos.Y + baseSize.Y + 1),
                "bottomright" or "lowerright" => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y + baseSize.Y + 1),
                _ => new Vector2I(basePos.X + baseSize.X + 1, basePos.Y), // Default to right
            };
        }

        /// <summary>
        /// Get positions with wiggle room
        /// </summary>
        private List<Vector2I> GetWigglePositions(Vector2I basePos, Vector2I buildingSize, string direction, int wiggleAmount)
        {
            var positions = new List<Vector2I>();

            // Add wiggle based on direction
            if (direction == "right" || direction == "left")
            {
                // For horizontal placement, wiggle vertically
                for (int y = -wiggleAmount; y <= wiggleAmount; y++)
                {
                    positions.Add(new Vector2I(basePos.X, basePos.Y + y));
                }
            }
            else if (direction == "up" || direction == "top" || direction == "down" || direction == "bottom")
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
        /// Expand the position outward in the preferred direction
        /// </summary>
        private Vector2I ExpandPositionOutward(Vector2I basePos, Vector2I buildingSize, string direction, int distance)
        {
            return direction.ToLower() switch
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
        /// Check if a position is valid for spawning a character
        /// </summary>
        private bool IsValidSpawnPosition(Vector2I pos)
        {
            // Check bounds and occupancy
            return IsPositionInWorldBounds(pos) && _gridArea.IsCellWalkable(pos);
        }

        /// <summary>
        /// Check if a position is valid for placing a building
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
                    Vector2I checkPos = new(position.X + x, position.Y + y);

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
        /// Check if a position is within world bounds
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
        /// Creates dirt paths connecting buildings to the village center
        /// </summary>
        public void CreateVillagePaths(Vector2I villageCenter)
        {
            List<(Vector2I position, Vector2I entrance, string type)> buildings = new();

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
        /// Creates a dirt path between two points
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
        /// Simple pathfinding between two points
        /// </summary>
        private List<Vector2I> FindPath(Vector2I start, Vector2I end)
        {
            List<Vector2I> path = new();

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
                    Vector2I next = new(current.X + dx, current.Y);
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
                    Vector2I next = new(current.X, current.Y + dy);
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
                    Vector2I nextH = new(current.X + dx, current.Y);
                    Vector2I nextV = new(current.X, current.Y + dy);

                    // Check a few tiles in each direction to find a path around obstacles
                    bool foundPath = false;
                    for (int i = 1; i <= 3 && !foundPath; i++)
                    {
                        // Try horizontal offset then vertical
                        Vector2I detourH = new(current.X + dx * i, current.Y);
                        if (IsPositionInWorldBounds(detourH) && _gridArea.IsCellWalkable(detourH))
                        {
                            current = detourH;
                            path.Add(current);
                            foundPath = true;
                            break;
                        }

                        // Try vertical offset then horizontal
                        Vector2I detourV = new(current.X, current.Y + dy * i);
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
        /// Connects buildings that have logical relationships
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
}
