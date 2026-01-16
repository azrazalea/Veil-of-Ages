using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities;

/// <summary>
/// Handles the roof system for buildings, including visibility and fading.
/// </summary>
public partial class RoofSystem : Node
{
    // Reference to the parent building
    private Building? _building;

    // TileMapLayer for roof tiles (each layer is now its own node)
    private TileMapLayer? _roofTileMap;

    // Dictionary of roof tiles by position
    private readonly Dictionary<Vector2I, RoofTile> _roofTiles = new ();

    // Current visibility state of the roof
    private bool _roofVisible = true;

    // Modulation for roof tiles when fully visible
    private Color _visibleColor = new (1, 1, 1, 1);

    // Modulation for roof tiles when partially visible
    private Color _fadeColor = new (1, 1, 1, 0.5f);

    // Modulation for roof tiles when invisible
    private Color _invisibleColor = new (1, 1, 1, 0);

    public override void _Ready()
    {
        // Get reference to parent building
        _building = GetParent<Building>();
        if (_building == null)
        {
            Log.Error("RoofSystem: Not attached to a Building node!");
            return;
        }
    }

    /// <summary>
    /// Initialize roof tiles from a building template.
    /// </summary>
    public void InitializeFromTemplate(BuildingTemplate template)
    {
        // Clear existing roof tiles
        _roofTiles.Clear();

        if (_building == null)
        {
            Log.Error("RoofSystem: Building reference is null!");
            return;
        }

        // Create the roof TileMapLayer if it doesn't exist
        if (_roofTileMap == null)
        {
            _roofTileMap = new TileMapLayer
            {
                Visible = true
            };
            _building.AddChild(_roofTileMap);

            // Position and Z-index to render above other building layers
            _roofTileMap.ZIndex = 10;
            _roofTileMap.ZAsRelative = true;

            // Setup tileset using TileResourceManager
            TileResourceManager.Instance.SetupTileSet(_roofTileMap);
        }

        // Find all roof tiles in the template
        foreach (var tileData in template.Tiles)
        {
            if (tileData.Type == "Roof")
            {
                // Create a roof tile
                var roofTile = new RoofTile
                {
                    Position = tileData.Position,
                    Material = tileData.Material,
                    AtlasCoords = tileData.AtlasCoords,
                    SourceId = tileData.SourceId,
                    IsVisible = true
                };

                // Add to dictionary
                _roofTiles[tileData.Position] = roofTile;

                // Set in TileMapLayer (no layer ID needed - each layer is its own node)
                _roofTileMap.SetCell(tileData.Position, tileData.SourceId, tileData.AtlasCoords);
            }
        }

        // Set initial visibility
        UpdateRoofVisibility(true);
    }

    /// <summary>
    /// Update roof visibility based on player's line of sight.
    /// </summary>
    public void UpdateRoofVisibility(bool forceVisible = false)
    {
        // If we're forcing visibility, show all roof tiles
        if (forceVisible)
        {
            SetAllRoofTilesVisible(true);
            return;
        }

        if (_roofTileMap == null)
        {
            return;
        }

        // TODO: Implement proper visibility checks based on the player's line of sight
        // For now, we'll just make the roof visible if the player is not inside the building

        // Update layer modulation using CanvasItem.Modulate property
        _roofTileMap.Modulate = _roofVisible ? _visibleColor : _invisibleColor;
    }

    /// <summary>
    /// Set all roof tiles to visible or invisible.
    /// </summary>
    public void SetAllRoofTilesVisible(bool visible)
    {
        _roofVisible = visible;

        if (_roofTileMap == null)
        {
            return;
        }

        // Update visibility for all roof tiles
        foreach (var tile in _roofTiles)
        {
            tile.Value.IsVisible = visible;
        }

        // Update modulation using CanvasItem.Modulate property
        _roofTileMap.Modulate = visible ? _visibleColor : _invisibleColor;
    }

    /// <summary>
    /// Make specific roof tiles fade (partially visible) based on line of sight.
    /// </summary>
    public void FadeRoofTiles(List<Vector2I> positions)
    {
        if (_roofTileMap == null)
        {
            return;
        }

        // This is a simplified version - in the full implementation,
        // we would calculate which specific tiles need to fade based on
        // the player's line of sight and other entities

        // For now, just set the modulation of the entire roof layer to fade
        _roofTileMap.Modulate = _fadeColor;
    }

    /// <summary>
    /// Check if the player can see inside the building.
    /// </summary>
    /// <returns></returns>
    public bool CanPlayerSeeInside()
    {
        // TODO: Implement proper check based on player position and line of sight
        // For now, return a simple placeholder
        return false;
    }

    public override void _Process(double delta)
    {
        // In the future, this could update roof visibility based on player position in real-time
        // For now, we'll only update when explicitly called
    }
}

/// <summary>
/// Represents a single roof tile.
/// </summary>
public class RoofTile
{
    public Vector2I Position { get; set; }
    public string? Material { get; set; }
    public Vector2I AtlasCoords { get; set; }
    public int SourceId { get; set; }
    public bool IsVisible { get; set; } = true;
}
