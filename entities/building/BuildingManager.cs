using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Manages building templates and building placement in the world
    /// </summary>
    public partial class BuildingManager : Node
    {
        // Singleton instance
        private static BuildingManager _instance;
        public static BuildingManager Instance => _instance;

        // Dictionary of loaded templates by name
        private Dictionary<string, BuildingTemplate> _templates = new();

        // Reference to the world and grid system
        private World _world;
        private VeilOfAges.Grid.Area _currentArea;

        // Path to templates directory
        private string _templatesPath = "res://resources/buildings/templates";

        public override void _Ready()
        {
            // Set singleton instance
            _instance = this;

            // Load building templates
            LoadAllTemplates();

            // Get references
            _world = GetTree().GetFirstNodeInGroup("World") as World;
            if (_world == null)
            {
                GD.PrintErr("BuildingManager: Could not find World node!");
                return;
            }

            // The current area should be set later when needed
        }

        /// <summary>
        /// Load all building templates from the templates directory
        /// </summary>
        public void LoadAllTemplates()
        {
            // Clear existing templates
            _templates.Clear();

            // Get the templates directory
            string templatesDir = ProjectSettings.GlobalizePath(_templatesPath);
            if (!Directory.Exists(templatesDir))
            {
                GD.PrintErr($"BuildingManager: Templates directory not found: {templatesDir}");
                return;
            }

            // Load all JSON files in the directory
            foreach (string file in Directory.GetFiles(templatesDir, "*.json"))
            {
                try
                {
                    var template = BuildingTemplate.LoadFromJson(file);
                    if (template != null && template.Validate())
                    {
                        _templates[template.Name] = template;
                        GD.Print($"Loaded building template: {template.Name}");
                    }
                    else
                    {
                        GD.PrintErr($"BuildingManager: Invalid template in file: {file}");
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"BuildingManager: Error loading template from {file}: {e.Message}");
                }
            }

            GD.Print($"BuildingManager: Loaded {_templates.Count} building templates");
        }

        /// <summary>
        /// Get a building template by name
        /// </summary>
        public BuildingTemplate GetTemplate(string name)
        {
            if (_templates.TryGetValue(name, out var template))
            {
                return template;
            }

            GD.PrintErr($"BuildingManager: Template not found: {name}");
            return null;
        }

        /// <summary>
        /// Get a list of all available template names
        /// </summary>
        public List<string> GetAllTemplateNames()
        {
            return _templates.Keys.ToList();
        }

        /// <summary>
        /// Place a building in the world using a template
        /// </summary>
        public Building PlaceBuilding(string templateName, Vector2I gridPosition, VeilOfAges.Grid.Area area = null)
        {
            // Get the template
            var template = GetTemplate(templateName);
            if (template == null)
            {
                return null;
            }

            // Use provided area or current area
            var targetArea = area ?? _currentArea;
            if (targetArea == null)
            {
                GD.PrintErr("BuildingManager: No area specified for building placement");
                return null;
            }

            // Check if the space is available
            if (!CanPlaceBuildingAt(template, gridPosition, targetArea))
            {
                GD.PrintErr($"BuildingManager: Cannot place {templateName} at {gridPosition} - space occupied");
                return null;
            }

            // Create the building scene instance
            var buildingScene = GD.Load<PackedScene>("res://entities/building/Building.tscn");
            var building = buildingScene.Instantiate<Building>();

            // Add to the scene tree
            targetArea.AddChild(building);

            // Initialize with template
            building.Initialize(targetArea, gridPosition, template);

            return building;
        }

        /// <summary>
        /// Check if a building can be placed at the specified position
        /// </summary>
        public bool CanPlaceBuildingAt(BuildingTemplate template, Vector2I gridPosition, VeilOfAges.Grid.Area area)
        {
            // Check each tile in the template
            foreach (var tileData in template.Tiles)
            {
                Vector2I absolutePos = gridPosition + tileData.Position;

                // Skip check for walkable tiles
                if (tileData.IsWalkable)
                {
                    continue;
                }

                // Check if the cell is occupied
                if (!area.IsCellWalkable(absolutePos))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Set the current grid area for building placement
        /// </summary>
        public void SetCurrentArea(VeilOfAges.Grid.Area area)
        {
            _currentArea = area;
        }

        /// <summary>
        /// Save a custom building as a new template
        /// </summary>
        public bool SaveAsTemplate(Building building, string templateName)
        {
            // TODO: Implement custom building saving
            // This would extract all tile information from an existing building
            // and create a new template from it

            GD.PrintErr("BuildingManager: SaveAsTemplate not implemented yet");
            return false;
        }
    }
}
