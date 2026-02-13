using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Skills;

/// <summary>
/// Autoload singleton manager for loading skill definitions from JSON.
/// Register as an autoload in project.godot.
/// </summary>
public partial class SkillResourceManager : ResourceManager<SkillResourceManager, SkillDefinition>
{
    protected override string ResourcePath => "res://resources/skills";

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
}
