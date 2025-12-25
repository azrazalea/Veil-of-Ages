using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// Represents a category of tile variants (e.g., "Tombstone", "Statue", etc.)
/// </summary>
public class TileCategory
{
    // Variants within this category, organized by material -> variant name -> definition
    public Dictionary<string, Dictionary<string, TileVariantDefinition>> Variants { get; set; } = new ();
}

/// <summary>
/// Represents a variant definition for a specific material and variant combination.
/// </summary>
public class TileVariantDefinition
{
    // Atlas source name reference for this variant
    public string? AtlasSource { get; set; }

    // Atlas coordinates within the source
    public Vector2I AtlasCoords { get; set; }

    // Additional properties specific to this variant
    public Dictionary<string, string> Properties { get; set; } = new ();
}

/// <summary>
/// Represents a tile definition that can be loaded from JSON.
/// </summary>
public class TileDefinition
{
    // Unique identifier for the tile
    public string? Id { get; set; }

    // Display name for the tile
    public string? Name { get; set; }

    // Description of the tile
    public string? Description { get; set; }

    // Default tile type
    public string? Type { get; set; }

    // Category for decoration subtypes (e.g., "Tombstone", "Statue", etc.)
    public string? Category { get; set; }

    // Default material ID
    public string? DefaultMaterial { get; set; }

    // Is this tile walkable by default?
    public bool IsWalkable { get; set; }

    // Base durability of the tile
    public int BaseDurability { get; set; } = 100;

    // Atlas source name reference (legacy/fallback)
    public string? AtlasSource { get; set; }

    // Atlas coordinates within the source (legacy/fallback)
    public Vector2I AtlasCoords { get; set; }

    // Default sensory detection difficulties
    public Dictionary<string, float> DefaultSensoryDifficulties { get; set; } = new ();

    // Additional properties specific to this tile type
    public Dictionary<string, string> Properties { get; set; } = new ();

    // Categories of variants (e.g., "Default", "Tombstone", "Statue")
    public Dictionary<string, TileCategory> Categories { get; set; } = new ();

    /// <summary>
    /// Load a tile definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>TileDefinition instance.</returns>
    public static TileDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            var definition = new TileDefinition();

            // Load basic properties
            if (root.TryGetProperty("Id", out var idElement))
            {
                definition.Id = idElement.GetString();
            }

            if (root.TryGetProperty("Name", out var nameElement))
            {
                definition.Name = nameElement.GetString();
            }

            if (root.TryGetProperty("Description", out var descElement))
            {
                definition.Description = descElement.GetString();
            }

            if (root.TryGetProperty("Type", out var typeElement))
            {
                definition.Type = typeElement.GetString();
            }

            if (root.TryGetProperty("Category", out var categoryElement))
            {
                definition.Category = categoryElement.GetString();
            }

            if (root.TryGetProperty("DefaultMaterial", out var materialElement))
            {
                definition.DefaultMaterial = materialElement.GetString();
            }

            if (root.TryGetProperty("IsWalkable", out var walkableElement))
            {
                definition.IsWalkable = walkableElement.GetBoolean();
            }

            if (root.TryGetProperty("BaseDurability", out var durabilityElement))
            {
                definition.BaseDurability = durabilityElement.GetInt32();
            }

            if (root.TryGetProperty("AtlasSource", out var atlasElement))
            {
                definition.AtlasSource = atlasElement.GetString();
            }

            // Handle AtlasCoords
            if (root.TryGetProperty("AtlasCoords", out var coordsElement))
            {
                var coords = JsonSerializer.Deserialize<Vector2I>(
                    coordsElement.GetRawText(),
                    new JsonSerializerOptions { Converters = { new Vector2IConverter() } });
                definition.AtlasCoords = coords;
            }

            // Handle DefaultSensoryDifficulties
            if (root.TryGetProperty("DefaultSensoryDifficulties", out var sensoryElement))
            {
                definition.DefaultSensoryDifficulties = JsonSerializer.Deserialize<Dictionary<string, float>>(
                    sensoryElement.GetRawText()) ?? new Dictionary<string, float>();
            }

            // Handle Properties
            if (root.TryGetProperty("Properties", out var propsElement))
            {
                definition.Properties = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    propsElement.GetRawText()) ?? new Dictionary<string, string>();
            }

            // Handle Categories/Variants conversion
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new Vector2IConverter() }
            };

            if (root.TryGetProperty("Categories", out var categoriesElement))
            {
                // New format - directly deserialize Categories
                definition.Categories = JsonSerializer.Deserialize<Dictionary<string, TileCategory>>(
                    categoriesElement.GetRawText(), jsonOptions) ?? new Dictionary<string, TileCategory>();
            }
            else if (root.TryGetProperty("Variants", out var variantsElement))
            {
                // Old format - convert Variants to Default category
                var variants = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, TileVariantDefinition>>>(
                    variantsElement.GetRawText(), jsonOptions) ?? new Dictionary<string, Dictionary<string, TileVariantDefinition>>();

                string categoryName = !string.IsNullOrEmpty(definition.Category) ? definition.Category : "Default";
                definition.Categories[categoryName] = new TileCategory { Variants = variants };
            }

            return definition;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading tile definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Merge a variant definition with this base definition.
    /// </summary>
    /// <param name="variantDef">The variant definition to merge.</param>
    /// <returns>A new TileDefinition with merged properties.</returns>
    public TileDefinition MergeWithVariant(TileDefinition variantDef)
    {
        var merged = new TileDefinition
        {
            // Base properties (preserved from base)
            Id = Id,
            Name = Name,
            Description = Description,
            Type = Type,
            DefaultMaterial = DefaultMaterial,
            IsWalkable = IsWalkable,

            // Override with variant values if they exist
            Category = variantDef.Category ?? Category,
            BaseDurability = variantDef.BaseDurability != 0 ? variantDef.BaseDurability : BaseDurability,
            AtlasSource = !string.IsNullOrEmpty(variantDef.AtlasSource) ? variantDef.AtlasSource : AtlasSource,
            AtlasCoords = variantDef.AtlasCoords != Vector2I.Zero ? variantDef.AtlasCoords : AtlasCoords,

            // Merge dictionaries
            DefaultSensoryDifficulties = new Dictionary<string, float>(DefaultSensoryDifficulties),
            Properties = new Dictionary<string, string>(Properties),
            Categories = new Dictionary<string, TileCategory>()
        };

        // Override/add variant-specific sensory difficulties
        if (variantDef.DefaultSensoryDifficulties != null)
        {
            foreach (var kvp in variantDef.DefaultSensoryDifficulties)
            {
                merged.DefaultSensoryDifficulties[kvp.Key] = kvp.Value;
            }
        }

        // Override/add variant-specific properties
        if (variantDef.Properties != null)
        {
            foreach (var kvp in variantDef.Properties)
            {
                merged.Properties[kvp.Key] = kvp.Value;
            }
        }

        // Copy base categories
        if (Categories != null)
        {
            foreach (var categoryKvp in Categories)
            {
                merged.Categories[categoryKvp.Key] = new TileCategory
                {
                    Variants = new Dictionary<string, Dictionary<string, TileVariantDefinition>>()
                };

                // Copy variants within this category
                foreach (var materialKvp in categoryKvp.Value.Variants)
                {
                    merged.Categories[categoryKvp.Key].Variants[materialKvp.Key] =
                        new Dictionary<string, TileVariantDefinition>(materialKvp.Value);
                }
            }
        }

        // Add/merge variant-specific categories
        if (variantDef.Categories != null)
        {
            foreach (var categoryKvp in variantDef.Categories)
            {
                string categoryName = categoryKvp.Key;

                if (!merged.Categories.ContainsKey(categoryName))
                {
                    merged.Categories[categoryName] = new TileCategory
                    {
                        Variants = new Dictionary<string, Dictionary<string, TileVariantDefinition>>()
                    };
                }

                // Merge variants within this category
                foreach (var materialKvp in categoryKvp.Value.Variants)
                {
                    if (!merged.Categories[categoryName].Variants.ContainsKey(materialKvp.Key))
                    {
                        merged.Categories[categoryName].Variants[materialKvp.Key] =
                            new Dictionary<string, TileVariantDefinition>();
                    }

                    foreach (var variantKvp in materialKvp.Value)
                    {
                        merged.Categories[categoryName].Variants[materialKvp.Key][variantKvp.Key] = variantKvp.Value;
                    }
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Get the default detection difficulty for a specific sense type.
    /// </summary>
    /// <param name="senseType">The sense type to get the difficulty for.</param>
    /// <returns>The difficulty value (default 0.0 if not specified).</returns>
    public float GetDefaultSensoryDifficulty(SenseType senseType)
    {
        string senseTypeName = senseType.ToString();
        if (DefaultSensoryDifficulties.TryGetValue(senseTypeName, out float difficulty))
        {
            return difficulty;
        }

        return 0.0f; // Default difficulty (no difficulty)
    }

    /// <summary>
    /// Validate this tile definition.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        // Basic validation
        if (string.IsNullOrEmpty(Id))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Type))
        {
            return false;
        }

        return true;
    }
}
