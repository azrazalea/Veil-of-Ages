using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Items;

/// <summary>
/// Categories for organizing items by their primary use.
/// </summary>
public enum ItemCategory
{
    RawMaterial,
    ProcessedMaterial,
    Food,
    Tool,
    Remains
}

/// <summary>
/// JSON-serializable item template that defines the properties of an item type.
/// </summary>
public class ItemDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this item type.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets display name for this item.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets descriptive text explaining what this item is.
    /// </summary>
    public string? Description { get; set; }

    public string LocalizedName => L.Tr($"item.name.{Id!.ToUpperInvariant()}");
    public string LocalizedDescription => L.Tr($"item.desc.{Id!.ToUpperInvariant()}");

    /// <summary>
    /// Gets or sets primary category of this item.
    /// </summary>
    public ItemCategory Category { get; set; }

    /// <summary>
    /// Gets or sets volume per unit in cubic meters.
    /// </summary>
    public float VolumeM3 { get; set; }

    /// <summary>
    /// Gets or sets weight per unit in kilograms.
    /// </summary>
    public float WeightKg { get; set; }

    /// <summary>
    /// Gets or sets base decay rate per game tick. 0 = no decay.
    /// </summary>
    public float BaseDecayRatePerTick { get; set; }

    /// <summary>
    /// Gets or sets nutrition value if this item is edible. 0 = not edible.
    /// </summary>
    public float EdibleNutrition { get; set; }

    /// <summary>
    /// Gets or sets maximum number of items that can be stacked together.
    /// </summary>
    public int StackLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets tags for categorization and filtering (e.g., "organic", "metal", "weapon").
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Load an item definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>ItemDefinition instance or null on error.</returns>
    public static ItemDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ItemDefinition>(jsonContent, Default);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading item definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate this item definition has required fields.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("ItemDefinition validation failed: Id is required");
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            Log.Error($"ItemDefinition validation failed: Name is required for '{Id}'");
            return false;
        }

        if (VolumeM3 < 0)
        {
            Log.Error($"ItemDefinition validation failed: VolumeM3 cannot be negative for '{Id}'");
            return false;
        }

        if (WeightKg < 0)
        {
            Log.Error($"ItemDefinition validation failed: WeightKg cannot be negative for '{Id}'");
            return false;
        }

        if (BaseDecayRatePerTick < 0)
        {
            Log.Error($"ItemDefinition validation failed: BaseDecayRatePerTick cannot be negative for '{Id}'");
            return false;
        }

        if (EdibleNutrition < 0)
        {
            Log.Error($"ItemDefinition validation failed: EdibleNutrition cannot be negative for '{Id}'");
            return false;
        }

        if (StackLimit < 1)
        {
            Log.Error($"ItemDefinition validation failed: StackLimit must be at least 1 for '{Id}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if this item has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the item has the tag, false otherwise.</returns>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }
}
