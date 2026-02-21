using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Abstract base class for singleton resource managers that load JSON definitions.
/// Provides singleton pattern, standard loading with subdirectory support, and common getters.
/// Subclasses must be registered as Godot autoloads in project.godot.
/// </summary>
/// <typeparam name="TManager">The concrete manager type (for type-safe singleton).</typeparam>
/// <typeparam name="TDefinition">The definition type being managed.</typeparam>
public abstract partial class ResourceManager<TManager, TDefinition> : Node
    where TManager : ResourceManager<TManager, TDefinition>
    where TDefinition : class, IResourceDefinition
{
    private static TManager? _instance;

    /// <summary>
    /// Gets the singleton instance. Throws if not initialized as autoload.
    /// </summary>
#pragma warning disable CA1000 // CRTP singleton requires static member on generic type
    public static TManager Instance => _instance
        ?? throw new InvalidOperationException(
            $"{typeof(TManager).Name} not initialized. Ensure it's registered as an autoload in project.godot");
#pragma warning restore CA1000

    /// <summary>
    /// Primary definition collection, keyed by ID.
    /// Protected so subclasses can access for custom queries.
    /// </summary>
    protected readonly Dictionary<string, TDefinition> _definitions = new ();

    /// <summary>
    /// Gets the res:// path to the directory containing JSON definitions.
    /// </summary>
    protected abstract string ResourcePath { get; }

    /// <summary>
    /// Gets jsonSerializerOptions for deserializing definitions.
    /// Override if definitions need custom converters (e.g., Vector2I).
    /// </summary>
    protected virtual JsonSerializerOptions SerializerOptions => JsonOptions.Default;

    /// <summary>
    /// Gets a value indicating whether whether to include one level of subdirectories when loading.
    /// Default: true.
    /// </summary>
    protected virtual bool IncludeSubdirectories => true;

    public override void _Ready()
    {
        MemoryProfiler.Checkpoint($"{typeof(TManager).Name} _Ready start");
        _instance = (TManager)this;
        LoadDefinitions();
        OnDefinitionsLoaded();
        LogInitialized();
        MemoryProfiler.Checkpoint($"{typeof(TManager).Name} _Ready end");
    }

    /// <summary>
    /// Load all definitions from ResourcePath. Override for custom loading logic.
    /// </summary>
    protected virtual void LoadDefinitions()
    {
        var loaded = JsonResourceLoader.LoadAllFromDirectory<TDefinition>(
            ResourcePath,
            getId: d => d.Id,
            validate: d => d.Validate(),
            options: SerializerOptions,
            includeSubdirectories: IncludeSubdirectories);

        foreach (var kvp in loaded)
        {
            _definitions[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Called after LoadDefinitions() completes. Override to add post-load logic
    /// (e.g., building indexes, loading additional resource types).
    /// </summary>
    protected virtual void OnDefinitionsLoaded()
    {
    }

    /// <summary>
    /// Log initialization summary. Override to customize the log message.
    /// </summary>
    protected virtual void LogInitialized()
    {
        Log.Print($"{typeof(TManager).Name} initialized with {_definitions.Count} {typeof(TDefinition).Name} definitions");
    }

    /// <summary>
    /// Get a definition by ID. Returns null if not found.
    /// </summary>
    public virtual TDefinition? GetDefinition(string id)
    {
        return _definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// Check if a definition with the given ID exists.
    /// </summary>
    public bool HasDefinition(string id)
    {
        return _definitions.ContainsKey(id);
    }

    /// <summary>
    /// Get all loaded definitions.
    /// </summary>
    public IEnumerable<TDefinition> GetAllDefinitions()
    {
        return _definitions.Values;
    }
}
