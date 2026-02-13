using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings.Health;

/// <summary>
/// Singleton manager for loading body structure definitions from JSON.
/// Registered as a Godot autoload for global access.
/// Uses strict loading: throws on missing directory, zero definitions, validation failures, or duplicates.
/// </summary>
public partial class BodyStructureResourceManager : ResourceManager<BodyStructureResourceManager, BodyStructureDefinition>
{
    protected override string ResourcePath => "res://resources/entities/body_structures";

    /// <summary>
    /// Strict loading override: throws if the directory is missing, if zero definitions are found,
    /// or if any definition fails ValidateStrict(). Uses BodyStructureDefinition.LoadFromJson()
    /// for per-file deserialization.
    /// </summary>
    protected override void LoadDefinitions()
    {
        string projectPath = JsonResourceLoader.ResolveResPath(ResourcePath);

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
        definition.ValidateStrict();

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
    /// Throws if definition is not found (non-nullable return).
    /// </summary>
    /// <param name="id">The body structure definition ID.</param>
    /// <returns>The body structure definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the definition is not found.</exception>
    public new BodyStructureDefinition GetDefinition(string id)
    {
        if (_definitions.TryGetValue(id, out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"BodyStructureResourceManager: Body structure definition '{id}' not found. " +
            $"Available definitions: [{string.Join(", ", _definitions.Keys)}]");
    }
}
