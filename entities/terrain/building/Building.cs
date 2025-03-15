using Godot;
using System;
using System.Collections.Generic;

public partial class Building : Node2D
{
    [Export]
    public string BuildingType = "Graveyard";

    [Export]
    public bool CanEnter = true;

    [Export]
    public Vector2I GridSize = new Vector2I(2, 2); // Size in tiles (most buildings are 2x2)

    private Vector2I _gridPosition;
    private GridSystem _gridSystem;
    private Dictionary<string, TileMapLayer> _buildingLayers = new Dictionary<string, TileMapLayer>();
    private TileMapLayer _currentLayer;

    // For tracking residents/workers
    private int _capacity = 0;
    private int _occupants = 0;

    public override void _Ready()
    {
        // Find the grid system
        var world = GetTree().GetFirstNodeInGroup("World") as World;
        if (world != null)
        {
            _gridSystem = world.GetNode<GridSystem>("GridSystem");
        }
        else
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

        // Get grid position
        _gridPosition = _gridSystem.WorldToGrid(GlobalPosition);

        // Snap to grid
        GlobalPosition = _gridSystem.GridToWorld(_gridPosition);

        // Configure building properties based on type
        ConfigureBuildingType();

        // Mark the grid cells as occupied
        _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, true);

        GD.Print($"{BuildingType} registered at grid position {_gridPosition}");
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(GridSystem gridSystem, Vector2I gridPos, string type = "")
    {
        _gridSystem = gridSystem;
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

        // Configure building properties
        ConfigureBuildingType();

        // Update the actual position
        GlobalPosition = _gridSystem.GridToWorld(_gridPosition);

        // Register with the grid system
        _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, true);
    }

    public override void _ExitTree()
    {
        // When the building is removed, mark its cells as unoccupied (if grid system exists)
        if (_gridSystem != null)
        {
            _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, false);
        }
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
