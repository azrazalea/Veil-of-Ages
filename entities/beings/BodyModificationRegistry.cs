using System;
using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// Registry for body modification handlers. Allows extensible body modifications
/// without hardcoding types in GenericBeing.
/// </summary>
public static class BodyModificationRegistry
{
    /// <summary>
    /// Handler delegate for applying a body modification to a being.
    /// </summary>
    /// <param name="being">The being to modify.</param>
    /// <param name="parameters">Parameters from the modification definition.</param>
    public delegate void ModificationHandler(Being being, Dictionary<string, object?> parameters);

    private static readonly Dictionary<string, ModificationHandler> _handlers = new (StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    /// <summary>
    /// Ensures default handlers are registered.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Registers the built-in body modification handlers.
    /// </summary>
    private static void RegisterDefaultHandlers()
    {
        Register("RemoveSoftTissues", HandleRemoveSoftTissues);
        Register("ScaleBoneHealth", HandleScaleBoneHealth);
        Register("ApplyRandomDecay", HandleApplyRandomDecay);
    }

    /// <summary>
    /// Registers a body modification handler.
    /// </summary>
    /// <param name="modificationType">The type name (case-insensitive).</param>
    /// <param name="handler">The handler function.</param>
    public static void Register(string modificationType, ModificationHandler handler)
    {
        _handlers[modificationType] = handler;
        Log.Print($"BodyModificationRegistry: Registered handler for '{modificationType}'");
    }

    /// <summary>
    /// Applies a body modification to a being.
    /// </summary>
    /// <param name="being">The being to modify.</param>
    /// <param name="modification">The modification to apply.</param>
    /// <returns>True if the modification was applied, false if no handler found.</returns>
    public static bool Apply(Being being, BodyModification modification)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(modification.Type))
        {
            Log.Warn("BodyModificationRegistry: Modification has null/empty Type");
            return false;
        }

        if (!_handlers.TryGetValue(modification.Type, out var handler))
        {
            Log.Warn($"BodyModificationRegistry: No handler registered for modification type '{modification.Type}'");
            return false;
        }

        try
        {
            handler(being, modification.Parameters);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"BodyModificationRegistry: Handler for '{modification.Type}' threw exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a handler is registered for a modification type.
    /// </summary>
    public static bool HasHandler(string modificationType)
    {
        EnsureInitialized();
        return _handlers.ContainsKey(modificationType);
    }

    private static void HandleRemoveSoftTissues(Being being, Dictionary<string, object?> parameters)
    {
        being.Health?.RemoveSoftTissuesAndOrgans();
    }

    private static void HandleScaleBoneHealth(Being being, Dictionary<string, object?> parameters)
    {
        if (being.Health?.BodyPartGroups == null)
        {
            return;
        }

        float multiplier = GetFloatParam(parameters, "Multiplier", 1.5f);

        foreach (var group in being.Health.BodyPartGroups.Values)
        {
            foreach (var part in group.Parts)
            {
                if (part.IsBonePart())
                {
                    part.ScaleMaxHealth(multiplier);
                }
            }
        }
    }

    private static void HandleApplyRandomDecay(Being being, Dictionary<string, object?> parameters)
    {
        if (being.Health?.BodyPartGroups == null)
        {
            return;
        }

        int minParts = GetIntParam(parameters, "MinParts", 2);
        int maxParts = GetIntParam(parameters, "MaxParts", 5);
        float minDamage = GetFloatParam(parameters, "MinDamagePercent", 0.3f);
        float maxDamage = GetFloatParam(parameters, "MaxDamagePercent", 0.7f);

        var rng = new Godot.RandomNumberGenerator();
        rng.Randomize();

        int partsToAffect = rng.RandiRange(minParts, maxParts);
        int affected = 0;

        // Collect all non-vital body parts
        var candidates = new List<(string groupName, string partName)>();
        foreach (var group in being.Health.BodyPartGroups)
        {
            foreach (var part in group.Value.Parts)
            {
                if (part.Name is not "Brain" and not "Heart" and not "Spine")
                {
                    candidates.Add((group.Key, part.Name));
                }
            }
        }

        // Shuffle candidates
        for (int i = 0; i < candidates.Count; i++)
        {
            int swapIndex = rng.RandiRange(0, candidates.Count - 1);
            (candidates[swapIndex], candidates[i]) = (candidates[i], candidates[swapIndex]);
        }

        // Apply damage to random parts
        foreach (var (groupName, partName) in candidates)
        {
            if (affected >= partsToAffect)
            {
                break;
            }

            if (being.Health.BodyPartGroups.TryGetValue(groupName, out var group))
            {
                var part = group.Parts.FirstOrDefault(p => p.Name == partName);
                if (part != null)
                {
                    float damageAmount = part.MaxHealth * rng.RandfRange(minDamage, maxDamage);
                    being.DamageBodyPart(groupName, partName, damageAmount);
                    affected++;
                }
            }
        }
    }

    private static int GetIntParam(Dictionary<string, object?> parameters, string key, int defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetInt32(),
            _ => defaultValue
        };
    }

    private static float GetFloatParam(Dictionary<string, object?> parameters, string key, float defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        return value switch
        {
            float f => f,
            double d => (float)d,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetSingle(),
            _ => defaultValue
        };
    }
}
