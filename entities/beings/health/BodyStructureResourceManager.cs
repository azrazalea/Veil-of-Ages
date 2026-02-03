using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings.Health;

/// <summary>
/// Singleton manager for loading body structure definitions from JSON.
/// Registered as a Godot autoload for global access.
/// </summary>
public partial class BodyStructureResourceManager : Node
{
    // Singleton instance
    private static BodyStructureResourceManager? _instance;

    /// <summary>
    /// Gets the singleton instance. Set by _Ready() when registered as autoload.
    /// </summary>
    public static BodyStructureResourceManager Instance => _instance
        ?? throw new InvalidOperationException("BodyStructureResourceManager not initialized. Ensure it's registered as an autoload in project.godot");

    // Body structure definition collection
    private readonly Dictionary<string, BodyStructureDefinition> _definitions = new ();

    public override void _Ready()
    {
        _instance = this;
        LoadAllDefinitions();
        Log.Print($"BodyStructureResourceManager: Initialized as autoload with {_definitions.Count} body structure definitions");
    }

    /// <summary>
    /// Load all body structure definitions from the resources folder.
    /// </summary>
    private void LoadAllDefinitions()
    {
        string definitionsPath = "res://resources/entities/body_structures";
        string projectPath = ProjectSettings.GlobalizePath(definitionsPath);

        if (!Directory.Exists(projectPath))
        {
            throw new InvalidOperationException($"BodyStructureResourceManager: Required directory not found: {projectPath}");
        }

        // Load all JSON files in the body_structures directory
        var jsonFiles = Directory.GetFiles(projectPath, "*.json");

        if (jsonFiles.Length == 0)
        {
            throw new InvalidOperationException($"BodyStructureResourceManager: No body structure definitions found in {projectPath}");
        }

        foreach (var file in jsonFiles)
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
    /// Load a single definition from a file. Throws on any failure.
    /// </summary>
    private void LoadDefinitionFromFile(string file)
    {
        var definition = BodyStructureDefinition.LoadFromJson(file);
        definition.Validate();

        if (definition.Id == null)
        {
            throw new InvalidOperationException($"BodyStructureResourceManager: Definition in {file} has null Id after validation");
        }

        if (_definitions.ContainsKey(definition.Id))
        {
            throw new InvalidOperationException($"BodyStructureResourceManager: Duplicate body structure Id '{definition.Id}' found in {file}");
        }

        _definitions[definition.Id] = definition;
        Log.Print($"Loaded body structure definition: {definition.Id}");
    }

    /// <summary>
    /// Gets a body structure definition by ID.
    /// Throws if definition is not found.
    /// </summary>
    /// <param name="id">The body structure definition ID.</param>
    /// <returns>The body structure definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the definition is not found.</exception>
    public BodyStructureDefinition GetDefinition(string id)
    {
        if (_definitions.TryGetValue(id, out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"BodyStructureResourceManager: Body structure definition '{id}' not found. " +
            $"Available definitions: [{string.Join(", ", _definitions.Keys)}]");
    }

    /// <summary>
    /// Check if a definition with the given ID exists.
    /// </summary>
    /// <param name="id">The body structure definition ID.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }

    /// <summary>
    /// Get all loaded body structure definitions.
    /// </summary>
    /// <returns>Enumerable of all body structure definitions.</returns>
    public IEnumerable<BodyStructureDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }
}
