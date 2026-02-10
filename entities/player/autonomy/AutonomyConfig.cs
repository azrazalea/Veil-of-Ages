using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Autonomy;

/// <summary>
/// Manages the player's autonomous trait loadout.
/// Rules map to trait types - when enabled, the corresponding trait is added to the player.
/// The traits themselves handle all behavior (work phases, activities, etc.).
/// This is a configuration layer, not a behavior layer.
/// </summary>
public class AutonomyConfig
{
    private readonly List<AutonomyRule> _rules = new ();

    /// <summary>
    /// Tracks which rules have already been applied (trait added to player).
    /// </summary>
    private readonly HashSet<string> _appliedRules = new ();

    /// <summary>
    /// Gets all rules in priority order.
    /// </summary>
    public IReadOnlyList<AutonomyRule> Rules => _rules;

    /// <summary>
    /// Add a rule to the configuration.
    /// </summary>
    public void AddRule(AutonomyRule rule)
    {
        _rules.Add(rule);
        _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Remove a rule by its ID.
    /// </summary>
    public bool RemoveRule(string id)
    {
        _appliedRules.Remove(id);
        return _rules.RemoveAll(r => r.Id == id) > 0;
    }

    /// <summary>
    /// Enable or disable a rule by its ID.
    /// </summary>
    public void SetEnabled(string id, bool enabled)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == id);
        if (rule != null)
        {
            rule.Enabled = enabled;
        }
    }

    /// <summary>
    /// Change a rule's priority and re-sort.
    /// </summary>
    public void ReorderRule(string id, int newPriority)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == id);
        if (rule != null)
        {
            rule.Priority = newPriority;
            _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }

    /// <summary>
    /// Get a rule by its ID.
    /// </summary>
    public AutonomyRule? GetRule(string id)
    {
        return _rules.FirstOrDefault(r => r.Id == id);
    }

    /// <summary>
    /// Apply the configuration to a player entity.
    /// For each enabled rule whose trait isn't already on the player,
    /// creates and adds the trait using TraitFactory.
    /// Called after SetHome so traits that need workplace/home can be configured.
    /// </summary>
    /// <param name="player">The player to apply the configuration to.</param>
    public void Apply(Being player)
    {
        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            // Skip rules that have already been applied
            if (_appliedRules.Contains(rule.Id))
            {
                continue;
            }

            // Skip if the player already has this trait (e.g., from player.json)
            if (PlayerHasTrait(player, rule.TraitType))
            {
                _appliedRules.Add(rule.Id);
                continue;
            }

            // Create the trait via TraitFactory
            var definition = new TraitDefinition
            {
                TraitType = rule.TraitType,
                Priority = rule.Priority,
                Parameters = new Dictionary<string, object?>(rule.Parameters)
            };

            // Build runtime config with home building if available
            var runtimeConfig = new TraitConfiguration();
            var home = player.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
            if (home != null)
            {
                runtimeConfig.Parameters["home"] = home;
                runtimeConfig.Parameters["workplace"] = home;
            }

            var trait = TraitFactory.CreateTrait(definition, runtimeConfig);
            if (trait == null)
            {
                Log.Warn($"AutonomyConfig: Failed to create trait '{rule.TraitType}' for rule '{rule.Id}'");
                continue;
            }

            // Add trait and initialize it
            player.SelfAsEntity().AddTrait(trait, rule.Priority);
            trait.Initialize(player, player.Health);

            _appliedRules.Add(rule.Id);
            Log.Print($"AutonomyConfig: Applied rule '{rule.Id}' - added trait {rule.TraitType}");
        }
    }

    /// <summary>
    /// Remove all autonomy-managed traits, cancel the current activity,
    /// then re-apply all currently enabled rules from scratch.
    /// Called when the player changes their autonomy profile.
    /// </summary>
    /// <param name="player">The player to reconfigure.</param>
    public void Reapply(Being player)
    {
        // Remove all previously applied traits
        foreach (var ruleId in _appliedRules)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule == null)
            {
                continue;
            }

            var trait = FindTraitByTypeName(player, rule.TraitType);
            if (trait != null)
            {
                trait.OnRemoved();
                player.SelfAsEntity().RemoveTrait(trait);
                Log.Print($"AutonomyConfig: Removed trait {rule.TraitType} for rule '{ruleId}'");
            }
        }

        _appliedRules.Clear();

        // Cancel current activity since the trait driving it may have been removed
        player.SetCurrentActivity(null);

        // Re-apply all enabled rules fresh
        Apply(player);
    }

    /// <summary>
    /// Find a trait on the player by its type name.
    /// </summary>
    private static BeingTrait? FindTraitByTypeName(Being player, string traitTypeName)
    {
        foreach (var trait in player.SelfAsEntity().Traits)
        {
            if (trait.GetType().Name == traitTypeName)
            {
                return trait;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the player already has a trait of the given type name.
    /// </summary>
    private static bool PlayerHasTrait(Being player, string traitTypeName)
    {
        foreach (var trait in player.SelfAsEntity().Traits)
        {
            if (trait.GetType().Name == traitTypeName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Create an AutonomyConfig from JSON definitions.
    /// Looks up each rule ID in the config and creates AutonomyRule instances.
    /// </summary>
    /// <param name="configDef">The config definition referencing rule IDs.</param>
    /// <param name="allRules">Dictionary of all loaded rule definitions.</param>
    /// <returns>A populated AutonomyConfig.</returns>
    public static AutonomyConfig FromDefinitions(AutonomyConfigDefinition configDef, Dictionary<string, AutonomyRuleDefinition> allRules)
    {
        var config = new AutonomyConfig();

        foreach (var ruleId in configDef.Rules)
        {
            if (!allRules.TryGetValue(ruleId, out var ruleDef))
            {
                Log.Warn($"AutonomyConfig: Rule '{ruleId}' referenced in config '{configDef.Id}' not found in loaded rules");
                continue;
            }

            var rule = new AutonomyRule(
                ruleDef.Id ?? ruleId,
                ruleDef.DisplayName ?? ruleId,
                ruleDef.TraitType ?? string.Empty,
                ruleDef.Priority,
                ruleDef.ActiveDuringPhases,
                ruleDef.Parameters);

            config.AddRule(rule);
        }

        return config;
    }
}
