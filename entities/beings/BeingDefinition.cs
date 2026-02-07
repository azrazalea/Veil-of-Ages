using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// JSON-serializable being template that defines the properties of an entity type.
/// </summary>
public class BeingDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this being type.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the parent definition ID for inheritance.
    /// Child definitions inherit from parent, with child values overriding.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an abstract definition that cannot be spawned directly.
    /// Abstract definitions serve as base templates for inheritance only.
    /// Null means "inherit from parent" (or false if no parent).
    /// </summary>
    public bool? Abstract { get; set; }

    /// <summary>
    /// Gets or sets display name for this being.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets descriptive text explaining what this being is.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets primary category of this being (e.g., "Human", "Undead").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets reference to SpriteAnimationDefinition for this being's visuals.
    /// </summary>
    public string? AnimationId { get; set; }

    /// <summary>
    /// Gets or sets attribute block defining base stats.
    /// </summary>
    public BeingAttributesDefinition? Attributes { get; set; }

    /// <summary>
    /// Gets or sets movement settings for this being.
    /// </summary>
    public BeingMovementDefinition? Movement { get; set; }

    /// <summary>
    /// Gets or sets the needs for this being (hunger, energy, etc.).
    /// Needs are initialized in NeedsSystem before traits are created.
    /// </summary>
    public List<NeedDefinition> Needs { get; set; } = [];

    /// <summary>
    /// Gets or sets starting skills for this being.
    /// Skills are initialized in SkillSystem before traits are created.
    /// </summary>
    public List<SkillStartDefinition> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets list of traits to add to this being.
    /// </summary>
    public List<TraitDefinition> Traits { get; set; } = [];

    /// <summary>
    /// Gets or sets body modification settings for this being.
    /// </summary>
    public BodyDefinition? Body { get; set; }

    /// <summary>
    /// Gets or sets sound configuration for this being.
    /// </summary>
    public AudioDefinition? Audio { get; set; }

    /// <summary>
    /// Gets or sets tags for categorization and filtering (e.g., "undead", "mindless").
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Load a being definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>BeingDefinition instance or null on error.</returns>
    public static BeingDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BeingDefinition>(jsonContent, Default);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading being definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate this being definition has required fields.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("BeingDefinition validation failed: Id is required");
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            Log.Error($"BeingDefinition validation failed: Name is required for '{Id}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if this being has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the being has the tag, false otherwise.</returns>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new definition by merging this definition with a parent.
    /// Uses generic DefinitionMerger for automatic field handling.
    /// </summary>
    /// <param name="parent">The parent definition to inherit from.</param>
    /// <returns>A new merged definition.</returns>
    public BeingDefinition MergeWithParent(BeingDefinition parent)
    {
        var merged = DefinitionMerger.Merge(parent, this);

        // Clear ParentId on merged result (don't propagate inheritance chain)
        merged.ParentId = null;

        // Deduplicate tags (DefinitionMerger does additive, but may have duplicates)
        merged.Tags = merged.Tags.Distinct().ToList();

        return merged;
    }
}

/// <summary>
/// JSON-serializable attribute block for being definitions.
/// Nullable floats allow distinguishing "not specified" from "specified as 0" for inheritance.
/// </summary>
public class BeingAttributesDefinition
{
    /// <summary>
    /// Gets or sets the Strength attribute value.
    /// </summary>
    public float? Strength { get; set; }

    /// <summary>
    /// Gets or sets the Dexterity attribute value.
    /// </summary>
    public float? Dexterity { get; set; }

    /// <summary>
    /// Gets or sets the Constitution attribute value.
    /// </summary>
    public float? Constitution { get; set; }

    /// <summary>
    /// Gets or sets the Intelligence attribute value.
    /// </summary>
    public float? Intelligence { get; set; }

    /// <summary>
    /// Gets or sets the Willpower attribute value.
    /// </summary>
    public float? Willpower { get; set; }

    /// <summary>
    /// Gets or sets the Wisdom attribute value.
    /// </summary>
    public float? Wisdom { get; set; }

    /// <summary>
    /// Gets or sets the Charisma attribute value.
    /// </summary>
    public float? Charisma { get; set; }

    /// <summary>
    /// Convert this definition to a BeingAttributes record.
    /// Uses default value of 10 for any unspecified attributes.
    /// </summary>
    /// <returns>A BeingAttributes record with the defined values.</returns>
    public BeingAttributes ToBeingAttributes()
    {
        return new BeingAttributes(
            Strength ?? 10f,
            Dexterity ?? 10f,
            Constitution ?? 10f,
            Intelligence ?? 10f,
            Willpower ?? 10f,
            Wisdom ?? 10f,
            Charisma ?? 10f);
    }
}

/// <summary>
/// JSON-serializable movement settings for being definitions.
/// </summary>
public class BeingMovementDefinition
{
    /// <summary>
    /// Gets or sets the base movement points per tick.
    /// </summary>
    public float BaseMovementPointsPerTick { get; set; }
}

/// <summary>
/// JSON-serializable trait definition for adding traits to beings.
/// </summary>
public class TraitDefinition
{
    /// <summary>
    /// Gets or sets the fully qualified class name of the trait (e.g., "VillagerTrait", "MindlessTrait").
    /// </summary>
    public string? TraitType { get; set; }

    /// <summary>
    /// Gets or sets the priority for trait ordering.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets static parameters from JSON to pass to the trait.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// JSON-serializable body configuration for being definitions.
/// </summary>
public class BodyDefinition
{
    /// <summary>
    /// Gets or sets the base body structure type (e.g., "Humanoid").
    /// </summary>
    public string? BaseStructure { get; set; }

    /// <summary>
    /// Gets or sets the list of body modifications to apply.
    /// </summary>
    public List<BodyModification> Modifications { get; set; } = [];
}

/// <summary>
/// JSON-serializable body modification for being definitions.
/// </summary>
public class BodyModification
{
    /// <summary>
    /// Gets or sets the modification type (e.g., "RemoveSoftTissues", "ScaleBoneHealth", "ApplyRandomDecay").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets parameters for the modification.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// JSON-serializable audio configuration for being definitions.
/// </summary>
public class AudioDefinition
{
    /// <summary>
    /// Gets or sets sound name to resource path mapping.
    /// </summary>
    public Dictionary<string, string> Sounds { get; set; } = [];
}

/// <summary>
/// JSON-serializable need definition for being definitions.
/// Defines a need that will be added to the being's NeedsSystem.
/// </summary>
public class NeedDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this need (e.g., "hunger", "energy").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets display name for this need (e.g., "Hunger", "Brain Hunger").
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets initial value for this need (0-100 scale, default 100).
    /// </summary>
    public float Initial { get; set; } = 100f;

    /// <summary>
    /// Gets or sets decay rate per tick (how fast need decreases).
    /// </summary>
    public float DecayRate { get; set; } = 0.01f;

    /// <summary>
    /// Gets or sets critical threshold below which urgent action is needed.
    /// </summary>
    public float Critical { get; set; } = 10f;

    /// <summary>
    /// Gets or sets low threshold below which the entity should address the need.
    /// </summary>
    public float Low { get; set; } = 30f;

    /// <summary>
    /// Gets or sets high threshold above which the need is well satisfied.
    /// </summary>
    public float High { get; set; } = 90f;
}

/// <summary>
/// JSON-serializable starting skill for being definitions.
/// Defines a skill the being starts with at a given level.
/// </summary>
public class SkillStartDefinition
{
    /// <summary>
    /// Gets or sets the skill definition ID (must match a loaded SkillDefinition).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the starting level for this skill (default 1).
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets or sets the starting XP within the current level (default 0).
    /// </summary>
    public float Xp { get; set; }
}
