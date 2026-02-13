using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities;

public partial class Building : Node2D, IEntity<Trait>, IBlocksPathfinding
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

    // Decoration sprites (purely visual, divorced from tile system)
    private readonly List<Decoration> _decorations = [];
    public IReadOnlyList<Decoration> Decorations => _decorations;

    // List of entrance positions (relative to building origin)
    private List<Vector2I> _entrancePositions = [];

    // For tracking residents/workers
    private int _capacity;
    private int _occupants;

    // Residents tracking
    private readonly List<Being> _residents = [];

    // Facilities (keyed by facility ID, with list of Facility instances for each)
    private readonly Dictionary<string, List<Facility>> _facilities = new ();

    // Reference to the storage facility (if any) for regeneration
    private Facility? _regenerationFacility;

    // Regeneration configuration (for buildings like wells that produce resources)
    private string? _regenerationItem;
    private float _regenerationRate;
    private int _regenerationMaxQuantity;
    private float _regenerationProgress;

    public override void _Ready()
    {
        Log.Print($"Building grid position _Ready: {_gridPosition}");

        // Snap to grid — use top-left alignment (not center) so child TileMapLayers render correctly
        Position = new Vector2(
            _gridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            _gridPosition.Y * VeilOfAges.Grid.Utils.TileSize);

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

            // Populate facilities from template
            foreach (var facilityData in template.Facilities)
            {
                // Create Facility instance from template data
                var facility = new Facility(
                    facilityData.Id,
                    facilityData.Positions,
                    facilityData.RequireAdjacent,
                    this);

                // If facility has storage configuration, create StorageTrait for it
                if (facilityData.Storage != null)
                {
                    var storageTrait = new StorageTrait(
                        facilityData.Storage.VolumeCapacity,
                        facilityData.Storage.WeightCapacity,
                        facilityData.Storage.DecayRateModifier,
                        fetchDuration: facilityData.Storage.FetchDuration);
                    facility.AddTrait(storageTrait);

                    // Handle regeneration configuration
                    if (!string.IsNullOrEmpty(facilityData.Storage.RegenerationItem))
                    {
                        _regenerationItem = facilityData.Storage.RegenerationItem;
                        _regenerationRate = facilityData.Storage.RegenerationRate;
                        _regenerationMaxQuantity = facilityData.Storage.RegenerationMaxQuantity;
                        _regenerationFacility = facility;

                        // Add initial items if configured
                        if (facilityData.Storage.RegenerationInitialQuantity > 0)
                        {
                            var itemDef = ItemResourceManager.Instance.GetDefinition(facilityData.Storage.RegenerationItem);
                            if (itemDef != null)
                            {
                                var initialItem = new Item(itemDef, facilityData.Storage.RegenerationInitialQuantity);
                                storageTrait.AddItem(initialItem);
                            }
                            else
                            {
                                Log.Warn($"Building {template.Name}: Regeneration item '{facilityData.Storage.RegenerationItem}' not found");
                            }
                        }
                    }
                }

                // Add facility to dictionary
                if (!_facilities.TryGetValue(facility.Id, out var facilityList))
                {
                    facilityList = new List<Facility>();
                    _facilities[facility.Id] = facilityList;
                }

                facilityList.Add(facility);
            }

            // Initialize and setup TileMap
            InitializeTileMaps(template);

            // Create decoration sprites from template
            foreach (var decorationPlacement in template.Decorations)
            {
                var decorationDef = TileResourceManager.Instance.GetDecorationDefinition(decorationPlacement.Id);
                if (decorationDef == null)
                {
                    Log.Warn($"Building {template.Name}: Decoration definition '{decorationPlacement.Id}' not found");
                    continue;
                }

                var decoration = new Decoration();
                decoration.Initialize(decorationDef, decorationPlacement.Position, decorationPlacement.PixelOffset);
                decoration.ZIndex = 1; // Above floor tiles
                AddChild(decoration);
                _decorations.Add(decoration);
            }
        }

        ZIndex = 2;

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position — top-left aligned for TileMapLayer children
        Position = new Vector2(
            _gridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            _gridPosition.Y * VeilOfAges.Grid.Utils.TileSize);
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
            _tileMap.ZAsRelative = true;
        }

        if (_groundTileMap == null)
        {
            _groundTileMap = new TileMapLayer
            {
                Visible = true
            };
            AddChild(_groundTileMap);
            _groundTileMap.ZIndex = _tileMap.ZIndex - 1;
            _groundTileMap.ZAsRelative = true;
        }

        // Setup the TileSet with all required atlas sources
        TileResourceManager.Instance.SetupTileSet(_tileMap);
        TileResourceManager.Instance.SetupTileSet(_groundTileMap);
        Log.Print($"Tile set {_tileMap.TileSet}");

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position — top-left aligned for TileMapLayer children
        Position = new Vector2(
            _gridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            _gridPosition.Y * VeilOfAges.Grid.Utils.TileSize);

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
            // Uses a distinctive DCSS tile so it's obvious when this fallback path fires
            int sourceId = TileResourceManager.Instance.GetTileSetSourceId("dcss_utumno");
            GridArea.SetGroundCell(absolutePos, new VeilOfAges.Grid.Tile(
                sourceId,
                new Vector2I(39, 50),
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

            // Use Category as definition ID when available (e.g., "Chest", "Tombstone"),
            // otherwise fall back to Type (e.g., "Wall", "Floor")
            string tileDefId = !string.IsNullOrEmpty(tileData.Category)
                ? tileData.Category.ToLowerInvariant()
                : tileData.Type.ToLowerInvariant();

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
                variantName);

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
        _groundTileMap.Clear();

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

        if (_groundTiles.TryGetValue(relativePos, out var groundTile))
        {
            return groundTile;
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

        // Include all tile positions (walls, doors, etc.)
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
    /// Checks terrain/building walkability via the A* grid (NOT entity occupancy).
    /// Use for pathfinding destinations.
    /// Returns relative positions to building origin.
    /// </summary>
    /// <remarks>
    /// This method intentionally does NOT check entity occupancy. Entities should not
    /// have "god knowledge" of where all other entities are standing. The blocking
    /// response system handles dynamic entity collisions at runtime.
    /// </remarks>
    /// <param name="excludeEntranceAdjacent">If true, also excludes positions adjacent to entrances (default: true).</param>
    public List<Vector2I> GetWalkableInteriorPositions(bool excludeEntranceAdjacent = true)
    {
        var result = new List<Vector2I>();

        if (GridArea?.AStarGrid == null)
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

                // Use A* grid's IsPointSolid which only checks terrain/buildings, NOT entity occupancy.
                // A solid point means the terrain itself is unwalkable (wall, water, etc.)
                bool isTerrainWalkable = GridArea.AStarGrid.IsInBoundsv(absolutePos) &&
                                         !GridArea.AStarGrid.IsPointSolid(absolutePos);

                // Exclude entrance positions so entities don't target doorways
                // Also exclude positions adjacent to entrances to prevent doorway blocking
                if (isTerrainWalkable &&
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
        if (being != null && !_residents.Contains(being) && _residents.Count < _capacity)
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

    /// <summary>
    /// Gets the primary storage for this building.
    /// First looks for a facility with id "storage", then any facility with StorageTrait.
    /// </summary>
    /// <returns>The StorageTrait if found, null otherwise.</returns>
    public StorageTrait? GetStorage()
    {
        // First try facility with id "storage"
        if (_facilities.TryGetValue("storage", out var storageFacilities) && storageFacilities.Count > 0)
        {
            var storage = storageFacilities[0].GetTrait<StorageTrait>();
            if (storage != null)
            {
                return storage;
            }
        }

        // Then try any facility that has StorageTrait
        foreach (var facilityList in _facilities.Values)
        {
            foreach (var facility in facilityList)
            {
                var storage = facility.GetTrait<StorageTrait>();
                if (storage != null)
                {
                    return storage;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets standing orders from this building's GranaryTrait, if it has one.
    /// Used by distributors to determine what to deliver where.
    /// </summary>
    /// <returns>The StandingOrders if this is a granary, null otherwise.</returns>
    public StandingOrders? GetStandingOrders()
    {
        var granaryTrait = Traits.OfType<GranaryTrait>().FirstOrDefault();
        return granaryTrait?.Orders;
    }

    /// <summary>
    /// Produces an item into this building's own storage.
    /// This is the building adding to itself - NOT remote storage access.
    /// THREAD SAFETY: Only call from main thread (ProcessRegeneration, Initialize, Action.Execute).
    /// For entity-initiated production, use ProduceToStorageAction instead.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to produce (e.g., "wheat").</param>
    /// <param name="quantity">How many to produce.</param>
    /// <returns>True if item was added to storage, false if no storage or storage full.</returns>
    public bool ProduceItem(string itemDefId, int quantity = 1)
    {
        var storage = GetStorage();
        if (storage == null)
        {
            return false;
        }

        var itemDef = ItemResourceManager.Instance.GetDefinition(itemDefId);
        if (itemDef == null)
        {
            Log.Warn($"Building {BuildingName}: Cannot produce item '{itemDefId}' - definition not found");
            return false;
        }

        var item = new Item(itemDef, quantity);
        return storage.AddItem(item);
    }

    public IEnumerable<string> GetFacilityIds()
    {
        return _facilities.Keys;
    }

    /// <summary>
    /// Get the first facility with the specified ID.
    /// </summary>
    /// <param name="facilityId">The facility ID to look up.</param>
    /// <returns>The first Facility with the given ID, or null if not found.</returns>
    public Facility? GetFacility(string facilityId)
    {
        if (_facilities.TryGetValue(facilityId, out var facilityList) && facilityList.Count > 0)
        {
            return facilityList[0];
        }

        return null;
    }

    /// <summary>
    /// Get all facilities with the specified ID.
    /// </summary>
    /// <param name="facilityId">The facility ID to look up.</param>
    /// <returns>List of Facility instances, or empty list if not found.</returns>
    public List<Facility> GetFacilities(string facilityId)
    {
        if (_facilities.TryGetValue(facilityId, out var facilityList))
        {
            return new List<Facility>(facilityList);
        }

        return new List<Facility>();
    }

    /// <summary>
    /// Get the storage from a specific facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to look up.</param>
    /// <returns>The StorageTrait from the first matching facility, or null if not found.</returns>
    public StorageTrait? GetFacilityStorage(string facilityId)
    {
        var facility = GetFacility(facilityId);
        return facility?.GetTrait<StorageTrait>();
    }

    /// <summary>
    /// Get all positions for a given facility type.
    /// </summary>
    /// <param name="facilityId">The facility ID to look up.</param>
    /// <returns>List of relative positions for the facility, or empty list if not found.</returns>
    public List<Vector2I> GetFacilityPositions(string facilityId)
    {
        if (_facilities.TryGetValue(facilityId, out var facilityList))
        {
            return facilityList.SelectMany(f => f.Positions).ToList();
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
        return _facilities.ContainsKey(facilityId) && _facilities[facilityId].Count > 0;
    }

    /// <summary>
    /// Get the fetch duration for retrieving items from this building's storage.
    /// Returns the FetchDuration from the storage's StorageTrait, or 0 for instant access.
    /// </summary>
    /// <returns>Fetch duration in ticks, or 0 for instant access.</returns>
    public uint GetStorageFetchDuration()
    {
        return GetStorage()?.FetchDuration ?? 0;
    }

    /// <summary>
    /// Gets the position an entity should navigate to for storage access.
    /// If the storage facility has RequireAdjacent set, returns a walkable position adjacent to the storage facility.
    /// Otherwise returns the building entrance position.
    /// </summary>
    /// <returns>The absolute grid position to navigate to, or null if no valid position exists.</returns>
    public Vector2I? GetStorageAccessPosition()
    {
        // Check if storage facility requires adjacent positioning
        var storageFacilities = GetFacilities("storage");
        if (storageFacilities.Count > 0 && storageFacilities[0].RequireAdjacent)
        {
            // Find a storage facility position that has an adjacent walkable tile
            foreach (var facility in storageFacilities)
            {
                foreach (var pos in facility.Positions)
                {
                    Vector2I? adjacentPos = GetAdjacentWalkablePosition(pos);
                    if (adjacentPos.HasValue)
                    {
                        // Return absolute position
                        return _gridPosition + adjacentPos.Value;
                    }
                }
            }

            // No walkable position adjacent to storage facility
            return null;
        }

        // Default: return first entrance position
        var entrances = GetEntrancePositions();
        if (entrances.Count > 0)
        {
            return entrances[0];
        }

        // Fallback: return building origin
        return _gridPosition;
    }

    /// <summary>
    /// Checks if this building requires navigating to a specific storage facility position
    /// rather than just being adjacent to the building.
    /// </summary>
    /// <returns>True if navigation should target the storage facility position.</returns>
    public bool RequiresStorageFacilityNavigation()
    {
        // Check if storage facility requires adjacent positioning
        var storageFacilities = GetFacilities("storage");
        if (storageFacilities.Count == 0 || !storageFacilities[0].RequireAdjacent)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if an entity position is adjacent to the storage facility.
    /// Used when the storage facility has RequireAdjacent set to true.
    /// </summary>
    /// <param name="entityPosition">The absolute grid position of the entity.</param>
    /// <returns>True if the entity is adjacent to any storage facility, false if no storage facility exists or entity is not adjacent.</returns>
    public bool IsAdjacentToStorageFacility(Vector2I entityPosition)
    {
        // Get all storage facility positions
        var storagePositions = GetFacilityPositions("storage");
        if (storagePositions.Count == 0)
        {
            // No storage facility defined - fall back to building adjacency
            return true;
        }

        // Cardinal directions for adjacency check
        Vector2I[] directions =
        [
            new Vector2I(0, -1),  // Up
            new Vector2I(1, 0),   // Right
            new Vector2I(0, 1),   // Down
            new Vector2I(-1, 0) // Left
        ];

        // Check if entity is adjacent to any storage facility
        foreach (var relativeStoragePos in storagePositions)
        {
            Vector2I absoluteStoragePos = _gridPosition + relativeStoragePos;

            // Check if entity is at the storage position or adjacent to it
            if (entityPosition == absoluteStoragePos)
            {
                return true;
            }

            foreach (var direction in directions)
            {
                if (entityPosition == absoluteStoragePos + direction)
                {
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>
    /// Process resource regeneration for this building (e.g., wells regenerating water).
    /// Called periodically during decay processing.
    /// </summary>
    /// <param name="tickMultiplier">Number of ticks since last processing.</param>
    public void ProcessRegeneration(int tickMultiplier)
    {
        // Skip if no regeneration configured
        if (string.IsNullOrEmpty(_regenerationItem) || _regenerationRate <= 0 || _regenerationFacility == null)
        {
            return;
        }

        var storage = _regenerationFacility.GetTrait<StorageTrait>();
        if (storage == null)
        {
            return;
        }

        // Check current quantity of regeneration item
        int currentQuantity = storage.GetItemCount(_regenerationItem);
        if (currentQuantity >= _regenerationMaxQuantity)
        {
            _regenerationProgress = 0f;
            return;
        }

        // Accumulate regeneration progress
        _regenerationProgress += _regenerationRate * tickMultiplier;

        // Add items when progress reaches 1.0 or more
        if (_regenerationProgress >= 1.0f)
        {
            int unitsToAdd = (int)_regenerationProgress;
            _regenerationProgress -= unitsToAdd;

            // Don't exceed max quantity
            int spaceAvailable = _regenerationMaxQuantity - currentQuantity;
            unitsToAdd = System.Math.Min(unitsToAdd, spaceAvailable);

            if (unitsToAdd > 0)
            {
                var itemDef = ItemResourceManager.Instance.GetDefinition(_regenerationItem);
                if (itemDef != null)
                {
                    var newItem = new Item(itemDef, unitsToAdd);
                    storage.AddItem(newItem);
                }
            }
        }
    }
}
