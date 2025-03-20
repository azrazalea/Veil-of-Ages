using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using System.Collections.Generic;
using VeilOfAges;

public partial class Building : Node2D, IEntity
{
    [Export]
    public string BuildingType = "Graveyard";

    [Export]
    public bool CanEnter = true;

    [Export]
    public Vector2I GridSize = new(2, 2); // Size in tiles (most buildings are 2x2)

    private Vector2I _gridPosition;
    public VeilOfAges.Grid.Area GridArea { get; private set; }
    public List<ITrait> _traits { get; private set; }
    private Dictionary<string, TileMapLayer> _buildingLayers = [];
    public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; }
    private TileMapLayer _currentLayer;

    // For tracking residents/workers
    private int _capacity = 0;
    private int _occupants = 0;

    public override void _Ready()
    {
        GD.Print($"Building grid position _Ready: {_gridPosition}");

        // Find the grid system
        if (GetTree().GetFirstNodeInGroup("World") is not World world)
        {
            GD.PrintErr("Building: Could not find World node with GridSystem!");
            return;
        }

        // Initialize the building layers dictionary
        foreach (Node child in GetChildren())
        {
            if (child is TileMapLayer)
            {
                string layerName = child.Name.ToString().Replace("Layer", "");
                _buildingLayers[layerName] = child as TileMapLayer;
                GD.Print(layerName);
                (child as TileMapLayer).Enabled = false; // Hide all initially
            }
        }

        // Snap to grid
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);

        // Configure building properties based on type
        ConfigureBuildingType();

        GD.Print($"{BuildingType} registered at grid position {_gridPosition}");
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(VeilOfAges.Grid.Area gridArea, Vector2I gridPos, string type = "")
    {
        GridArea = gridArea;
        _gridPosition = gridPos;

        if (!string.IsNullOrEmpty(type))
        {
            BuildingType = type;
        }

        // Initialize the building layers dictionary if not done already
        if (_buildingLayers.Count == 0)
        {
            foreach (Node child in GetChildren())
            {
                if (child is TileMapLayer)
                {
                    string layerName = child.Name.ToString().Replace("Layer", "");
                    _buildingLayers[layerName] = child as TileMapLayer;
                    (child as TileMapLayer).Enabled = false; // Hide all initially
                }
            }
        }

        ZIndex = 1;
        // Configure building properties
        ConfigureBuildingType();

        GD.Print($"Building grid position Initialize: {_gridPosition}");
        // Update the actual position
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    public Vector2I GetGridPosition()
    {
        return _gridPosition;
    }
    public SensableType GetSensableType()
    {
        return SensableType.Building;
    }
    public override void _ExitTree()
    {
        // When the building is removed, mark its cells as unoccupied (if grid system exists)
        GridArea.RemoveEntity(_gridPosition, GridSize);
    }

    // Method for when player interacts with building
    public void Interact()
    {
        GD.Print($"Player is interacting with {BuildingType}");
        // This would later handle entering buildings, triggering events, etc.
    }

    // Configure properties based on building type
    private void ConfigureBuildingType()
    {
        switch (BuildingType)
        {
            case "House":
                GridSize = new Vector2I(2, 2);
                _capacity = 4;
                break;

            case "Blacksmith":
                GridSize = new Vector2I(3, 2);
                _capacity = 2;
                break;

            case "Tavern":
                GridSize = new Vector2I(3, 3);
                _capacity = 8;
                break;

            case "Farm":
                GridSize = new Vector2I(3, 2);
                _capacity = 3;
                break;

            case "Well":
                GridSize = new Vector2I(1, 1);
                _capacity = 0;
                break;

            case "Graveyard":
                GridSize = new Vector2I(7, 6);
                _capacity = 4; // For undead
                break;

            case "Laboratory":
                GridSize = new Vector2I(3, 2);
                _capacity = 2;
                break;

            default:
                GridSize = new Vector2I(2, 2);
                _capacity = 2;
                break;
        }

        // Update visual representation
        UpdateBuildingVisual();
    }

    private void UpdateBuildingVisual()
    {
        // Disable all building layers first
        foreach (var layer in _buildingLayers.Values)
        {
            layer.Enabled = false;
        }

        // Enable the correct layer for this building type
        if (_buildingLayers.ContainsKey(BuildingType))
        {
            _currentLayer = _buildingLayers[BuildingType];
            _currentLayer.Enabled = true;
            // Force an immediate update to make sure the tiles appear correctly
            _currentLayer.UpdateInternals();
        }
        else
        {
            GD.PrintErr($"No TileMapLayer found for building type: {BuildingType}");
        }
    }

    // Used by the WorldGenerator to set building type
    public void SetBuildingType(string type)
    {
        BuildingType = type;
        ConfigureBuildingType();
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
}
