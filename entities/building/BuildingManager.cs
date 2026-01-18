using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities;

/// <summary>
/// Manages building templates and building placement in the world.
/// </summary>
public partial class BuildingManager : Node
{
    // Singleton instance
    private static BuildingManager? _instance;
    public static BuildingManager? Instance => _instance;

    // Dictionary of loaded templates by name
    private readonly Dictionary<string, BuildingTemplate> _templates = new ();

    // Reference to the world and grid system
    private World? _world;
    private VeilOfAges.Grid.Area? _currentArea;

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
            Log.Error("BuildingManager: Could not find World node!");
            return;
        }

        // The current area should be set later when needed
    }

    /// <summary>
    /// Load all building templates from the templates directory.
    /// </summary>
    public void LoadAllTemplates()
    {
        _templates.Clear();

        var loaded = JsonResourceLoader.LoadAllFromDirectory<BuildingTemplate>(
            _templatesPath,
            t => t.Name,
            t => t.Validate(),
            JsonOptions.WithVector2I);

        foreach (var kvp in loaded)
        {
            _templates[kvp.Key] = kvp.Value;
        }

        Log.Print($"BuildingManager: Loaded {_templates.Count} building templates");
    }

    /// <summary>
    /// Get a building template by name.
    /// </summary>
    /// <returns></returns>
    public BuildingTemplate? GetTemplate(string name)
    {
        if (_templates.TryGetValue(name, out var template))
        {
            return template;
        }

        Log.Error($"BuildingManager: Template not found: {name}");
        return null;
    }

    /// <summary>
    /// Get a list of all available template names.
    /// </summary>
    /// <returns></returns>
    public List<string> GetAllTemplateNames()
    {
        return [.. _templates.Keys];
    }

    /// <summary>
    /// Place a building in the world using a template.
    /// </summary>
    /// <returns></returns>
    public Building? PlaceBuilding(string templateName, Vector2I gridPosition, VeilOfAges.Grid.Area area)
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
            Log.Error("BuildingManager: No area specified for building placement");
            return null;
        }

        // Check if the space is available
        if (!CanPlaceBuildingAt(template, gridPosition, targetArea))
        {
            Log.Error($"BuildingManager: Cannot place {templateName} at {gridPosition} - space occupied");
            return null;
        }

        // Create the building scene instance
        var buildingScene = GD.Load<PackedScene>("res://entities/building/scene.tscn");
        var building = buildingScene.Instantiate<Building>();

        // Add to the scene tree
        targetArea.AddChild(building);

        // Initialize with template
        building.Initialize(targetArea, gridPosition, template);

        return building;
    }

    /// <summary>
    /// Check if a building can be placed at the specified position.
    /// </summary>
    /// <returns></returns>
    public static bool CanPlaceBuildingAt(BuildingTemplate template, Vector2I gridPosition, VeilOfAges.Grid.Area area)
    {
        // Check each tile in the template
        foreach (var tileData in template.Tiles)
        {
            Vector2I absolutePos = gridPosition + tileData.Position;

            // Skip check for walkable tiles in the template
            // These are tiles like floors inside buildings that don't block placement
            if (tileData.IsWalkable)
            {
                continue;
            }

            // Check if the cell is available for building
            // A cell is available if:
            // 1. No entity occupies it
            // 2. The ground tile is walkable (grass, dirt, path - not water or void)
            if (!area.IsCellWalkable(absolutePos))
            {
                Log.Warn($"CanPlaceBuildingAt: {template.Name} blocked at {absolutePos} (building pos {gridPosition}, tile offset {tileData.Position})");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Set the current grid area for building placement.
    /// </summary>
    public void SetCurrentArea(VeilOfAges.Grid.Area area)
    {
        _currentArea = area;
    }

    /// <summary>
    /// Save a custom building as a new template.
    /// </summary>
    /// <returns></returns>
    public static bool SaveAsTemplate(Building building, string templateName)
    {
        // TODO: Implement custom building saving
        // This would extract all tile information from an existing building
        // and create a new template from it
        Log.Error("BuildingManager: SaveAsTemplate not implemented yet");
        return false;
    }
}
