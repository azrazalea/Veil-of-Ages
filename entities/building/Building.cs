using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities;

public partial class Building : Node2D, IEntity<Trait>
{
    [Export]
    public string BuildingType = "House";

    [Export]
    public string BuildingName = string.Empty;

    [Export]
    public bool CanEnter = true;

    [Export]
    public Vector2I GridSize = new (2, 2); // Size in tiles

    private Vector2I _gridPosition;
    public VeilOfAges.Grid.Area? GridArea { get; private set; }
    public SortedSet<Trait> Traits { get; private set; } = [];
    private TileMapLayer? _tileMap;
    private TileMapLayer? _groundTileMap;
    public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; } = [];

    // Dictionary mapping relative grid positions to building tiles
    private readonly Dictionary<Vector2I, BuildingTile> _tiles = [];
    private readonly Dictionary<Vector2I, BuildingTile> _groundTiles = [];

    // List of entrance positions (relative to building origin)
    private List<Vector2I> _entrancePositions = [];

    // For tracking residents/workers
    private int _capacity;
    private int _occupants;

    // Residents tracking
    private readonly List<Being> _residents = [];

    // Facility positions (keyed by facility ID, with list of positions for each)
    private readonly Dictionary<string, List<Vector2I>> _facilityPositions = new ();

    private const int HORIZONTALPIXELOFFSET = -4;
    private const int VERTICALPIXELOFFSET = -4;

    public override void _Ready()
    {
        Log.Print($"Building grid position _Ready: {_gridPosition}");

        // Snap to grid
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);

        Log.Print($"{BuildingType} registered at grid position {_gridPosition}");
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(VeilOfAges.Grid.Area gridArea, Vector2I gridPos, BuildingTemplate? template = null)
    {
        GridArea = gridArea;
        _gridPosition = gridPos;
        DetectionDifficulties = [];

        // Apply template properties if provided
        if (template != null && template.Name != null && template.BuildingType != null)
        {
            // Set basic properties
            BuildingType = template.BuildingType;
            BuildingName = template.Name;
            GridSize = template.Size;
            _capacity = template.Capacity;
            _entrancePositions = template.EntrancePositions;

            // Initialize storage trait if template specifies storage
            if (template.Storage != null)
            {
                var storageTrait = new StorageTrait(
                    template.Storage.VolumeCapacity,
                    template.Storage.WeightCapacity,
                    template.Storage.DecayRateModifier,
                    template.Storage.Facilities);
                Traits.Add(storageTrait);
            }

            // Populate facility positions from template
            foreach (var facility in template.Facilities)
            {
                if (!_facilityPositions.TryGetValue(facility.Id, out var positions))
                {
                    positions = new List<Vector2I>();
                    _facilityPositions[facility.Id] = positions;
                }

                positions.Add(facility.Position);
            }

            // Initialize and setup TileMap
            InitializeTileMaps(template);
        }

        ZIndex = 2;

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    // Initialize and set up the TileMap
    private void InitializeTileMaps(BuildingTemplate template)
    {
        // If no TileMap was found, create a new one
        if (_tileMap == null)
        {
            _tileMap = new TileMapLayer
            {
                Visible = true
            };
            AddChild(_tileMap);
            _tileMap.Position = new Vector2(HORIZONTALPIXELOFFSET, VERTICALPIXELOFFSET);
            _tileMap.ZAsRelative = true;
        }

        if (_groundTileMap == null)
        {
            _groundTileMap = new TileMapLayer
            {
                Visible = true
            };
            AddChild(_groundTileMap);
            _groundTileMap.Position = new Vector2(HORIZONTALPIXELOFFSET, VERTICALPIXELOFFSET);
            _groundTileMap.ZIndex = _tileMap.ZIndex - 1;
            _groundTileMap.ZAsRelative = true;
        }

        // Initialize the TileResourceManager if not already initialized
        TileResourceManager.Instance.Initialize();

        // Setup the TileSet with all required atlas sources
        TileResourceManager.Instance.SetupTileSet(_tileMap);
        TileResourceManager.Instance.SetupTileSet(_groundTileMap);
        Log.Print($"Tile set {_tileMap.TileSet}");

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);

        // Create individual tiles from template if provided
        if (template != null)
        {
            // Create individual tiles from template
            CreateTilesFromTemplate(template, _gridPosition);
        }
    }

    // Register a building tile with the grid system
    private void RegisterTileWithGrid(Vector2I relativePos, BuildingTile tile)
    {
        if (GridArea == null)
        {
            return;
        }

        // Calculate absolute grid position
        Vector2I absolutePos = _gridPosition + relativePos;

        // Register tile with grid
        if (tile.IsWalkable)
        {
            // For walkable tiles, we don't block the grid cell but may affect movement cost
            // This is a hack as it sets the ground cell to a potentially random graphic.
            GridArea.SetGroundCell(absolutePos, new VeilOfAges.Grid.Tile(
                0,
                new Vector2I(0, 0),
                true, // Walkable
                1.0f)); // Standard movement cost
        }
        else
        {
            // For non-walkable tiles (walls, etc.), mark as entity in grid
            GridArea.AddEntity(absolutePos, this);
        }
    }

    // Get the current grid position
    public Vector2I GetCurrentGridPosition()
    {
        return _gridPosition;
    }

    public SensableType GetSensableType()
    {
        return SensableType.Building;
    }

    // Create building tiles from a template
    private void CreateTilesFromTemplate(BuildingTemplate template, Vector2I gridPos)
    {
        if (_tileMap == null || _groundTileMap == null)
        {
            return;
        }

        // Process each tile in the template
        foreach (var tileData in template.Tiles)
        {
            // Create a tile using the new resource system
            // In the old system, we have Type (string) and Material (string)
            // We'll use these to determine which tile definition and material to use
            if (tileData.Type == null || tileData.Material == null)
            {
                continue;
            }

            // For the tile definition, we'll use the Type field
            string tileDefId = tileData.Type.ToLowerInvariant(); // Use lowercase to match our resource IDs

            // For the material, we'll use the Material field
            string materialId = tileData.Material.ToLowerInvariant(); // Use lowercase to match our resource IDs

            // Get the variant name if specified
            string? variantName = tileData.Variant;

            // If no variant is specified, use "Default"
            if (string.IsNullOrEmpty(variantName))
            {
                variantName = "Default";
            }

            // Calculate the absolute grid position
            Vector2I absoluteGridPos = gridPos + tileData.Position;

            // Create the building tile using the resource manager
            var buildingTile = TileResourceManager.Instance.CreateBuildingTile(
                tileDefId,
                tileData.Position,
                this,
                absoluteGridPos,
                materialId,
                variantName,
                tileData.Category);

            // No fallback - if the tile creation fails, we'll get a null result
            // and the game should crash or handle the error at a higher level
            if (buildingTile == null)
            {
                // Log the error and throw an exception
                var errorMessage = $"Critical Error: Failed to create building tile at position {tileData.Position} with type '{tileDefId}' and material '{materialId}' variant '{variantName}'";
                Log.Error(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }

            // Register with grid system
            RegisterTileWithGrid(tileData.Position, buildingTile);
            if (buildingTile.Type == TileType.Floor)
            {
                var source = (TileSetAtlasSource)_groundTileMap.TileSet.GetSource(buildingTile.SourceId);
                if (source.GetTileAtCoords(buildingTile.AtlasCoords) == new Vector2I(-1, -1))
                {
                    source.CreateTile(buildingTile.AtlasCoords);
                }
            }
            else
            {
                var source = (TileSetAtlasSource)_tileMap.TileSet.GetSource(buildingTile.SourceId);
                if (source.GetTileAtCoords(buildingTile.AtlasCoords) == new Vector2I(-1, -1))
                {
                    source.CreateTile(buildingTile.AtlasCoords);
                }
            }

            Log.Print($"{tileDefId} {tileData.Position} tile with source {buildingTile.SourceId} and coords {buildingTile.AtlasCoords}");

            // Add to tile dictionary (using relative position)
            if (buildingTile.Type == TileType.Floor)
            {
                _groundTiles[tileData.Position] = buildingTile;
                _groundTileMap.SetCell(tileData.Position, buildingTile.SourceId, buildingTile.AtlasCoords);
            }
            else
            {
                _tiles[tileData.Position] = buildingTile;
                _tileMap.SetCell(tileData.Position, buildingTile.SourceId, buildingTile.AtlasCoords);
            }

            // Set the tile in the tilemap for visualization
            Log.Print($"Confirm cell {tileData.Position} atlas source {_tileMap.GetCellSourceId(tileData.Position)} {_tileMap.GetCellAtlasCoords(tileData.Position)}");
        }
    }

    public override void _ExitTree()
    {
        // When the building is removed, unregister all tiles
        if (GridArea != null)
        {
            foreach (var tile in _tiles)
            {
                Vector2I absolutePos = _gridPosition + tile.Key;
                if (!tile.Value.IsWalkable)
                {
                    GridArea.RemoveEntity(absolutePos);
                }
            }
        }
    }

    // Get entrance position(s)
    public List<Vector2I> GetEntrancePositions()
    {
        return _entrancePositions.Select(pos => _gridPosition + pos).ToList();
    }

    /// <summary>
    /// Get positions that are cardinally adjacent (N, S, E, W) to entrance positions.
    /// These tiles should be avoided as pathfinding destinations to prevent doorway blocking.
    /// Returns absolute grid positions.
    /// </summary>
    public HashSet<Vector2I> GetEntranceAdjacentPositions()
    {
        var result = new HashSet<Vector2I>();
        Vector2I[] cardinalDirections = [
            Vector2I.Up,
            Vector2I.Down,
            Vector2I.Left,
            Vector2I.Right
        ];

        foreach (var entranceRelative in _entrancePositions)
        {
            Vector2I entranceAbsolute = _gridPosition + entranceRelative;

            foreach (var direction in cardinalDirections)
            {
                Vector2I adjacentPos = entranceAbsolute + direction;

                // Don't include the entrance itself
                if (adjacentPos != entranceAbsolute)
                {
                    result.Add(adjacentPos);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get relative positions that are cardinally adjacent to entrance positions.
    /// Used internally for filtering walkable interior positions.
    /// Returns relative positions to building origin.
    /// </summary>
    private HashSet<Vector2I> GetEntranceAdjacentRelativePositions()
    {
        var result = new HashSet<Vector2I>();
        Vector2I[] cardinalDirections = [
            Vector2I.Up,
            Vector2I.Down,
            Vector2I.Left,
            Vector2I.Right
        ];

        foreach (var entrance in _entrancePositions)
        {
            foreach (var direction in cardinalDirections)
            {
                Vector2I adjacentPos = entrance + direction;

                // Don't include the entrance itself
                if (adjacentPos != entrance)
                {
                    result.Add(adjacentPos);
                }
            }
        }

        return result;
    }

    // Method for when player interacts with building
    public void Interact()
    {
        Log.Print($"Player is interacting with {BuildingType}");

        // This would later handle entering buildings, triggering events, etc.
    }

    // Update visual representation of the building
    private void UpdateBuildingVisual()
    {
        // Ensure TileMap exists
        if (_tileMap == null || _groundTileMap == null)
        {
            return;
        }

        // Clear existing cells
        _tileMap.Clear();

        // Add each tile to the TileMap
        foreach (var tile in _tiles)
        {
            _tileMap.SetCell(tile.Key, tile.Value.SourceId, tile.Value.AtlasCoords);
        }

        foreach (var tile in _groundTiles)
        {
            _groundTileMap.SetCell(tile.Key, tile.Value.SourceId, tile.Value.AtlasCoords);
        }
    }

    // Get a building tile at the specified relative position
    public BuildingTile? GetTile(Vector2I relativePos)
    {
        if (_tiles.TryGetValue(relativePos, out var tile))
        {
            return tile;
        }

        return null;
    }

    // Get a building tile at the specified absolute grid position
    public BuildingTile? GetTileAtAbsolutePosition(Vector2I absolutePos)
    {
        Vector2I relativePos = absolutePos - _gridPosition;
        return GetTile(relativePos);
    }

    // Check if a position has a tile that blocks sight
    public bool BlocksSight(Vector2I relativePos)
    {
        var tile = GetTile(relativePos);
        return tile != null && tile.BlocksSense(SenseType.Sight);
    }

    // Apply damage to a specific tile
    public bool DamageTile(Vector2I relativePos, int amount)
    {
        var tile = GetTile(relativePos);
        if (tile == null)
        {
            return false;
        }

        // Apply damage and check if destroyed
        bool destroyed = tile.TakeDamage(amount);

        // If tile was destroyed, handle consequences
        if (destroyed)
        {
            // This could change the tile type, make it walkable, etc.
            HandleDestroyedTile(relativePos, tile);
        }

        return destroyed;
    }

    // Handle a destroyed tile
    private void HandleDestroyedTile(Vector2I relativePos, BuildingTile tile)
    {
        if (_tileMap == null)
        {
            return;
        }

        // For now, just make it walkable and update visualization
        tile.IsWalkable = true;

        // Update grid system
        if (GridArea != null)
        {
            Vector2I absolutePos = _gridPosition + relativePos;
            GridArea.RemoveEntity(absolutePos);

            // Register as walkable
            GridArea.SetGroundCell(absolutePos, new VeilOfAges.Grid.Tile(
                tile.SourceId, // Use default source ID for now
                tile.AtlasCoords,
                true, // Now walkable
                1.0f)); // Standard movement cost
        }

        // Update visualization
        // For now, we'll just change the atlas coords to represent damaged state
        // In the future, could have different tiles for damaged states
        _tileMap.SetCell(relativePos, tile.SourceId, tile.AtlasCoords);
    }

    // Add an occupant to the building
    public bool AddOccupant()
    {
        if (_occupants < _capacity)
        {
            _occupants++;
            return true;
        }

        return false; // Building is at capacity
    }

    // Remove an occupant from the building
    public void RemoveOccupant()
    {
        if (_occupants > 0)
        {
            _occupants--;
        }
    }

    // Check if building has space for more occupants
    public bool HasSpace()
    {
        return _occupants < _capacity;
    }

    // Get current occupancy information
    public (int current, int max) GetOccupancy()
    {
        return (_occupants, _capacity);
    }

    // Get all tiles of a specific type
    public List<(Vector2I Position, BuildingTile Tile)> GetTilesOfType(TileType type)
    {
        var result = new List<(Vector2I, BuildingTile)>();

        foreach (var tile in _tiles)
        {
            if (tile.Value.Type == type)
            {
                result.Add((tile.Key, tile.Value));
            }
        }

        return result;
    }

    /// <summary>
    /// Get all interior positions based on tile definitions (inherently walkable tiles).
    /// Excludes entrance positions (doors) so entities don't idle in doorways.
    /// Does NOT check current occupancy - use for goal checking (IsGoalReached).
    /// Returns relative positions to building origin.
    /// </summary>
    /// <param name="excludeEntranceAdjacent">If true, also excludes positions adjacent to entrances (default: true).</param>
    public List<Vector2I> GetInteriorPositions(bool excludeEntranceAdjacent = true)
    {
        var result = new HashSet<Vector2I>();

        // Get positions to exclude
        var entranceAdjacentPositions = excludeEntranceAdjacent
            ? GetEntranceAdjacentRelativePositions()
            : new HashSet<Vector2I>();

        // Include all tile positions (walls, doors, furniture, etc.)
        foreach (var tile in _tiles)
        {
            if (!_entrancePositions.Contains(tile.Key) &&
                !entranceAdjacentPositions.Contains(tile.Key))
            {
                result.Add(tile.Key);
            }
        }

        // Include all ground tile positions (floors)
        foreach (var tile in _groundTiles)
        {
            if (!_entrancePositions.Contains(tile.Key) &&
                !entranceAdjacentPositions.Contains(tile.Key))
            {
                result.Add(tile.Key);
            }
        }

        return result.ToList();
    }

    /// <summary>
    /// Get all walkable positions inside the building bounds.
    /// Excludes entrance positions (doors) and positions adjacent to entrances
    /// so entities don't path to or block doorways.
    /// Queries the GridArea for actual walkability (checks current occupancy).
    /// Use for pathfinding destinations.
    /// Returns relative positions to building origin.
    /// </summary>
    /// <param name="excludeEntranceAdjacent">If true, also excludes positions adjacent to entrances (default: true).</param>
    public List<Vector2I> GetWalkableInteriorPositions(bool excludeEntranceAdjacent = true)
    {
        var result = new List<Vector2I>();

        if (GridArea == null)
        {
            return result;
        }

        // Get positions to exclude
        var entranceAdjacentPositions = excludeEntranceAdjacent
            ? GetEntranceAdjacentRelativePositions()
            : new HashSet<Vector2I>();

        // Check all positions within building bounds
        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                Vector2I relativePos = new (x, y);
                Vector2I absolutePos = _gridPosition + relativePos;

                // Exclude entrance positions so entities don't target doorways
                // Also exclude positions adjacent to entrances to prevent doorway blocking
                if (GridArea.IsCellWalkable(absolutePos) &&
                    !_entrancePositions.Contains(relativePos) &&
                    !entranceAdjacentPositions.Contains(relativePos))
                {
                    result.Add(relativePos);
                }
            }
        }

        return result;
    }

    // Legacy method - kept for compatibility but prefer GetWalkableInteriorPositions
    public List<(Vector2I Position, BuildingTile Tile)> GetWalkableTiles()
    {
        var result = new List<(Vector2I, BuildingTile)>();

        foreach (var tile in _tiles)
        {
            if (tile.Value.IsWalkable)
            {
                result.Add((tile.Key, tile.Value));
            }
        }

        return result;
    }

    // Resident management methods
    public void AddResident(Being being)
    {
        if (!_residents.Contains(being) && _residents.Count < _capacity)
        {
            _residents.Add(being);
        }
    }

    public void RemoveResident(Being being)
    {
        _residents.Remove(being);
    }

    public IReadOnlyList<Being> GetResidents() => _residents.AsReadOnly();

    public bool HasResident(Being being) => _residents.Contains(being);

    // Storage helper methods
    public StorageTrait? GetStorage()
    {
        return Traits.OfType<StorageTrait>().FirstOrDefault();
    }

    public IEnumerable<string> GetFacilities()
    {
        return _facilityPositions.Keys;
    }

    /// <summary>
    /// Get all positions for a given facility type.
    /// </summary>
    /// <param name="facilityId">The facility ID to look up.</param>
    /// <returns>List of relative positions for the facility, or empty list if not found.</returns>
    public List<Vector2I> GetFacilityPositions(string facilityId)
    {
        if (_facilityPositions.TryGetValue(facilityId, out var positions))
        {
            return new List<Vector2I>(positions);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Check if building has a specific facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to check for.</param>
    /// <returns>True if the building has at least one instance of the facility.</returns>
    public bool HasFacility(string facilityId)
    {
        return _facilityPositions.ContainsKey(facilityId) && _facilityPositions[facilityId].Count > 0;
    }

    /// <summary>
    /// Get a walkable position adjacent to a facility.
    /// Returns null if no adjacent walkable position exists.
    /// </summary>
    /// <param name="facilityPosition">The relative position of the facility within the building.</param>
    /// <returns>A relative position that is walkable and adjacent to the facility, or null if none exists.</returns>
    public Vector2I? GetAdjacentWalkablePosition(Vector2I facilityPosition)
    {
        // Cardinal directions to check
        Vector2I[] directions = new[]
        {
            new Vector2I(0, -1),  // Up
            new Vector2I(1, 0),   // Right
            new Vector2I(0, 1),   // Down
            new Vector2I(-1, 0) // Left
        };

        foreach (var direction in directions)
        {
            Vector2I adjacentPos = facilityPosition + direction;

            // Check if this position has a walkable tile in our building
            if (_tiles.TryGetValue(adjacentPos, out var tile) && tile.IsWalkable)
            {
                return adjacentPos;
            }

            // Also check ground tiles (floors are typically in _groundTiles)
            if (_groundTiles.TryGetValue(adjacentPos, out var groundTile) && groundTile.IsWalkable)
            {
                return adjacentPos;
            }
        }

        return null;
    }
}
