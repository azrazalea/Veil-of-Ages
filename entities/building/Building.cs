using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;
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

    /// <summary>
    /// Gets a value indicating whether buildings are never walkable — their wall tiles block pathfinding.
    /// </summary>
    public bool IsWalkable => false;

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

    // Rooms detected via flood fill of walkable interior positions
    private readonly List<Room> _rooms = [];

    /// <summary>
    /// Gets all rooms detected in this building.
    /// </summary>
    public IReadOnlyList<Room> Rooms => _rooms;

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

            // Track which (DecorationId, Position) pairs are owned by facilities
            // so we can skip duplicate decorations
            var facilityOwnedDecorations = new HashSet<(string Id, Vector2I Position)>();

            // Populate facilities from template
            foreach (var facilityData in template.Facilities)
            {
                // Create Facility instance from template data
                var facility = new Facility(
                    facilityData.Id,
                    facilityData.Positions,
                    facilityData.RequireAdjacent,
                    this)
                {
                    // Set walkability from template
                    IsWalkable = facilityData.IsWalkable,
                    GridArea = GridArea
                };

                // Set absolute grid position of primary tile
                if (facilityData.Positions.Count > 0)
                {
                    facility.SetGridPosition(gridPos + facilityData.Positions[0]);
                }

                // If facility has storage configuration, create StorageTrait for it
                if (facilityData.Storage != null)
                {
                    var storageTrait = new StorageTrait(
                        facilityData.Storage.VolumeCapacity,
                        facilityData.Storage.WeightCapacity,
                        facilityData.Storage.DecayRateModifier,
                        fetchDuration: facilityData.Storage.FetchDuration);
                    facility.SelfAsEntity().AddTrait(storageTrait, 0);

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

                // Initialize visual sprite if facility has a DecorationId
                if (!string.IsNullOrEmpty(facilityData.DecorationId))
                {
                    var decorationDef = TileResourceManager.Instance.GetDecorationDefinition(facilityData.DecorationId);
                    if (decorationDef != null && facilityData.Positions.Count > 0)
                    {
                        facility.InitializeVisual(decorationDef, facilityData.Positions[0], facilityData.PixelOffset);
                        facility.ZIndex = 1; // Above floor tiles
                        AddChild(facility);

                        // Track this so we skip the duplicate decoration
                        facilityOwnedDecorations.Add((facilityData.DecorationId, facilityData.Positions[0]));
                    }
                    else if (decorationDef == null)
                    {
                        Log.Warn($"Building {template.Name}: Decoration definition '{facilityData.DecorationId}' not found for facility '{facilityData.Id}'");
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

            // Initialize and setup TileMap (must happen before entity registration,
            // because SetGroundCell resets A* solid state for floor tiles)
            InitializeTileMaps(template);

            // Register facilities as grid entities (AFTER tiles are placed so SetGroundCell
            // doesn't overwrite their solid state). AddEntity handles walkability marking.
            foreach (var facilityList in _facilities.Values)
            {
                foreach (var facility in facilityList)
                {
                    foreach (var absolutePos in facility.GetAbsolutePositions())
                    {
                        GridArea.AddEntity(absolutePos, facility);
                    }
                }
            }

            // Create decoration sprites from template (skipping those now owned by facilities)
            foreach (var decorationPlacement in template.Decorations)
            {
                // Skip decorations that are now rendered by a facility
                if (facilityOwnedDecorations.Contains((decorationPlacement.Id, decorationPlacement.Position)))
                {
                    continue;
                }

                var decorationDef = TileResourceManager.Instance.GetDecorationDefinition(decorationPlacement.Id);
                if (decorationDef == null)
                {
                    Log.Warn($"Building {template.Name}: Decoration definition '{decorationPlacement.Id}' not found");
                    continue;
                }

                var decoration = new Decoration();
                decoration.Initialize(decorationDef, decorationPlacement.Position,
                    decorationPlacement.PixelOffset, decorationPlacement.IsWalkable,
                    decorationPlacement.AdditionalPositions);
                decoration.GridArea = GridArea;
                decoration.ZIndex = 1; // Above floor tiles
                AddChild(decoration);
                _decorations.Add(decoration);

                // Register decoration as grid entity for each position it occupies
                foreach (var relativePos in decoration.AllPositions)
                {
                    var absolutePos = _gridPosition + relativePos;
                    decoration.AbsoluteGridPosition = absolutePos;
                    GridArea.AddEntity(absolutePos, decoration);
                }
            }
        }

        // Detect rooms via flood fill after all tiles, facilities, and decorations are placed
        if (template != null)
        {
            DetectRooms(template);
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

            // Unregister facilities from the entity grid
            foreach (var facilityList in _facilities.Values)
            {
                foreach (var facility in facilityList)
                {
                    foreach (var absolutePos in facility.GetAbsolutePositions())
                    {
                        GridArea.RemoveEntity(absolutePos);
                    }
                }
            }

            // Unregister decorations from the entity grid
            foreach (var decoration in _decorations)
            {
                foreach (var relativePos in decoration.AllPositions)
                {
                    var absolutePos = _gridPosition + relativePos;
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

    // --- Room accessors ---

    /// <summary>
    /// Gets the default (first) room in this building, or null if no rooms detected.
    /// For current single-room buildings, this returns the only room.
    /// </summary>
    public Room? GetDefaultRoom() => _rooms.Count > 0 ? _rooms[0] : null;

    /// <summary>
    /// Gets the room containing the given relative position, or null.
    /// </summary>
    public Room? GetRoomAtRelativePosition(Vector2I relativePos)
    {
        foreach (var room in _rooms)
        {
            if (room.ContainsRelativePosition(relativePos))
            {
                return room;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the room containing the given absolute position, or null.
    /// </summary>
    public Room? GetRoomAtAbsolutePosition(Vector2I absolutePos)
    {
        return GetRoomAtRelativePosition(absolutePos - _gridPosition);
    }

    // --- Room detection via flood fill ---

    /// <summary>
    /// Detect rooms by flood-filling walkable interior positions.
    /// Each connected region of non-wall tiles = one room.
    /// Template RoomData hints are matched by position overlap.
    /// Facilities and decorations are assigned to their containing room.
    /// </summary>
    private void DetectRooms(BuildingTemplate template)
    {
        // Collect all non-boundary positions within building bounds.
        // Boundary = non-walkable structure tiles (walls, fences, windows).
        // Interior = walkable structure tiles (doors, gates) + all floor/ground positions.
        var interiorPositions = new HashSet<Vector2I>();

        // Floor tiles are always interior
        foreach (var pos in _groundTiles.Keys)
        {
            interiorPositions.Add(pos);
        }

        foreach (var pos in _groundBaseTiles.Keys)
        {
            interiorPositions.Add(pos);
        }

        // Structure tiles: walkable ones (doors, gates) are interior, non-walkable (walls) are not
        foreach (var (pos, tile) in _tiles)
        {
            if (tile.IsWalkable)
            {
                interiorPositions.Add(pos);
            }
        }

        // Also include facility positions even if they're non-walkable (oven is IN the room)
        foreach (var facilityList in _facilities.Values)
        {
            foreach (var facility in facilityList)
            {
                foreach (var pos in facility.Positions)
                {
                    interiorPositions.Add(pos);
                }
            }
        }

        // Also include decoration positions
        foreach (var decoration in _decorations)
        {
            foreach (var pos in decoration.AllPositions)
            {
                interiorPositions.Add(pos);
            }
        }

        // Remove entrance positions from room tiles (entrances are thresholds, not room interior)
        foreach (var entrance in _entrancePositions)
        {
            interiorPositions.Remove(entrance);
        }

        // Flood fill to find connected regions
        var visited = new HashSet<Vector2I>();
        int roomIndex = 0;

        foreach (var startPos in interiorPositions)
        {
            if (visited.Contains(startPos))
            {
                continue;
            }

            // BFS flood fill from this position
            var roomTiles = new HashSet<Vector2I>();
            var queue = new Queue<Vector2I>();
            queue.Enqueue(startPos);
            visited.Add(startPos);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                roomTiles.Add(current);

                // Check 4 neighbors
                Vector2I[] neighbors =
                [
                    current + Vector2I.Up,
                    current + Vector2I.Down,
                    current + Vector2I.Left,
                    current + Vector2I.Right
                ];

                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor) && interiorPositions.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Create room from this connected region
            var room = new Room($"room_{roomIndex}", this, roomTiles)
            {
                GridArea = GridArea
            };
            _rooms.Add(room);
            roomIndex++;
        }

        // Match rooms to template RoomData hints by position overlap
        if (template.Rooms.Count > 0)
        {
            MatchRoomHints(template.Rooms);
        }
        else if (_rooms.Count == 1)
        {
            // No hints, single room — give it a default name based on building type
            _rooms[0].Name = BuildingName;
        }

        // Assign facilities to their containing rooms
        foreach (var facilityList in _facilities.Values)
        {
            foreach (var facility in facilityList)
            {
                if (facility.Positions.Count > 0)
                {
                    var room = GetRoomAtRelativePosition(facility.Positions[0]);
                    room?.AddFacility(facility);
                }
            }
        }

        // Assign decorations to their containing rooms
        foreach (var decoration in _decorations)
        {
            var room = GetRoomAtRelativePosition(decoration.GridPosition);
            room?.AddDecoration(decoration);
        }

        Log.Print($"Building '{BuildingName}': Detected {_rooms.Count} room(s)");
    }

    /// <summary>
    /// Match detected rooms to template RoomData hints by bounding box overlap.
    /// Each hint is matched to the room whose tiles overlap most with the hint's bounding box.
    /// </summary>
    private void MatchRoomHints(List<RoomData> hints)
    {
        foreach (var hint in hints)
        {
            Room? bestMatch = null;
            int bestOverlap = 0;

            foreach (var room in _rooms)
            {
                // Count how many of the room's tiles fall within the hint's bounding box
                int overlap = 0;
                foreach (var tile in room.Tiles)
                {
                    if (tile.X >= hint.TopLeft.X && tile.X < hint.TopLeft.X + hint.Size.X &&
                        tile.Y >= hint.TopLeft.Y && tile.Y < hint.TopLeft.Y + hint.Size.Y)
                    {
                        overlap++;
                    }
                }

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestMatch = room;
                }
            }

            if (bestMatch != null)
            {
                if (hint.Name != null)
                {
                    bestMatch.Name = hint.Name;
                }

                if (hint.Purpose != null)
                {
                    bestMatch.Purpose = hint.Purpose;
                }

                bestMatch.IsSecret = hint.IsSecret;
            }
        }
    }

    /// <summary>
    /// Add a facility to this building's internal dictionary.
    /// Used during Initialize() to register template-defined facilities.
    /// </summary>
    private void AddFacility(Facility facility)
    {
        if (!_facilities.TryGetValue(facility.Id, out var facilityList))
        {
            facilityList = new List<Facility>();
            _facilities[facility.Id] = facilityList;
        }

        facilityList.Add(facility);
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
    /// Create an interactable handler by type name using reflection.
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
                        return Activator.CreateInstance(type, facility) as IFacilityInteractable;
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

        var storage = _regenerationFacility.SelfAsEntity().GetTrait<StorageTrait>();
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
