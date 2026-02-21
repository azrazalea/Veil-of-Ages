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
/// Inherits from ResourceManager with BeingDefinition as primary type.
/// Also manages SpriteAnimationDefinitions and autonomy data as secondary types.
/// </summary>
public partial class BeingResourceManager : ResourceManager<BeingResourceManager, BeingDefinition>
{
    protected override string ResourcePath => "res://resources/entities/definitions";

    // Cache for resolved definitions (with inheritance applied)
    private readonly Dictionary<string, BeingDefinition> _resolvedCache = new ();

    // Sprite definition collection
    private readonly Dictionary<string, SpriteDefinition> _sprites = new ();

    // Autonomy rule definitions (loaded from JSON)
    private Dictionary<string, AutonomyRuleDefinition> _autonomyRules = new ();

    // Autonomy config definitions (loaded from JSON)
    private Dictionary<string, AutonomyConfigDefinition> _autonomyConfigs = new ();

    public override void _Ready()
    {
        MemoryProfiler.Checkpoint("BeingResourceManager _Ready start");

        // Pre-register all trait types before loading definitions
        TraitFactory.RegisterAllTraits();

        // Base _Ready sets singleton, calls LoadDefinitions, OnDefinitionsLoaded, LogInitialized
        base._Ready();
        MemoryProfiler.Checkpoint("BeingResourceManager _Ready end");
    }

    /// <summary>
    /// Called after LoadDefinitions() completes. Loads secondary resource types:
    /// sprites and autonomy data.
    /// </summary>
    protected override void OnDefinitionsLoaded()
    {
        LoadAllSprites();
        LoadAutonomyData();
    }

    /// <summary>
    /// Log initialization summary including all managed resource types.
    /// </summary>
    protected override void LogInitialized()
    {
        Log.Print($"BeingResourceManager: Initialized as autoload with {_definitions.Count} being definitions, {_sprites.Count} sprite definitions, {_autonomyRules.Count} autonomy rules, {_autonomyConfigs.Count} autonomy configs");
    }

    /// <summary>
    /// Gets a resolved being definition by ID, with inheritance applied.
    /// </summary>
    /// <param name="id">The being definition ID.</param>
    /// <returns>The being definition with inheritance resolved, or null if not found.</returns>
    public override BeingDefinition? GetDefinition(string id)
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
    /// Load all sprite definitions from the resources folder.
    /// </summary>
    private void LoadAllSprites()
    {
        string spritesPath = "res://resources/entities/sprites";
        string projectPath = JsonResourceLoader.ResolveResPath(spritesPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Warn($"Sprites directory not found: {projectPath} - creating empty directory");
            Directory.CreateDirectory(projectPath);
            return;
        }

        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            LoadSpriteFromFile(file);
        }

        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                LoadSpriteFromFile(file);
            }
        }
    }

    /// <summary>
    /// Load a single sprite definition from a file.
    /// </summary>
    private void LoadSpriteFromFile(string file)
    {
        var sprite = SpriteDefinition.LoadFromJson(file);
        if (sprite != null && sprite.Validate())
        {
            if (sprite.Id != null)
            {
                _sprites[sprite.Id] = sprite;
                Log.Print($"Loaded sprite definition: {sprite.Id}");
            }
        }
        else
        {
            Log.Error($"Failed to load sprite definition from: {file}");
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
    /// Get a sprite definition by ID.
    /// </summary>
    /// <param name="id">The sprite definition ID.</param>
    /// <returns>The sprite definition or null if not found.</returns>
    public SpriteDefinition? GetSprite(string id)
    {
        return _sprites.TryGetValue(id, out var sprite) ? sprite : null;
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
}
