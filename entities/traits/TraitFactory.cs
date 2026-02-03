using System;
using System.Collections.Generic;
using System.Reflection;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Factory for creating trait instances from JSON definitions using reflection.
/// </summary>
public static class TraitFactory
{
    private static readonly Dictionary<string, Type> _traitTypeCache = new (StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    /// <summary>
    /// Pre-registers all BeingTrait subclasses from loaded assemblies.
    /// Should be called once at startup before loading definitions.
    /// </summary>
    public static void RegisterAllTraits()
    {
        if (_initialized)
        {
            return;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(BeingTrait).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        _traitTypeCache[type.Name] = type;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies may not be fully loadable, skip them
            }
        }

        _initialized = true;
        Log.Print($"TraitFactory: Registered {_traitTypeCache.Count} trait types");
    }

    /// <summary>
    /// Manually registers a trait type. Useful for runtime-loaded assemblies.
    /// </summary>
    /// <typeparam name="T">The trait type to register.</typeparam>
    public static void RegisterTraitType<T>()
        where T : BeingTrait
    {
        var type = typeof(T);
        _traitTypeCache[type.Name] = type;
    }

    /// <summary>
    /// Manually registers a trait type by Type instance. Useful for runtime-loaded assemblies.
    /// </summary>
    /// <param name="type">The trait type to register.</param>
    public static void RegisterTraitType(Type type)
    {
        if (!typeof(BeingTrait).IsAssignableFrom(type))
        {
            Log.Warn($"TraitFactory: Cannot register type '{type.Name}' - does not inherit from BeingTrait");
            return;
        }

        _traitTypeCache[type.Name] = type;
    }

    /// <summary>
    /// Creates a trait instance from a TraitDefinition, optionally merging runtime configuration.
    /// </summary>
    /// <param name="definition">The trait definition from JSON.</param>
    /// <param name="runtimeConfig">Optional runtime configuration that overrides JSON parameters.</param>
    /// <returns>A configured trait instance, or null if creation failed.</returns>
    public static BeingTrait? CreateTrait(TraitDefinition definition, TraitConfiguration? runtimeConfig = null)
    {
        if (string.IsNullOrEmpty(definition.TraitType))
        {
            Log.Warn("TraitFactory: TraitType is null or empty in trait definition");
            return null;
        }

        // Find the trait type by name
        Type? traitType = FindTraitType(definition.TraitType);
        if (traitType == null)
        {
            Log.Error($"TraitFactory: Could not find trait type '{definition.TraitType}'");
            return null;
        }

        // Verify it inherits from BeingTrait
        if (!typeof(BeingTrait).IsAssignableFrom(traitType))
        {
            Log.Error($"TraitFactory: Type '{definition.TraitType}' does not inherit from BeingTrait");
            return null;
        }

        // Create instance using parameterless constructor
        BeingTrait? trait;
        try
        {
            trait = Activator.CreateInstance(traitType) as BeingTrait;
            if (trait == null)
            {
                Log.Error($"TraitFactory: Failed to create instance of trait type '{definition.TraitType}'");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"TraitFactory: Exception creating trait '{definition.TraitType}': {ex.Message}");
            return null;
        }

        // Merge JSON parameters with runtime parameters
        TraitConfiguration config = new ();

        // First, add parameters from definition
        if (definition.Parameters != null)
        {
            foreach (var kvp in definition.Parameters)
            {
                config.Parameters[kvp.Key] = kvp.Value;
            }
        }

        // Then, merge runtime config (runtime overrides JSON)
        if (runtimeConfig != null)
        {
            foreach (var kvp in runtimeConfig.Parameters)
            {
                config.Parameters[kvp.Key] = kvp.Value;
            }
        }

        // Validate configuration
        if (!trait.ValidateConfiguration(config))
        {
            Log.Error($"TraitFactory: Configuration validation failed for trait '{definition.TraitType}'");
            return null;
        }

        // Configure the trait
        trait.Configure(config);

        return trait;
    }

    /// <summary>
    /// Finds a trait type by name using the pre-registered cache.
    /// If not initialized, triggers registration first.
    /// </summary>
    /// <param name="typeName">The name of the trait type to find.</param>
    /// <returns>The Type if found, null otherwise.</returns>
    private static Type? FindTraitType(string typeName)
    {
        if (!_initialized)
        {
            RegisterAllTraits();
        }

        return _traitTypeCache.TryGetValue(typeName, out var type) ? type : null;
    }
}
