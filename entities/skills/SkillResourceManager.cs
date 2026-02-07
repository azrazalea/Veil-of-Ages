using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Skills;

/// <summary>
/// Autoload singleton manager for loading skill definitions from JSON.
/// Register as an autoload in project.godot.
/// </summary>
public partial class SkillResourceManager : Node
{
    // Singleton instance
    private static SkillResourceManager? _instance;

    public static SkillResourceManager Instance => _instance
        ?? throw new InvalidOperationException("SkillResourceManager not initialized. Ensure it's registered as an autoload in project.godot");

    // Skill definition collection
    private readonly Dictionary<string, SkillDefinition> _definitions = new ();

    public override void _Ready()
    {
        _instance = this;
        LoadAllDefinitions();
        Log.Print($"SkillResourceManager initialized with {_definitions.Count} skill definitions");
    }

    /// <summary>
    /// Load all skill definitions from the resources folder.
    /// </summary>
    private void LoadAllDefinitions()
    {
        string skillsPath = "res://resources/skills";
        string projectPath = ProjectSettings.GlobalizePath(skillsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Skills directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        // Load all JSON files in the skills directory
        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            var definition = SkillDefinition.LoadFromJson(file);
            if (definition != null && definition.Validate())
            {
                if (definition.Id != null)
                {
                    _definitions[definition.Id] = definition;
                    Log.Print($"Loaded skill definition: {definition.Id}");
                }
            }
            else
            {
                Log.Error($"Failed to load skill definition from: {file}");
            }
        }

        // Also load from subdirectories for organization
        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var definition = SkillDefinition.LoadFromJson(file);
                if (definition != null && definition.Validate())
                {
                    if (definition.Id != null)
                    {
                        _definitions[definition.Id] = definition;
                        Log.Print($"Loaded skill definition: {definition.Id}");
                    }
                }
                else
                {
                    Log.Error($"Failed to load skill definition from: {file}");
                }
            }
        }
    }

    /// <summary>
    /// Get a skill definition by ID.
    /// </summary>
    /// <param name="id">The skill definition ID.</param>
    /// <returns>The skill definition or null if not found.</returns>
    public SkillDefinition? GetDefinition(string id)
    {
        if (_definitions.TryGetValue(id, out var definition))
        {
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Get all loaded skill definitions.
    /// </summary>
    /// <returns>Enumerable of all skill definitions.</returns>
    public IEnumerable<SkillDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Get all skill definitions matching a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Enumerable of matching skill definitions.</returns>
    public IEnumerable<SkillDefinition> GetDefinitionsByCategory(SkillCategory category)
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
    /// Get all skill definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching skill definitions.</returns>
    public IEnumerable<SkillDefinition> GetDefinitionsByTag(string tag)
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
    /// <param name="id">The skill definition ID.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }
}
