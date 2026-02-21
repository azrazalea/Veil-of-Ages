using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Skills;

/// <summary>
/// Categories for organizing skills by their primary domain.
/// </summary>
public enum SkillCategory
{
    General,
    Combat,
    Crafting,
    Magic,
    Social
}

/// <summary>
/// JSON-serializable skill template that defines the properties of a skill type.
/// </summary>
public class SkillDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this skill type.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets display name for this skill.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets descriptive text explaining what this skill is.
    /// </summary>
    public string? Description { get; set; }

    public string LocalizedName => L.Tr($"skill.name.{Id!.ToUpperInvariant()}");
    public string LocalizedDescription => L.Tr($"skill.desc.{Id!.ToUpperInvariant()}");

    /// <summary>
    /// Gets or sets primary category of this skill.
    /// </summary>
    public SkillCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the maximum achievable level for this skill.
    /// </summary>
    public int MaxLevel { get; set; } = 100;

    /// <summary>
    /// Gets or sets the base XP required per level before scaling.
    /// </summary>
    public float BaseXpPerLevel { get; set; } = 100;

    /// <summary>
    /// Gets or sets the exponential scaling factor applied per level.
    /// </summary>
    public float XpScaling { get; set; } = 1.15f;

    /// <summary>
    /// Gets or sets the attribute influences that affect this skill.
    /// Keys are attribute names (e.g., "strength", "intelligence"), values are weights.
    /// </summary>
    public Dictionary<string, float> AttributeInfluences { get; set; } = new ();

    /// <summary>
    /// Gets or sets tags for categorization and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Calculate the XP required to advance from the given level to the next level.
    /// Level 1 requires BaseXpPerLevel. Each subsequent level scales exponentially.
    /// </summary>
    /// <param name="level">The current level (0-based minimum).</param>
    /// <returns>XP needed to go from level to level+1.</returns>
    public float GetXpForLevel(int level)
    {
        return BaseXpPerLevel * MathF.Pow(XpScaling, level - 1);
    }

    /// <summary>
    /// Check if this skill has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the skill has the tag, false otherwise.</returns>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate this skill definition has required fields and valid constraints.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("SkillDefinition validation failed: Id is required");
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            Log.Error($"SkillDefinition validation failed: Name is required for '{Id}'");
            return false;
        }

        if (MaxLevel < 1)
        {
            Log.Error($"SkillDefinition validation failed: MaxLevel must be at least 1 for '{Id}'");
            return false;
        }

        if (BaseXpPerLevel <= 0)
        {
            Log.Error($"SkillDefinition validation failed: BaseXpPerLevel must be greater than 0 for '{Id}'");
            return false;
        }

        if (XpScaling <= 0)
        {
            Log.Error($"SkillDefinition validation failed: XpScaling must be greater than 0 for '{Id}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Load a skill definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>SkillDefinition instance or null on error.</returns>
    public static SkillDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SkillDefinition>(jsonContent, Default);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading skill definition: {e.Message}");
            return null;
        }
    }
}
