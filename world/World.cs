using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.WorldGeneration;

namespace VeilOfAges
{
    public partial class World : Node2D
    {
        // Global world properties
        [Export] public int WorldSeed { get; set; } = 0;
        [Export] public float GlobalTimeScale { get; set; } = 1.0f;
        [Export]
        public bool GenerateOnReady = true;
        public Grid.Area? ActiveGridArea;
        private List<Grid.Area> _gridAreas = [];

        // Entities container
        private Node2D? _entitiesContainer;
        private GridGenerator? _gridGenerator;
        private SensorySystem? _sensorySystem;
        private EventSystem? _eventSystem;

        // References
        private Player? _player;

        [Export]
        public Vector2I WorldSizeInTiles = new(100, 100);

        public override void _Ready()
        {
            // Get references to nodes
            _entitiesContainer = GetNode<Node2D>("Entities");
            _gridGenerator = GetNode<GridGenerator>("GridGenerator");
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
                _player.Initialize(ActiveGridArea, new Vector2I(50, 50));
                ActiveGridArea.MakePlayerArea(_player, new Vector2I(50, 50));
            }
            else
            {
                GD.PrintErr("Player node not found! Make sure you've instanced Player.tscn as a child of Entities.");
            }
            if (GenerateOnReady)
            {
                _gridGenerator.CallDeferred(GridGenerator.MethodName.Generate, this);
            }
        }

        public SensorySystem? GetSensorySystem() => _sensorySystem;
        public EventSystem? GetEventSystem() => _eventSystem;

        public void PrepareForTick()
        {
            GetSensorySystem()?.PrepareForTick();
            // Update needs for all beings
            foreach (var being in GetBeings())
            {
                being.NeedsSystem?.UpdateNeeds();
            }
        }

        public List<Being> GetBeings()
        {
            var entities = new List<Being>();
            foreach (Node entity in _entitiesContainer?.GetChildren() ?? [])
            {
                if (entity is Being being) entities.Add(being);
            }

            return entities;
        }
    }
}
