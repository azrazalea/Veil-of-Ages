using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges
{
    public partial class World : Node2D
    {
        // Global world properties
        [Export] public int WorldSeed { get; set; } = 0;
        [Export] public float GlobalTimeScale { get; set; } = 1.0f;
        [Export]
        public bool GenerateOnReady = true;
        public Grid.Area ActiveGridArea;
        private List<Grid.Area> _gridAreas = [];

        // Entities container
        private Node2D _entitiesContainer;
        private WorldGenerator _worldGenerator;
        private SensorySystem _sensorySystem;
        private EventSystem _eventSystem;

        // References
        private Player _player;

        [Export]
        public Vector2I WorldSizeInTiles = new(100, 100);

        public override void _Ready()
        {
            // Get references to nodes
            _entitiesContainer = GetNode<Node2D>("Entities");
            _worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
            var gridAreasContainer = GetNode<Node>("GridAreas");

            // Initialize grid system with world bounds
            ActiveGridArea = new Grid.Area(WorldSizeInTiles);
            _gridAreas.Add(ActiveGridArea);
            gridAreasContainer.AddChild(ActiveGridArea);

            // Get reference to the Player scene instance
            _player = GetNode<Player>("Entities/Player");

            _sensorySystem = new SensorySystem(this);
            _eventSystem = new EventSystem();

            // Register the player with the grid system
            if (_player != null)
            {
                Vector2I playerGridPos = Grid.Utils.WorldToGrid(_player.Position);
                _player.Initialize(ActiveGridArea, playerGridPos);
                ActiveGridArea.MakePlayerArea(_player, playerGridPos);
            }
            else
            {
                GD.PrintErr("Player node not found! Make sure you've instanced Player.tscn as a child of Entities.");
            }
            if (GenerateOnReady)
            {
                _worldGenerator.CallDeferred(WorldGenerator.MethodName.Generate, this);
            }
        }

        public float GetTerrainDifficulty(Vector2I from, Vector2I to)
        {
            return 1.0f;
        }

        public SensorySystem GetSensorySystem() => _sensorySystem;
        public EventSystem GetEventSystem() => _eventSystem;

        // Converts a world position to a grid position
        public Vector2I WorldToGrid(Vector2 worldPosition)
        {
            return Grid.Utils.WorldToGrid(worldPosition);
        }

        // Converts a grid position to a world position
        public Vector2 GridToWorld(Vector2I gridPosition)
        {
            return Grid.Utils.GridToWorld(gridPosition);
        }

        public List<Being> GetEntities()
        {
            var entities = new List<Being>();
            foreach (Node entity in _entitiesContainer.GetChildren())
            {
                if (entity is Being being) entities.Add(being);
            }

            return entities;
        }
    }
}
