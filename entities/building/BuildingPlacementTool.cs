using System.Collections.Generic;
using Godot;
using VeilOfAges.Grid;

// TODO: Rewrite
namespace VeilOfAges.Entities;

/// <summary>
/// Tool for placing buildings in the game world.
/// </summary>
public partial class BuildingPlacementTool : Node
{
    // Current template being placed
    private BuildingTemplate? _currentTemplate;

    // Preview tiles
    private TileMapLayer? _previewTileMap;

    // Current grid position for placement
    private Vector2I _currentGridPosition;

    // Colors for valid/invalid placement
    private Color _validColor = new (0, 1, 0, 0.5f); // Green, semi-transparent
    private Color _invalidColor = new (1, 0, 0, 0.5f); // Red, semi-transparent

    // Is placement currently valid
    private bool _isValidPlacement = false;

    // Reference to the grid area
    private Area? _gridArea;

    // Reference to the building manager
    private BuildingManager? _buildingManager;

    // Callback for when a building is placed
    public delegate void BuildingPlacedDelegate(Building building, Vector2I position);
    public BuildingPlacedDelegate? OnBuildingPlaced;

    public override void _Ready()
    {
        // Create preview tilemap
        _previewTileMap = new TileMapLayer();
        AddChild(_previewTileMap);

        // Get references
        _buildingManager = GetNode<BuildingManager>("/root/BuildingManager");
        if (_buildingManager == null)
        {
            GD.PrintErr("BuildingPlacementTool: BuildingManager not found!");
        }

        // Initially hide preview
        _previewTileMap.Visible = false;
    }

    /// <summary>
    /// Start placing a building with the specified template.
    /// </summary>
    public void StartPlacement(string templateName, Area gridArea)
    {
        // Get the template
        _currentTemplate = _buildingManager?.GetTemplate(templateName);
        if (_currentTemplate == null)
        {
            GD.PrintErr($"BuildingPlacementTool: Template not found: {templateName}");
            return;
        }

        // Set the grid area
        _gridArea = gridArea;

        // Set up preview tilemap
        SetupPreviewTileMap();

        // Show preview
        if (_previewTileMap != null)
        {
            _previewTileMap.Visible = true;
        }

        // Reset state
        _isValidPlacement = false;

        // Enable processing
        SetProcess(true);
        SetProcessInput(true);
    }

    /// <summary>
    /// Cancel building placement.
    /// </summary>
    public void CancelPlacement()
    {
        // Hide preview
        if (_previewTileMap != null)
        {
            _previewTileMap.Visible = false;
        }

        // Disable processing
        SetProcess(false);
        SetProcessInput(false);

        _currentTemplate = null;
    }

    /// <summary>
    /// Set up the preview tilemap based on the current template.
    /// </summary>
    private void SetupPreviewTileMap()
    {
        if (_previewTileMap == null || _currentTemplate == null)
        {
            return;
        }

        // Clear existing tiles
        _previewTileMap.Clear();

        // Set the same tileset as used by buildings
        var building = _buildingManager?.PlaceBuilding(_currentTemplate.Name, new Vector2I(0, 0), null!);
        if (building != null)
        {
            var buildingTileMap = building.GetNode<TileMapLayer>("TileMap");
            if (buildingTileMap != null)
            {
                _previewTileMap.TileSet = buildingTileMap.TileSet;
            }

            // Remove the temporary building
            building.QueueFree();
        }

        // Add preview tiles based on template
        foreach (var tileData in _currentTemplate.Tiles)
        {
            _previewTileMap.SetCell(tileData.Position, tileData.SourceId, tileData.AtlasCoords);
        }
    }

    public override void _Process(double delta)
    {
        if (_currentTemplate == null || _gridArea == null)
        {
            return;
        }

        // Update preview position based on mouse position
        Vector2 mousePos = GetViewport().GetCamera2D().GetGlobalMousePosition();
        Vector2I gridPos = Utils.WorldToGrid(mousePos);

        if (gridPos != _currentGridPosition)
        {
            _currentGridPosition = gridPos;
            UpdatePreviewPosition();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_currentTemplate == null || _gridArea == null)
        {
            return;
        }

        // Handle mouse click for placement
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            if (_isValidPlacement)
            {
                PlaceBuildingAtCurrentPosition();
            }
        }

        // Handle escape key to cancel placement
        if (@event is InputEventKey keyEvent &&
            keyEvent.Keycode == Key.Escape &&
            keyEvent.Pressed)
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Update the preview position based on the current grid position.
    /// </summary>
    private void UpdatePreviewPosition()
    {
        if (_previewTileMap == null || _currentTemplate == null || _gridArea == null)
        {
            return;
        }

        // Move preview to grid position
        _previewTileMap.Position = Utils.GridToWorld(_currentGridPosition);

        // Check if placement is valid
        _isValidPlacement = _buildingManager?.CanPlaceBuildingAt(_currentTemplate, _currentGridPosition, _gridArea) ?? false;

        // Update preview color
        _previewTileMap.Modulate = _isValidPlacement ? _validColor : _invalidColor;
    }

    /// <summary>
    /// Place a building at the current position.
    /// </summary>
    private void PlaceBuildingAtCurrentPosition()
    {
        if (_buildingManager == null || _currentTemplate == null || _gridArea == null)
        {
            return;
        }

        // Place the building
        var building = _buildingManager.PlaceBuilding(_currentTemplate.Name, _currentGridPosition, _gridArea);

        if (building != null)
        {
            // Notify listeners
            OnBuildingPlaced?.Invoke(building, _currentGridPosition);

            // Hide preview
            if (_previewTileMap != null)
            {
                _previewTileMap.Visible = false;
            }

            // Disable processing
            SetProcess(false);
            SetProcessInput(false);

            _currentTemplate = null;
        }
    }
}
