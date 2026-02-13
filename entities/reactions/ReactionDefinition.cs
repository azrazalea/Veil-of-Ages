using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Reactions;

/// <summary>
/// JSON-serializable reaction template that defines a transformation of items.
/// Reactions convert input items into output items over a duration.
/// </summary>
public class ReactionDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this reaction type.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets display name for this reaction.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets descriptive text explaining what this reaction does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets items consumed by this reaction.
    /// </summary>
    public List<ItemQuantity> Inputs { get; set; } = [];

    /// <summary>
    /// Gets or sets items produced by this reaction.
    /// </summary>
    public List<ItemQuantity> Outputs { get; set; } = [];

    /// <summary>
    /// Gets or sets number of game ticks required to complete this reaction.
    /// </summary>
    public uint Duration { get; set; }

    /// <summary>
    /// Gets or sets facilities/equipment required to perform this reaction.
    /// Examples: "millstone", "oven", "forge", "mortar_and_pestle"
    /// Empty list means no special equipment needed.
    /// </summary>
    public List<string> RequiredFacilities { get; set; } = [];

    /// <summary>
    /// Gets or sets tags for categorization and job matching (e.g., "milling", "baking", "smithing").
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the energy cost multiplier for this reaction.
    /// Applied against the base energy cost per tick in ProcessReactionActivity.
    /// Default: 1.0 (normal energy cost)
    /// Examples: 0.5 = light work, 1.5 = heavy labor, 2.0 = very strenuous.
    /// </summary>
    public float EnergyCostMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the hunger decay multiplier for this reaction.
    /// Applied against the base hunger decay rate while processing.
    /// Default: 1.0 (normal hunger rate)
    /// Examples: 0.75 = light work, 1.2 = moderate work, 1.5 = heavy labor.
    /// </summary>
    public float HungerMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Load a reaction definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>ReactionDefinition instance or null on error.</returns>
    public static ReactionDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ReactionDefinition>(jsonContent, Default);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading reaction definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate this reaction definition has required fields.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("ReactionDefinition validation failed: Id is required");
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            Log.Error($"ReactionDefinition validation failed: Name is required for '{Id}'");
            return false;
        }

        if (Inputs.Count == 0)
        {
            Log.Error($"ReactionDefinition validation failed: At least one input is required for '{Id}'");
            return false;
        }

        if (Outputs.Count == 0)
        {
            Log.Error($"ReactionDefinition validation failed: At least one output is required for '{Id}'");
            return false;
        }

        // Validate input quantities
        foreach (var input in Inputs)
        {
            if (string.IsNullOrEmpty(input.ItemId))
            {
                Log.Error($"ReactionDefinition validation failed: Input ItemId cannot be empty for '{Id}'");
                return false;
            }

            if (input.Quantity < 1)
            {
                Log.Error($"ReactionDefinition validation failed: Input quantity must be at least 1 for '{Id}'");
                return false;
            }
        }

        // Validate output quantities
        foreach (var output in Outputs)
        {
            if (string.IsNullOrEmpty(output.ItemId))
            {
                Log.Error($"ReactionDefinition validation failed: Output ItemId cannot be empty for '{Id}'");
                return false;
            }

            if (output.Quantity < 1)
            {
                Log.Error($"ReactionDefinition validation failed: Output quantity must be at least 1 for '{Id}'");
                return false;
            }
        }

        if (Duration == 0)
        {
            Log.Error($"ReactionDefinition validation failed: Duration must be greater than 0 for '{Id}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if this reaction has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the reaction has the tag, false otherwise.</returns>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if this reaction can be performed with the available facilities.
    /// </summary>
    /// <param name="availableFacilities">Set of facility IDs available at the location.</param>
    /// <returns>True if all required facilities are available.</returns>
    public bool CanPerformWith(IEnumerable<string>? availableFacilities)
    {
        // If no facilities are required, the reaction can be performed anywhere
        if (RequiredFacilities.Count == 0)
        {
            return true;
        }

        // If facilities are required but none are provided, cannot perform
        if (availableFacilities == null)
        {
            return false;
        }

        // Check if all required facilities are available
        var available = new HashSet<string>(availableFacilities, StringComparer.OrdinalIgnoreCase);
        foreach (var required in RequiredFacilities)
        {
            if (!available.Contains(required))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check if this reaction requires a specific facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to check for.</param>
    /// <returns>True if the reaction requires this facility.</returns>
    public bool RequiresFacility(string facilityId)
    {
        return RequiredFacilities.Contains(facilityId, StringComparer.OrdinalIgnoreCase);
    }
}
