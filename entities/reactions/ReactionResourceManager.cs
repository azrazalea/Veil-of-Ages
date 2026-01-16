using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Reactions;

/// <summary>
/// Singleton manager for loading and accessing reaction definitions.
/// </summary>
public class ReactionResourceManager
{
    // Singleton instance
    private static ReactionResourceManager? _instance;
    public static ReactionResourceManager Instance
    {
        get
        {
            _instance ??= new ReactionResourceManager();
            return _instance;
        }
    }

    // Reaction definition collection
    private readonly Dictionary<string, ReactionDefinition> _definitions = new ();

    // Has this manager been initialized?
    private bool _initialized;

    // Private constructor to enforce singleton pattern
    private ReactionResourceManager()
    {
    }

    /// <summary>
    /// Initialize the resource manager by loading all reaction definitions.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        LoadAllDefinitions();

        _initialized = true;
        Log.Print($"ReactionResourceManager initialized with {_definitions.Count} reaction definitions");
    }

    /// <summary>
    /// Load all reaction definitions from the resources folder.
    /// </summary>
    private void LoadAllDefinitions()
    {
        string reactionsPath = "res://resources/reactions";
        string projectPath = ProjectSettings.GlobalizePath(reactionsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Reactions directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        // Load all JSON files in the reactions directory
        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            LoadDefinitionFromFile(file);
        }

        // Also load from subdirectories for organization
        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                LoadDefinitionFromFile(file);
            }
        }
    }

    /// <summary>
    /// Load a single definition from a JSON file.
    /// </summary>
    /// <param name="file">The path to the JSON file.</param>
    private void LoadDefinitionFromFile(string file)
    {
        var definition = ReactionDefinition.LoadFromJson(file);
        if (definition != null && definition.Validate())
        {
            if (definition.Id != null)
            {
                _definitions[definition.Id] = definition;
                Log.Print($"Loaded reaction definition: {definition.Id}");
            }
        }
        else
        {
            Log.Error($"Failed to load reaction definition from: {file}");
        }
    }

    /// <summary>
    /// Get a reaction definition by ID.
    /// </summary>
    /// <param name="id">The reaction definition ID.</param>
    /// <returns>The reaction definition or null if not found.</returns>
    public ReactionDefinition? GetDefinition(string id)
    {
        if (_definitions.TryGetValue(id, out var definition))
        {
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Get all loaded reaction definitions.
    /// </summary>
    /// <returns>Enumerable of all reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Get all reaction definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsByTag(string tag)
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
    /// Get all reaction definitions that can be performed with the available facilities.
    /// </summary>
    /// <param name="availableFacilities">Set of facility IDs available at the location.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsForFacilities(IEnumerable<string>? availableFacilities)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.CanPerformWith(availableFacilities))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all reaction definitions that require a specific facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to filter by.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsRequiringFacility(string facilityId)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.RequiresFacility(facilityId))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Check if a definition with the given ID exists.
    /// </summary>
    /// <param name="id">The reaction definition ID.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }
}
