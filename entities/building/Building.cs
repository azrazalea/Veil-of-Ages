using System;
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
    private TintableTileMapLayer? _tileMap;
    private TintableTileMapLayer? _groundTileMap;
    private TintableTileMapLayer? _groundBaseTileMap;
    public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; } = [];

    // Dictionary mapping relative grid positions to building tiles
    private readonly Dictionary<Vector2I, BuildingTile> _tiles = [];
    private readonly Dictionary<Vector2I, BuildingTile> _groundTiles = [];
    private readonly Dictionary<Vector2I, BuildingTile> _groundBaseTiles = [];

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

                // Wire up interaction handler from template data
                if (!string.IsNullOrEmpty(facilityData.InteractableType))
                {
                    facility.Interactable = CreateFacilityInteractable(facilityData.InteractableType, facility);
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

        ZIndex = 3;

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position — top-left aligned for TileMapLayer children
        Position = new Vector2(
            _gridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            _gridPosition.Y * VeilOfAges.Grid.Utils.TileSize);
    }

    // Initialize and set up the TileMap layers
    private void InitializeTileMaps(BuildingTemplate template)
    {
        // Structure layer (walls, doors, etc.) — relative ZIndex 0 (effective 3)
        if (_tileMap == null)
        {
            _tileMap = new TintableTileMapLayer
            {
                Visible = true
            };
            AddChild(_tileMap);
            _tileMap.ZAsRelative = true;
        }

        // Floor layer — relative ZIndex -1 (effective 2)
        if (_groundTileMap == null)
        {
            _groundTileMap = new TintableTileMapLayer
            {
                Visible = true
            };
            AddChild(_groundTileMap);
            _groundTileMap.ZIndex = -1;
            _groundTileMap.ZAsRelative = true;
        }

        // Ground base layer — relative ZIndex -2 (effective 1, above terrain)
        if (_groundBaseTileMap == null)
        {
            _groundBaseTileMap = new TintableTileMapLayer
            {
                Visible = true
            };
            AddChild(_groundBaseTileMap);
            _groundBaseTileMap.ZIndex = -2;
            _groundBaseTileMap.ZAsRelative = true;
        }

        // Setup the TileSet with all required atlas sources
        TileResourceManager.Instance.SetupTileSet(_tileMap);
        TileResourceManager.Instance.SetupTileSet(_groundTileMap);
        TileResourceManager.Instance.SetupTileSet(_groundBaseTileMap);
        Log.Print($"Tile set {_tileMap.TileSet}");

        Log.Print($"Building grid position Initialize: {_gridPosition}");

        // Update the actual position — top-left aligned for TileMapLayer children
        Position = new Vector2(
            _gridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            _gridPosition.Y * VeilOfAges.Grid.Utils.TileSize);

        // Create individual tiles from template if provided
        if (template != null)
        {
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
            int sourceId = TileResourceManager.Instance.GetTileSetSourceId("dcss");
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
        if (_tileMap == null || _groundTileMap == null || _groundBaseTileMap == null)
        {
            return;
        }

        // Process each tile in the template
        foreach (var tileData in template.Tiles)
        {
            if (tileData.Type == null || tileData.Material == null)
            {
                continue;
            }

            // Use Category as definition ID when available (e.g., "Chest", "Tombstone"),
            // otherwise fall back to Type (e.g., "Wall", "Floor")
            string tileDefId = !string.IsNullOrEmpty(tileData.Category)
                ? tileData.Category.ToLowerInvariant()
                : tileData.Type.ToLowerInvariant();

            string materialId = tileData.Material.ToLowerInvariant();
            string variantName = string.IsNullOrEmpty(tileData.Variant) ? "Default" : tileData.Variant;

            Vector2I absoluteGridPos = gridPos + tileData.Position;

            // Create the building tile using the resource manager
            var buildingTile = TileResourceManager.Instance.CreateBuildingTile(
                tileDefId,
                tileData.Position,
                this,
                absoluteGridPos,
                materialId,
                variantName);

            if (buildingTile == null)
            {
                var errorMessage = $"Critical Error: Failed to create building tile at position {tileData.Position} with type '{tileDefId}' and material '{materialId}' variant '{variantName}'";
                Log.Error(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }

            // Determine target layer based on explicit Layer field or default routing
            RouteTileToLayer(tileData, buildingTile);

            // Register with grid system
            RegisterTileWithGrid(tileData.Position, buildingTile);

            Log.Print($"{tileDefId} {tileData.Position} tile with source {buildingTile.SourceId} and coords {buildingTile.AtlasCoords}");
        }
    }

    /// <summary>
    /// Routes a tile to the appropriate layer and applies tint if configured.
    /// </summary>
    private void RouteTileToLayer(BuildingTileData tileData, BuildingTile buildingTile)
    {
        // Determine which layer this tile belongs to
        TintableTileMapLayer targetLayer;
        Dictionary<Vector2I, BuildingTile> targetDict;

        string? explicitLayer = tileData.Layer;

        if (string.Equals(explicitLayer, "Ground", System.StringComparison.OrdinalIgnoreCase))
        {
            targetLayer = _groundBaseTileMap!;
            targetDict = _groundBaseTiles;
        }
        else if (string.Equals(explicitLayer, "Floor", System.StringComparison.OrdinalIgnoreCase)
                 || (explicitLayer == null && buildingTile.Type == TileType.Floor))
        {
            targetLayer = _groundTileMap!;
            targetDict = _groundTiles;
        }
        else
        {
            // "Structure" or default for non-Floor types
            targetLayer = _tileMap!;
            targetDict = _tiles;
        }

        // Ensure the atlas tile exists
        var source = (TileSetAtlasSource)targetLayer.TileSet.GetSource(buildingTile.SourceId);
        if (source.GetTileAtCoords(buildingTile.AtlasCoords) == new Vector2I(-1, -1))
        {
            source.CreateTile(buildingTile.AtlasCoords);
        }

        // Add to tile dictionary and set in TileMapLayer
        targetDict[tileData.Position] = buildingTile;
        targetLayer.SetCell(tileData.Position, buildingTile.SourceId, buildingTile.AtlasCoords);

        // Resolve and apply tint cascade: BuildingTileData.Tint > variant Tint > TileDefinition.DefaultTint
        var tintColor = ResolveTileTint(tileData, buildingTile);
        if (tintColor.HasValue)
        {
            targetLayer.SetTileTint(tileData.Position, tintColor.Value);
        }
    }

    /// <summary>
    /// Resolves the tint color for a tile using the cascade:
    /// BuildingTileData.Tint > variant Tint > TileDefinition.DefaultTint > no tint.
    /// </summary>
    private static Color? ResolveTileTint(BuildingTileData tileData, BuildingTile buildingTile)
    {
        // 1. Per-tile template override (highest priority)
        var color = ParseHexColor(tileData.Tint);
        if (color.HasValue)
        {
            return color;
        }

        // 2. Variant-level tint
        color = ParseHexColor(buildingTile.VariantTint);
        if (color.HasValue)
        {
            return color;
        }

        // 3. Tile definition default tint
        color = ParseHexColor(buildingTile.DefinitionDefaultTint);
        if (color.HasValue)
        {
            return color;
        }

        return null;
    }

    /// <summary>
    /// Parses a hex color string (e.g., "#AAAACC") into a Godot Color.
    /// Returns null if the string is null, empty, or invalid.
    /// </summary>
    private static Color? ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return null;
        }

        try
        {
            return new Color(hex);
        }
        catch
        {
            Log.Warn($"Building: Failed to parse hex color '{hex}'");
            return null;
        }
    }

    public override void _ExitTree()
    {
        // When the building is removed, unregister all tiles
        if (GridArea != null)
        {
            foreach (var tileDict in new[] { _tiles, _groundTiles, _groundBaseTiles })
            {
                foreach (var tile in tileDict)
                {
                    Vector2I absolutePos = _gridPosition + tile.Key;
                    if (!tile.Value.IsWalkable)
                    {
                        GridArea.RemoveEntity(absolutePos);
                    }
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

        foreach (var entranceRelative in _entrancePositions)
        {
            Vector2I entranceAbsolute = _gridPosition + entranceRelative;

            foreach (var direction in DirectionUtils.All)
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

        foreach (var entrance in _entrancePositions)
        {
            foreach (var direction in DirectionUtils.All)
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
        if (_tileMap == null || _groundTileMap == null || _groundBaseTileMap == null)
        {
            return;
        }

        // Clear existing cells
        _tileMap.Clear();
        _groundTileMap.Clear();
        _groundBaseTileMap.Clear();

        // Add each tile to the TileMap
        foreach (var tile in _tiles)
        {
            _tileMap.SetCell(tile.Key, tile.Value.SourceId, tile.Value.AtlasCoords);
        }

        foreach (var tile in _groundTiles)
        {
            _groundTileMap.SetCell(tile.Key, tile.Value.SourceId, tile.Value.AtlasCoords);
        }

        foreach (var tile in _groundBaseTiles)
        {
            _groundBaseTileMap.SetCell(tile.Key, tile.Value.SourceId, tile.Value.AtlasCoords);
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

        if (_groundBaseTiles.TryGetValue(relativePos, out var groundBaseTile))
        {
            return groundBaseTile;
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

        // Include all ground base tile positions
        foreach (var tile in _groundBaseTiles)
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

    /// <summary>
    /// Programmatically add a facility to this building.
    /// </summary>
    public void AddFacility(Facility facility)
    {
        if (!_facilities.TryGetValue(facility.Id, out var facilityList))
        {
            facilityList = new List<Facility>();
            _facilities[facility.Id] = facilityList;
        }

        facilityList.Add(facility);
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
    /// Check if the given absolute grid position falls within this building's bounds.
    /// </summary>
    public bool ContainsPosition(Vector2I absolutePos)
    {
        var relativePos = absolutePos - _gridPosition;
        return relativePos.X >= 0 && relativePos.X < GridSize.X &&
               relativePos.Y >= 0 && relativePos.Y < GridSize.Y;
    }

    /// <summary>
    /// Find an interactable facility at the given absolute position.
    /// </summary>
    private IFacilityInteractable? CreateFacilityInteractable(string typeName, Facility facility)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.Name == typeName && typeof(IFacilityInteractable).IsAssignableFrom(type))
                {
                    try
                    {
                        return Activator.CreateInstance(type, facility, this) as IFacilityInteractable;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Building {BuildingName}: Failed to create interactable '{typeName}': {ex.Message}");
                        return null;
                    }
                }
            }
        }

        Log.Error($"Building {BuildingName}: Interactable type '{typeName}' not found");
        return null;
    }

    public IFacilityInteractable? GetInteractableFacilityAt(Vector2I absolutePos)
    {
        var relativePos = absolutePos - _gridPosition;
        foreach (var facilityList in _facilities.Values)
        {
            foreach (var facility in facilityList)
            {
                if (facility.Interactable != null && facility.Positions.Contains(relativePos))
                {
                    return facility.Interactable;
                }
            }
        }

        return null;
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
    /// Check if an entity position is adjacent to a facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to check adjacency against.</param>
    /// <param name="entityPosition">The absolute grid position of the entity.</param>
    /// <returns>True if the entity is adjacent to any instance of the facility, false if no such facility exists or entity is not adjacent.</returns>
    public bool IsAdjacentToFacility(string facilityId, Vector2I entityPosition)
    {
        var positions = GetFacilityPositions(facilityId);
        if (positions.Count == 0)
        {
            // No facility defined - fall back to building adjacency
            return true;
        }

        // Check if entity is adjacent to any facility position (including diagonals)
        foreach (var relativePos in positions)
        {
            Vector2I absolutePos = _gridPosition + relativePos;

            // Check if entity is at the facility position or adjacent to it
            if (entityPosition == absolutePos)
            {
                return true;
            }

            foreach (var direction in DirectionUtils.All)
            {
                if (entityPosition == absolutePos + direction)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if an entity position is adjacent to the storage facility.
    /// Used when the storage facility has RequireAdjacent set to true.
    /// </summary>
    /// <param name="entityPosition">The absolute grid position of the entity.</param>
    /// <returns>True if the entity is adjacent to any storage facility, false if no storage facility exists or entity is not adjacent.</returns>
    public bool IsAdjacentToStorageFacility(Vector2I entityPosition)
    {
        return IsAdjacentToFacility("storage", entityPosition);
    }

    /// <summary>
    /// Get a walkable position adjacent to a facility.
    /// Returns null if no adjacent walkable position exists.
    /// </summary>
    /// <param name="facilityPosition">The relative position of the facility within the building.</param>
    /// <returns>A relative position that is walkable and adjacent to the facility, or null if none exists.</returns>
    public Vector2I? GetAdjacentWalkablePosition(Vector2I facilityPosition)
    {
        foreach (var direction in DirectionUtils.All)
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

            // Also check ground base tiles
            if (_groundBaseTiles.TryGetValue(adjacentPos, out var groundBaseTile) && groundBaseTile.IsWalkable)
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
