using Godot;
using System.Collections.Generic;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Handles the roof system for buildings, including visibility and fading
    /// </summary>
    public partial class RoofSystem : Node
    {
        // Reference to the parent building
        private Building _building;

        // TileMap layer for roof tiles
        private int _roofLayerId = 2; // Assuming layer 2 is the roof layer

        // Dictionary of roof tiles by position
        private Dictionary<Vector2I, RoofTile> _roofTiles = new();

        // Current visibility state of the roof
        private bool _roofVisible = true;

        // Modulation for roof tiles when fully visible
        private Color _visibleColor = new Color(1, 1, 1, 1);

        // Modulation for roof tiles when partially visible
        private Color _fadeColor = new Color(1, 1, 1, 0.5f);

        // Modulation for roof tiles when invisible
        private Color _invisibleColor = new Color(1, 1, 1, 0);

        public override void _Ready()
        {
            // Get reference to parent building
            _building = GetParent<Building>();
            if (_building == null)
            {
                GD.PrintErr("RoofSystem: Not attached to a Building node!");
                return;
            }
        }

        /// <summary>
        /// Initialize roof tiles from a building template
        /// </summary>
        public void InitializeFromTemplate(BuildingTemplate template)
        {
            // Clear existing roof tiles
            _roofTiles.Clear();

            // Get the TileMap reference
            var tileMap = _building.GetNode<TileMap>("TileMap");
            if (tileMap == null)
            {
                GD.PrintErr("RoofSystem: TileMap not found!");
                return;
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

                    // Set in TileMap
                    tileMap.SetCell(_roofLayerId, tileData.Position, tileData.SourceId, tileData.AtlasCoords);
                }
            }

            // Set initial visibility
            UpdateRoofVisibility(true);
        }

        /// <summary>
        /// Update roof visibility based on player's line of sight
        /// </summary>
        public void UpdateRoofVisibility(bool forceVisible = false)
        {
            // If we're forcing visibility, show all roof tiles
            if (forceVisible)
            {
                SetAllRoofTilesVisible(true);
                return;
            }

            // TODO: Implement proper visibility checks based on the player's line of sight
            // For now, we'll just make the roof visible if the player is not inside the building

            // Get the TileMap reference
            var tileMap = _building.GetNode<TileMap>("TileMap");
            if (tileMap == null) return;

            // Update layer modulation
            if (_roofVisible)
            {
                tileMap.SetLayerModulate(_roofLayerId, _visibleColor);
            }
            else
            {
                tileMap.SetLayerModulate(_roofLayerId, _invisibleColor);
            }
        }

        /// <summary>
        /// Set all roof tiles to visible or invisible
        /// </summary>
        public void SetAllRoofTilesVisible(bool visible)
        {
            _roofVisible = visible;

            // Get the TileMap reference
            var tileMap = _building.GetNode<TileMap>("TileMap");
            if (tileMap == null) return;

            // Update visibility for all roof tiles
            foreach (var tile in _roofTiles)
            {
                tile.Value.IsVisible = visible;
            }

            // Update layer modulation
            tileMap.SetLayerModulate(_roofLayerId, visible ? _visibleColor : _invisibleColor);
        }

        /// <summary>
        /// Make specific roof tiles fade (partially visible) based on line of sight
        /// </summary>
        public void FadeRoofTiles(List<Vector2I> positions)
        {
            // Get the TileMap reference
            var tileMap = _building.GetNode<TileMap>("TileMap");
            if (tileMap == null) return;

            // This is a simplified version - in the full implementation,
            // we would calculate which specific tiles need to fade based on
            // the player's line of sight and other entities

            // For now, just set the modulation of the entire roof layer to fade
            tileMap.SetLayerModulate(_roofLayerId, _fadeColor);
        }

        /// <summary>
        /// Check if the player can see inside the building
        /// </summary>
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
    /// Represents a single roof tile
    /// </summary>
    public class RoofTile
    {
        public Vector2I Position { get; set; }
        public string Material { get; set; }
        public Vector2I AtlasCoords { get; set; }
        public int SourceId { get; set; }
        public bool IsVisible { get; set; } = true;
    }
}
