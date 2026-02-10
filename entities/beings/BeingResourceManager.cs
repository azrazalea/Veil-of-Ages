using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Autonomy;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// Singleton manager for loading entity and animation definitions from JSON.
/// Registered as a Godot autoload for global access.
/// </summary>
public partial class BeingResourceManager : Node
{
    // Singleton instance
    private static BeingResourceManager? _instance;

    /// <summary>
    /// Gets the singleton instance. Set by _Ready() when registered as autoload.
    /// </summary>
    public static BeingResourceManager Instance => _instance
        ?? throw new InvalidOperationException("BeingResourceManager not initialized. Ensure it's registered as an autoload in project.godot");

    // Being definition collection (raw, unresolved)
    private readonly Dictionary<string, BeingDefinition> _definitions = new ();

    // Cache for resolved definitions (with inheritance applied)
    private readonly Dictionary<string, BeingDefinition> _resolvedCache = new ();

    // Animation definition collection
    private readonly Dictionary<string, SpriteAnimationDefinition> _animations = new ();

    // Autonomy rule definitions (loaded from JSON)
    private Dictionary<string, AutonomyRuleDefinition> _autonomyRules = new ();

    // Autonomy config definitions (loaded from JSON)
    private Dictionary<string, AutonomyConfigDefinition> _autonomyConfigs = new ();

    public override void _Ready()
    {
        _instance = this;

        // Pre-register all trait types before loading definitions
        TraitFactory.RegisterAllTraits();

        LoadAllDefinitions();
        LoadAllAnimations();
        LoadAutonomyData();
        Log.Print($"BeingResourceManager: Initialized as autoload with {_definitions.Count} being definitions, {_animations.Count} animation definitions, {_autonomyRules.Count} autonomy rules, {_autonomyConfigs.Count} autonomy configs");
    }

    /// <summary>
    /// Load all being definitions from the resources folder.
    /// </summary>
    private void LoadAllDefinitions()
    {
        string definitionsPath = "res://resources/entities/definitions";
        string projectPath = ProjectSettings.GlobalizePath(definitionsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Definitions directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        // Load all JSON files in the definitions directory
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
    /// Load a single definition from a file.
    /// </summary>
    private void LoadDefinitionFromFile(string file)
    {
        var definition = BeingDefinition.LoadFromJson(file);
        if (definition != null && definition.Validate())
        {
            if (definition.Id != null)
            {
                _definitions[definition.Id] = definition;
                Log.Print($"Loaded being definition: {definition.Id}");
            }
        }
        else
        {
            Log.Error($"Failed to load being definition from: {file}");
        }
    }

    /// <summary>
    /// Load all animation definitions from the resources folder.
    /// </summary>
    private void LoadAllAnimations()
    {
        string animationsPath = "res://resources/entities/animations";
        string projectPath = ProjectSettings.GlobalizePath(animationsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Animations directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        // Load all JSON files in the animations directory
        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            LoadAnimationFromFile(file);
        }

        // Also load from subdirectories for organization
        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                LoadAnimationFromFile(file);
            }
        }
    }

    /// <summary>
    /// Load a single animation from a file.
    /// </summary>
    private void LoadAnimationFromFile(string file)
    {
        var animation = SpriteAnimationDefinition.LoadFromJson(file);
        if (animation != null && animation.Validate())
        {
            if (animation.Id != null)
            {
                _animations[animation.Id] = animation;
                Log.Print($"Loaded animation definition: {animation.Id}");
            }
        }
        else
        {
            Log.Error($"Failed to load animation definition from: {file}");
        }
    }

    /// <summary>
    /// Load autonomy rule definitions and config definitions from JSON.
    /// </summary>
    private void LoadAutonomyData()
    {
        _autonomyRules = JsonResourceLoader.LoadAllFromDirectory<AutonomyRuleDefinition>(
            "res://resources/entities/autonomy/rules", d => d.Id);

        _autonomyConfigs = JsonResourceLoader.LoadAllFromDirectory<AutonomyConfigDefinition>(
            "res://resources/entities/autonomy/configs", d => d.Id);
    }

    /// <summary>
    /// Gets all loaded autonomy rule definitions.
    /// </summary>
    public Dictionary<string, AutonomyRuleDefinition> GetAutonomyRules()
    {
        return _autonomyRules;
    }

    /// <summary>
    /// Gets an autonomy config definition by entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID (e.g., "player").</param>
    /// <returns>The config definition, or null if not found.</returns>
    public AutonomyConfigDefinition? GetAutonomyConfig(string entityId)
    {
        return _autonomyConfigs.TryGetValue(entityId, out var config) ? config : null;
    }

    /// <summary>
    /// Gets a resolved being definition by ID, with inheritance applied.
    /// </summary>
    /// <param name="id">The being definition ID.</param>
    /// <returns>The being definition with inheritance resolved, or null if not found.</returns>
    public BeingDefinition? GetDefinition(string id)
    {
        // Check resolved cache first
        if (_resolvedCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (!_definitions.TryGetValue(id, out var definition))
        {
            return null;
        }

        // If no parent, cache and return as-is
        if (string.IsNullOrEmpty(definition.ParentId))
        {
            _resolvedCache[id] = definition;
            return definition;
        }

        // Resolve inheritance and cache
        var resolved = ResolveInheritance(definition);
        _resolvedCache[id] = resolved;
        return resolved;
    }

    /// <summary>
    /// Gets a raw definition without resolving inheritance.
    /// Useful for debugging or when you need the unmerged definition.
    /// </summary>
    /// <param name="id">The being definition ID.</param>
    /// <returns>The raw being definition or null if not found.</returns>
    public BeingDefinition? GetRawDefinition(string id)
    {
        return _definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// Resolves inheritance for a definition by merging with parent(s).
    /// Handles inheritance chains (grandparent -> parent -> child).
    /// </summary>
    private BeingDefinition ResolveInheritance(BeingDefinition definition)
    {
        // Track visited definitions to detect circular inheritance
        var visited = new HashSet<string>();
        var current = definition;

        // Build inheritance chain (child -> parent -> grandparent -> ...)
        var chain = new List<BeingDefinition>();

        while (current != null)
        {
            if (current.Id != null && visited.Contains(current.Id))
            {
                Log.Error($"BeingResourceManager: Circular inheritance detected at '{current.Id}'");
                break;
            }

            if (current.Id != null)
            {
                visited.Add(current.Id);
            }

            chain.Add(current);

            if (string.IsNullOrEmpty(current.ParentId))
            {
                break;
            }

            if (!_definitions.TryGetValue(current.ParentId, out var parent))
            {
                Log.Error($"BeingResourceManager: Parent definition '{current.ParentId}' not found for '{current.Id}'");
                break;
            }

            current = parent;
        }

        // Reverse to process from root ancestor to leaf
        chain.Reverse();

        // Merge from root to leaf
        var result = chain[0];
        for (int i = 1; i < chain.Count; i++)
        {
            result = chain[i].MergeWithParent(result);
        }

        return result;
    }

    /// <summary>
    /// Get an animation definition by ID.
    /// </summary>
    /// <param name="id">The animation definition ID.</param>
    /// <returns>The animation definition or null if not found.</returns>
    public SpriteAnimationDefinition? GetAnimation(string id)
    {
        if (_animations.TryGetValue(id, out var animation))
        {
            return animation;
        }

        return null;
    }

    /// <summary>
    /// Get all being definitions matching a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Enumerable of matching being definitions.</returns>
    public IEnumerable<BeingDefinition> GetDefinitionsByCategory(string category)
    {
        foreach (var definition in _definitions.Values)
        {
            if (string.Equals(definition.Category, category, System.StringComparison.OrdinalIgnoreCase))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all being definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching being definitions.</returns>
    public IEnumerable<BeingDefinition> GetDefinitionsByTag(string tag)
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
    /// Get all loaded being definitions.
    /// </summary>
    /// <returns>Enumerable of all being definitions.</returns>
    public IEnumerable<BeingDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }

    /// <summary>
    /// Check if a definition with the given ID exists.
    /// </summary>
    /// <param name="id">The being definition ID.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }
}
