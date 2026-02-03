using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Items;

/// <summary>
/// Autoload singleton manager for loading and creating items from JSON definitions.
/// Register as an autoload in project.godot.
/// </summary>
public partial class ItemResourceManager : Node
{
    // Singleton instance
    private static ItemResourceManager? _instance;

    public static ItemResourceManager Instance => _instance
        ?? throw new InvalidOperationException("ItemResourceManager not initialized. Ensure it's registered as an autoload in project.godot");

    // Item definition collection
    private readonly Dictionary<string, ItemDefinition> _definitions = new ();

    public override void _Ready()
    {
        _instance = this;
        LoadAllDefinitions();
        Log.Print($"ItemResourceManager initialized with {_definitions.Count} item definitions");
    }

    /// <summary>
    /// Load all item definitions from the resources folder.
    /// </summary>
    private void LoadAllDefinitions()
    {
        string itemsPath = "res://resources/items";
        string projectPath = ProjectSettings.GlobalizePath(itemsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Items directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        // Load all JSON files in the items directory
        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            var definition = ItemDefinition.LoadFromJson(file);
            if (definition != null && definition.Validate())
            {
                if (definition.Id != null)
                {
                    _definitions[definition.Id] = definition;
                    Log.Print($"Loaded item definition: {definition.Id}");
                }
            }
            else
            {
                Log.Error($"Failed to load item definition from: {file}");
            }
        }

        // Also load from subdirectories for organization
        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var definition = ItemDefinition.LoadFromJson(file);
                if (definition != null && definition.Validate())
                {
                    if (definition.Id != null)
                    {
                        _definitions[definition.Id] = definition;
                        Log.Print($"Loaded item definition: {definition.Id}");
                    }
                }
                else
                {
                    Log.Error($"Failed to load item definition from: {file}");
                }
            }
        }
    }

    /// <summary>
    /// Get an item definition by ID.
    /// </summary>
    /// <param name="id">The item definition ID.</param>
    /// <returns>The item definition or null if not found.</returns>
    public ItemDefinition? GetDefinition(string id)
    {
        if (_definitions.TryGetValue(id, out var definition))
        {
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Create a new item instance from a definition ID.
    /// </summary>
    /// <param name="definitionId">The item definition ID.</param>
    /// <param name="quantity">The quantity to create (default 1).</param>
    /// <returns>A new Item instance or null if definition not found.</returns>
    public Item? CreateItem(string definitionId, int quantity = 1)
    {
        var definition = GetDefinition(definitionId);
        if (definition == null)
        {
            Log.Error($"Cannot create item: definition '{definitionId}' not found");
            return null;
        }

        return new Item(definition, quantity);
    }

    /// <summary>
    /// Get all loaded item definitions.
    /// </summary>
    /// <returns>Enumerable of all item definitions.</returns>
    public IEnumerable<ItemDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Get all item definitions matching a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Enumerable of matching item definitions.</returns>
    public IEnumerable<ItemDefinition> GetDefinitionsByCategory(ItemCategory category)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.Category == category)
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all item definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching item definitions.</returns>
    public IEnumerable<ItemDefinition> GetDefinitionsByTag(string tag)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.HasTag(tag))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Check if a definition with the given ID exists.
    /// </summary>
    /// <param name="id">The item definition ID.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }
}
