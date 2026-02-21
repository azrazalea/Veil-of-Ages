using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

namespace VeilOfAges.Grid;

/// <summary>
/// Handles the graphics display of an area.
/// </summary>
public partial class Area(Vector2I worldSize): Node2D
{
    [Export]
    public string AreaName { get; set; } = "area.VILLAGE";

    /// <summary>
    /// Gets localized display name for this area. AreaName should be a translation key.
    /// </summary>
    public string AreaDisplayName => L.Tr(AreaName);
    public Vector2I GridSize { get; set; } = new (worldSize.X, worldSize.Y);
    public AStarGrid2D? AStarGrid;

    private TileMapLayer? _groundLayer;
    private TileMapLayer? _objectsLayer;
    private readonly GroundSystem _groundGridSystem = new (worldSize);

    // TODO: We need to properly implement items and make this
    // a proper object that has a texture assoiciated with it
    private readonly GroundSystem _objectGridSystem = new (worldSize);
    public Node2DSystem EntitiesGridSystem { get; private set; } = new Node2DSystem(worldSize);

    private Node2D? _entitiesContainer;

    /// <summary>
    /// Is area in full detail mode with all AI active?.
    /// </summary>
    private bool _isActive;

    /// <summary>
    /// Is this the area the player is in currently?.
    /// </summary>
    private bool _isPlayerArea;
    private uint _beingNum;
    public List<Node2D> Entities { get; private set; } = [];

    // Terrain tiles loaded from JSON via TileResourceManager at runtime.
    // Definitions live in resources/tiles/terrain/*.json.
    private static Tile? _waterTile;
    public static Tile WaterTile => _waterTile ??= TileResourceManager.Instance.GetTerrainTile("water")
        ?? throw new InvalidOperationException("Terrain tile definition 'water' not found");

    private static Tile? _grassTile;
    public static Tile GrassTile => _grassTile ??= TileResourceManager.Instance.GetTerrainTile("grass")
        ?? throw new InvalidOperationException("Terrain tile definition 'grass' not found");

    private static Tile? _dirtTile;
    public static Tile DirtTile => _dirtTile ??= TileResourceManager.Instance.GetTerrainTile("dirt")
        ?? throw new InvalidOperationException("Terrain tile definition 'dirt' not found");

    private static Tile? _pathTile;
    public static Tile PathTile => _pathTile ??= TileResourceManager.Instance.GetTerrainTile("path")
        ?? throw new InvalidOperationException("Terrain tile definition 'path' not found");

    private World? _gameWorld;

    // Transition points in this area
    private readonly List<TransitionPoint> _transitionPoints = [];
    public IReadOnlyList<TransitionPoint> TransitionPoints => _transitionPoints;

    public void AddTransitionPoint(TransitionPoint point) => _transitionPoints.Add(point);

    public TransitionPoint? GetTransitionPointAt(Vector2I position)
        => _transitionPoints.FirstOrDefault(tp => tp.SourcePosition == position);

    public void SetActive()
    {
        _isActive = true;
    }

    public override void _Ready()
    {
        base._Ready();

        // Get TileSet from the main ground layer, or from TileResourceManager for programmatic areas
        var worldGroundLayer = GetNodeOrNull<TileMapLayer>("/root/World/GroundLayer");
        var tileSet = worldGroundLayer?.TileSet ?? TileResourceManager.Instance?.GetTileSet();
        _groundLayer = new TileMapLayer
        {
            TileSet = tileSet
        };
        _objectsLayer = new TileMapLayer
        {
            ZIndex = 5
        };
        AStarGrid = PathFinder.CreateNewAStarGrid(this);
    }

    public void MakePlayerArea(Player player, Vector2I playerStartingLocation)
    {
        // Disable existing player GridArea
        var playerArea = GetNode<Node>("/root/World/GridAreas/PlayerArea");

        foreach (TileMapLayer child in playerArea.GetChildren().Cast<TileMapLayer>())
        {
            playerArea.RemoveChild(child);
            child.Enabled = false;
        }

        // Add scene items to Tile Layers
        PopulateLayersFromGrid();

        // Add our tile layers to scene tree and enable
        playerArea.AddChild(_groundLayer);
        playerArea.AddChild(_objectsLayer);

        if (_groundLayer != null)
        {
            _groundLayer.Enabled = true;
        }

        if (_objectsLayer != null)
        {
            _objectsLayer.Enabled = true;
        }

        // Declare we are active
        _isActive = true;
        _isPlayerArea = true;
        player.Position = Utils.GridToWorld(playerStartingLocation);
    }

    public bool HasBeings()
    {
        return _beingNum > 0;
    }

    /// <summary>
    /// Set A* walkability and weight for a position without painting the ground TileMapLayer visual.
    /// Used by walkable StructuralEntities (floors) that render via their own Sprite2D nodes.
    /// </summary>
    public void SetGroundWalkability(Vector2I pos, bool walkable, float weight = 1.0f)
    {
        var entityAtPos = EntitiesGridSystem.GetCell(pos);
        bool hasBlockingEntity = entityAtPos is IEntity { IsWalkable: false };
        AStarGrid?.SetPointSolid(pos, !walkable || hasBlockingEntity);
        AStarGrid?.SetPointWeightScale(pos, weight);
    }

    public void SetGroundCell(Vector2I groundPos, Tile tile)
    {
        _groundGridSystem.SetCell(groundPos, tile);

        // Only mark solid if terrain is unwalkable OR a non-walkable entity is here
        var entityAtPos = EntitiesGridSystem.GetCell(groundPos);
        bool hasBlockingEntity = entityAtPos is IEntity { IsWalkable: false };
        AStarGrid?.SetPointSolid(groundPos, !tile.IsWalkable || hasBlockingEntity);
        AStarGrid?.SetPointWeightScale(groundPos, tile.WalkDifficulty);

        if (_groundLayer?.Enabled == true)
        {
            // Godot requires tiles be explicitly created in the atlas source before use
            var source = (TileSetAtlasSource)_groundLayer.TileSet.GetSource(tile.SourceId);
            if (source.GetTileAtCoords(tile.AtlasCoords) == new Vector2I(-1, -1))
            {
                source.CreateTile(tile.AtlasCoords);
            }

            _groundLayer.SetCell(groundPos, tile.SourceId, tile.AtlasCoords);
        }
    }

    public void AddEntity(Vector2I entityPos, Node2D entity, Vector2I? entitySize = null)
    {
        if (entity is Being)
        {
            _beingNum++;
        }

        Entities.Add(entity);

        // Mark non-walkable entities as solid in the A* grid.
        // Beings are walkable (dynamic collision handled at runtime), so they don't block pathing.
        bool shouldMarkSolid = entity is IEntity { IsWalkable: false };

        if (entitySize is Vector2I size)
        {
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    var pos = new Vector2I(entityPos.X + x, entityPos.Y + y);
                    EntitiesGridSystem.SetCell(pos, entity);
                    if (shouldMarkSolid)
                    {
                        AStarGrid?.SetPointSolid(pos, true);
                    }
                }
            }
        }
        else
        {
            if (shouldMarkSolid)
            {
                AStarGrid?.SetPointSolid(entityPos, true);
            }

            EntitiesGridSystem.SetCell(entityPos, entity);
        }
    }

    public void RemoveEntity(Vector2I entityPos, Vector2I? entitySize = null)
    {
        var foundEntity = EntitiesGridSystem.GetCell(entityPos);

        if (foundEntity == null)
        {
            return; // No actual entity
        }

        if (foundEntity is Being)
        {
            _beingNum--;
        }

        Entities.Remove(foundEntity);

        // Only non-walkable entities were marked solid, so only unmark them
        bool shouldUnmarkSolid = foundEntity is IEntity { IsWalkable: false };

        if (entitySize is Vector2I size)
        {
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    var pos = new Vector2I(entityPos.X + x, entityPos.Y + y);
                    EntitiesGridSystem.RemoveCell(new Vector2I(entityPos.X + x, entityPos.Y + y));
                    if (shouldUnmarkSolid)
                    {
                        AStarGrid?.SetPointSolid(pos, false);
                    }
                }
            }
        }
        else
        {
            if (shouldUnmarkSolid)
            {
                AStarGrid?.SetPointSolid(entityPos, false);
            }

            EntitiesGridSystem.RemoveCell(entityPos);
        }
    }

    public bool IsCellWalkable(Vector2I gridPos)
    {
        var entityAtPos = EntitiesGridSystem.GetCell(gridPos);
        bool hasBlockingEntity = entityAtPos is IEntity { IsWalkable: false };
        var groundTile = _groundGridSystem.GetCell(gridPos);
        bool groundWalkable = groundTile?.IsWalkable ?? false;

        return !hasBlockingEntity && groundWalkable;
    }

    /// <summary>
    /// Check if a cell can be physically moved into at runtime.
    /// Unlike IsCellWalkable (for A* where Beings are walkable), this method
    /// treats Being occupants as blocking. Use this for runtime step-aside,
    /// alternative position selection, and movement validation.
    /// </summary>
    /// <param name="gridPos">The grid position to check.</param>
    /// <param name="excludeEntity">An entity to ignore when checking occupancy (typically the mover).</param>
    /// <returns>True if the cell is terrain-walkable and not occupied by another Being.</returns>
    public bool IsCellPassable(Vector2I gridPos, Being? excludeEntity = null)
    {
        if (!IsCellWalkable(gridPos))
        {
            return false;
        }

        var occupant = EntitiesGridSystem.GetCell(gridPos);
        if (occupant is Being being && being != excludeEntity)
        {
            return false;
        }

        return true;
    }

    public float GetTerrainDifficulty(Vector2I gridPos)
    {
        return _groundGridSystem.GetCell(gridPos)?.WalkDifficulty ?? 1.0f;
    }

    public void PopulateLayersFromGrid()
    {
        foreach (var kvp in _groundGridSystem.OccupiedCells)
        {
            SetGroundCell(kvp.Key, kvp.Value);
        }

        if (_objectsLayer != null)
        {
            foreach (var kvp in _objectGridSystem.OccupiedCells)
            {
                _objectsLayer.SetCell(kvp.Key, kvp.Value.SourceId, kvp.Value.AtlasCoords);
            }
        }

        foreach (var (key, entity) in EntitiesGridSystem.OccupiedCells)
        {
            AddEntity(key, entity);
        }
    }

    public float GetTerrainDifficulty(Vector2I from, Vector2I to)
    {
        return (GetTerrainDifficulty(from) + GetTerrainDifficulty(to)) / 2;
    }
}
