using System;

namespace VeilOfAges.Entities.Skills;

/// <summary>
/// Runtime skill instance tracking an entity's progress in a specific skill.
/// </summary>
public class Skill
{
    /// <summary>
    /// Gets the skill definition this instance is based on.
    /// </summary>
    public SkillDefinition Definition { get; }

    /// <summary>
    /// Gets the current skill level.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// Gets the current XP accumulated toward the next level.
    /// </summary>
    public float CurrentXp { get; private set; }

    /// <summary>
    /// Gets the total XP required to advance from the current level to the next.
    /// </summary>
    public float XpToNextLevel => Definition.GetXpForLevel(Level);

    /// <summary>
    /// Gets the progress toward the next level as a value from 0 to 1.
    /// </summary>
    public float LevelProgress => IsMaxLevel ? 1f : CurrentXp / XpToNextLevel;

    /// <summary>
    /// Gets a value indicating whether gets whether this skill has reached its maximum level.
    /// </summary>
    public bool IsMaxLevel => Level >= Definition.MaxLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="Skill"/> class.
    /// Create a new skill instance from a definition.
    /// </summary>
    /// <param name="definition">The skill definition.</param>
    /// <param name="level">Starting level (clamped to [1, MaxLevel]).</param>
    /// <param name="currentXp">Starting XP toward next level.</param>
    public Skill(SkillDefinition definition, int level = 1, float currentXp = 0f)
    {
        Definition = definition;
        Level = Math.Clamp(level, 1, definition.MaxLevel);
        CurrentXp = currentXp;
    }

    /// <summary>
    /// Add XP to this skill, potentially gaining one or more levels.
    /// </summary>
    /// <param name="amount">The amount of XP to add.</param>
    /// <returns>The number of levels gained.</returns>
    public int AddXp(float amount)
    {
        if (IsMaxLevel)
        {
            return 0;
        }

        CurrentXp += amount;
        int levelsGained = 0;

        while (CurrentXp >= XpToNextLevel && !IsMaxLevel)
        {
            CurrentXp -= XpToNextLevel;
            Level++;
            levelsGained++;
        }

        // Cap XP if we hit max level
        if (IsMaxLevel)
        {
            CurrentXp = 0f;
        }

        return levelsGained;
    }

    /// <summary>
    /// Calculate the effective level of this skill, factoring in attribute bonuses.
    /// Each attribute point above 10 adds a bonus scaled by the attribute's weight.
    /// </summary>
    /// <param name="attributes">The entity's attributes.</param>
    /// <returns>The effective skill level as a float.</returns>
    public float GetEffectiveLevel(BeingAttributes attributes)
    {
        float effectiveLevel = Level;

        foreach (var (attrName, weight) in Definition.AttributeInfluences)
        {
            float attrValue = GetAttributeByName(attributes, attrName);
            effectiveLevel += (attrValue - 10f) * weight * 0.1f;
        }

        return effectiveLevel;
    }

    /// <summary>
    /// Look up an attribute value by name from the BeingAttributes record.
    /// </summary>
    /// <param name="attributes">The entity's attributes.</param>
    /// <param name="name">The attribute name (case-insensitive).</param>
    /// <returns>The attribute value, or 10 if the name is unrecognized.</returns>
    private static float GetAttributeByName(BeingAttributes attributes, string name)
    {
        return name.ToLowerInvariant() switch
        {
            "strength" => attributes.strength,
            "dexterity" => attributes.dexterity,
            "constitution" => attributes.constitution,
            "intelligence" => attributes.intelligence,
            "willpower" => attributes.willpower,
            "wisdom" => attributes.wisdom,
            "charisma" => attributes.charisma,
            _ => 10f
        };
    }
}
