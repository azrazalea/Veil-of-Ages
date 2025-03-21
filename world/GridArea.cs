using System;
using System.Linq;
using Godot;
using VeilOfAges.Entities;

namespace VeilOfAges.Grid
{
    /// <summary>
    /// Handles the graphics display of an area
    /// </summary>
    public partial class Area(Vector2I worldSize) : Node2D
    {
        [Export] public string AreaName { get; set; } = "Default Area";
        [Export] public Vector2I GridSize { get; set; } = new(100, 100);

        private TileMapLayer _groundLayer;
        private TileMapLayer _objectsLayer;
        // TODO: We need to properly implement terrain and make this
        // a proper object that has a texture associated with it
        private GroundSystem _groundGridSystem = new(worldSize);
        // TODO: We need to properly implement items and make this
        // a proper object that has a texture assoiciated with it
        private GroundSystem _objectGridSystem = new(worldSize);
        public Node2DSystem EntitiesGridSystem { get; private set; } = new Node2DSystem(worldSize);

        private Node2D _entitiesContainer;
        /// <summary>
        /// Is area in full detail mode with all AI active?
        /// </summary>
        private bool _isActive = false;
        /// <summary>
        /// Is this the area the player is in currently?
        /// </summary>
        private bool _isPlayerArea = false;

        public static Tile WaterTile = new(
            1,
            new(3, 16),
            false
        );

        public static Tile GrassTile = new(
            0,
            new(1, 3),
            true,
            1.0f
        );

        public static Tile DirtTile = new(
            0,
            new(5, 3),
            true,
            0.8f
        );

        public static Tile PathTile = new(
            0,
            new(6, 21),
            true,
            0.5f
        );

        private World _gameWorld;

        public void SetActive()
        {
            _isActive = true;
        }

        public override void _Ready()
        {
            base._Ready();

            // TODO: Update to not be hard coded in this way
            var tileSet = GetNode<TileMapLayer>("/root/World/GroundLayer").TileSet;
            _groundLayer = new TileMapLayer
            {
                TileSet = tileSet
            };
            _objectsLayer = new TileMapLayer();
        }

        public void MakePlayerArea(Player _player, Vector2I playerStartingLocation)
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
            _groundLayer.Enabled = true;
            _objectsLayer.Enabled = true;

            // Declare we are active
            _isActive = true;
            _isPlayerArea = true;
            _player.Position = Grid.Utils.GridToWorld(playerStartingLocation);
        }

        public void SetGroundCell(Vector2I groundPos, Tile tile)
        {
            _groundGridSystem.SetCell(groundPos, tile);

            if (_groundLayer.Enabled)
            {
                _groundLayer.SetCell(groundPos, tile.SourceId, tile.AtlasCoords);
            }
        }

        public void AddEntity(Vector2I entityPos, Node2D entity, Vector2I? entitySize = null)
        {
            if (entitySize is Vector2I size)
            {
                for (int x = 0; x < size.X; x++)
                {
                    for (int y = 0; y < size.Y; y++)
                    {
                        EntitiesGridSystem.SetCell(new Vector2I(entityPos.X + x, entityPos.Y + y), entity);
                    }
                }
            }
            else
            {
                EntitiesGridSystem.SetCell(entityPos, entity);
            }

        }

        public void RemoveEntity(Vector2I entityPos, Vector2I? entitySize = null)
        {
            if (entitySize is Vector2I size)
            {
                for (int x = 0; x < size.X; x++)
                {
                    for (int y = 0; y < size.Y; y++)
                    {
                        EntitiesGridSystem.RemoveCell(new Vector2I(entityPos.X + x, entityPos.Y + y));
                    }
                }
            }
            else
            {
                EntitiesGridSystem.RemoveCell(entityPos);
            }
        }

        // TODO: We need to handle unwalkable objects here if any
        public bool IsCellWalkable(Vector2I gridPos)
        {
            return !EntitiesGridSystem.IsCellOccupied(gridPos) && (
                !_groundGridSystem.IsCellOccupied(gridPos) || _groundGridSystem.GetCell(gridPos).IsWalkable
            );
        }

        public void PopulateLayersFromGrid()
        {
            foreach (var kvp in _groundGridSystem.OccupiedCells)
            {
                SetGroundCell(kvp.Key, kvp.Value);
            }

            foreach (var kvp in _objectGridSystem.OccupiedCells)
            {
                _objectsLayer.SetCell(kvp.Key, kvp.Value.SourceId, kvp.Value.AtlasCoords);
            }

            foreach (var (key, entity) in EntitiesGridSystem.OccupiedCells)
            {
                AddEntity(key, entity);
            }
        }

        public float GetTerrainDifficulty(Vector2I from, Vector2I to)
        {
            return (_groundGridSystem.GetCell(from).WalkDifficulty + _groundGridSystem.GetCell(to).WalkDifficulty) / 2;
        }
    }
}
