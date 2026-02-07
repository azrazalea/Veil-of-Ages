using System;
using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Skills;

/// <summary>
/// Per-entity skill manager that tracks all skills for a Being.
/// Follows the BeingNeedsSystem pattern as a composition-based service.
/// </summary>
public class BeingSkillSystem
{
    private readonly Dictionary<string, Skill> _skills = new ();
    private readonly Being _owner;

    public BeingSkillSystem(Being owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Add a skill to this entity's skill collection.
    /// </summary>
    /// <param name="skill">The skill to add.</param>
    public void AddSkill(Skill skill)
    {
        if (skill.Definition.Id != null)
        {
            _skills[skill.Definition.Id] = skill;
        }
    }

    /// <summary>
    /// Get a skill by its definition ID.
    /// </summary>
    /// <param name="id">The skill definition ID.</param>
    /// <returns>The skill instance or null if not found.</returns>
    public Skill? GetSkill(string id)
    {
        return _skills.TryGetValue(id, out var skill) ? skill : null;
    }

    /// <summary>
    /// Check if this entity has a specific skill.
    /// </summary>
    /// <param name="id">The skill definition ID.</param>
    /// <returns>True if the entity has the skill, false otherwise.</returns>
    public bool HasSkill(string id)
    {
        return _skills.ContainsKey(id);
    }

    /// <summary>
    /// Get all skills this entity has.
    /// </summary>
    /// <returns>Enumerable of all skills.</returns>
    public IEnumerable<Skill> GetAllSkills()
    {
        return _skills.Values;
    }

    /// <summary>
    /// Get all skills matching a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Enumerable of matching skills.</returns>
    public IEnumerable<Skill> GetSkillsByCategory(SkillCategory category)
    {
        return _skills.Values.Where(s => s.Definition.Category == category);
    }

    /// <summary>
    /// Grant XP to a skill, auto-creating the skill if the entity doesn't have it yet.
    /// Applies an attribute-based multiplier to the base XP amount.
    /// </summary>
    /// <param name="skillId">The skill definition ID.</param>
    /// <param name="baseXp">The base XP to grant before attribute multiplier.</param>
    /// <returns>The number of levels gained.</returns>
    public int GainXp(string skillId, float baseXp)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
        {
            var definition = SkillResourceManager.Instance.GetDefinition(skillId);
            if (definition == null)
            {
                Log.Error($"Cannot gain XP: skill definition '{skillId}' not found");
                return 0;
            }

            skill = new Skill(definition);
            AddSkill(skill);
        }

        float multiplier = CalculateAttributeMultiplier(skill.Definition);
        float adjustedXp = baseXp * multiplier;
        int levelsGained = skill.AddXp(adjustedXp);

        if (levelsGained > 0)
        {
            Log.Print($"{_owner.Name} gained {levelsGained} level(s) in {skill.Definition.Name} (now level {skill.Level})");
        }

        return levelsGained;
    }

    /// <summary>
    /// Perform a skill check against a difficulty value.
    /// If effective level >= difficulty, auto-pass. If effective level is less than half
    /// the difficulty, auto-fail. Otherwise, perform a random roll.
    /// </summary>
    /// <param name="skillId">The skill definition ID.</param>
    /// <param name="difficulty">The difficulty threshold.</param>
    /// <returns>True if the check succeeds, false otherwise.</returns>
    public bool SkillCheck(string skillId, float difficulty)
    {
        var skill = GetSkill(skillId);
        if (skill == null)
        {
            // Entity doesn't have the skill at all - use effective level 0
            return difficulty <= 0;
        }

        float effectiveLevel = skill.GetEffectiveLevel(_owner.Attributes);

        // Auto-pass if effective level meets or exceeds difficulty
        if (effectiveLevel >= difficulty)
        {
            return true;
        }

        // Auto-fail if effective level is less than half the difficulty
        if (effectiveLevel < difficulty * 0.5f)
        {
            return false;
        }

        // Random roll with success chance proportional to effective level vs difficulty
        float successChance = effectiveLevel / difficulty;
        return Random.Shared.NextSingle() < successChance;
    }

    /// <summary>
    /// Calculate the attribute-based XP multiplier for a skill.
    /// Each attribute point above 10 adds bonus scaled by the attribute's weight.
    /// Minimum multiplier is 0.5 (50% rate).
    /// </summary>
    /// <param name="definition">The skill definition with attribute influences.</param>
    /// <returns>The XP multiplier (minimum 0.5).</returns>
    private float CalculateAttributeMultiplier(SkillDefinition definition)
    {
        float sum = 0f;

        foreach (var (attrName, weight) in definition.AttributeInfluences)
        {
            float attrValue = GetAttributeByName(attrName);
            sum += ((attrValue - 10f) / 10f) * weight;
        }

        return MathF.Max(0.5f, 1.0f + sum);
    }

    /// <summary>
    /// Look up an attribute value by name from the owner's attributes.
    /// </summary>
    /// <param name="name">The attribute name (case-insensitive).</param>
    /// <returns>The attribute value, or 10 if the name is unrecognized.</returns>
    private float GetAttributeByName(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "strength" => _owner.Attributes.strength,
            "dexterity" => _owner.Attributes.dexterity,
            "constitution" => _owner.Attributes.constitution,
            "intelligence" => _owner.Attributes.intelligence,
            "willpower" => _owner.Attributes.willpower,
            "wisdom" => _owner.Attributes.wisdom,
            "charisma" => _owner.Attributes.charisma,
            _ => 10f
        };
    }
}
