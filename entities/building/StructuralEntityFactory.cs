using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// Factory for creating StructuralEntity instances from tile definition data.
/// Wraps TileResourceManager lookup to resolve definitions, materials, variants, and atlas coordinates.
/// </summary>
public static class StructuralEntityFactory
{
    /// <summary>
    /// Create a StructuralEntity from a building template tile data entry.
    /// </summary>
    /// <param name="tileData">The template tile data.</param>
    /// <param name="buildingOrigin">The absolute grid position of the building origin.</param>
    /// <returns>A fully configured StructuralEntity (not yet added to scene tree), or null on failure.</returns>
    public static StructuralEntity? Create(BuildingTileData tileData, Vector2I buildingOrigin)
    {
        if (tileData.Type == null || tileData.Material == null)
        {
            Log.Error("StructuralEntityFactory: Tile data missing Type or Material");
            return null;
        }

        // Use Category as definition ID when available, otherwise fall back to Type
        string tileDefId = !string.IsNullOrEmpty(tileData.Category)
            ? tileData.Category.ToLowerInvariant()
            : tileData.Type.ToLowerInvariant();

        string materialId = tileData.Material.ToLowerInvariant();
        string variantName = string.IsNullOrEmpty(tileData.Variant) ? "Default" : tileData.Variant;

        Vector2I absolutePos = buildingOrigin + tileData.Position;

        return CreateFromDefinition(tileDefId, materialId, variantName, absolutePos, tileData.Tint);
    }

    /// <summary>
    /// Create a StructuralEntity from raw definition parameters.
    /// </summary>
    /// <param name="tileDefId">The tile definition ID (e.g., "wall", "floor").</param>
    /// <param name="materialId">The material ID (e.g., "wood", "stone").</param>
    /// <param name="variantName">The variant name (e.g., "Default").</param>
    /// <param name="absoluteGridPosition">The absolute grid position for this tile.</param>
    /// <param name="tintOverride">Optional per-tile tint override (hex color string).</param>
    /// <returns>A fully configured StructuralEntity (not yet added to scene tree), or null on failure.</returns>
    public static StructuralEntity? CreateFromDefinition(
        string tileDefId,
        string materialId,
        string variantName,
        Vector2I absoluteGridPosition,
        string? tintOverride = null)
    {
        var tileDef = TileResourceManager.Instance.GetTileDefinition(tileDefId);
        if (tileDef == null)
        {
            Log.Error($"StructuralEntityFactory: Tile definition not found: {tileDefId}");
            return null;
        }

        // Fall back to definition's default material if none specified
        if (string.IsNullOrEmpty(materialId) && !string.IsNullOrEmpty(tileDef.DefaultMaterial))
        {
            materialId = tileDef.DefaultMaterial;
        }

        // Process variant to get atlas source, coords, and tint
        var processedVariant = TileResourceManager.GetProcessedVariant(tileDef, materialId, variantName);

        var atlasSource = (string?)processedVariant["AtlasSource"];
        var atlasCoords = (Vector2I?)processedVariant["AtlasCoords"];

        if (atlasSource == null || atlasCoords == null)
        {
            Log.Error($"StructuralEntityFactory: Could not resolve atlas for '{tileDefId}' material '{materialId}' variant '{variantName}'");
            return null;
        }

        int sourceId = TileResourceManager.Instance.GetTileSetSourceId(atlasSource);
        if (sourceId == -1)
        {
            Log.Error($"StructuralEntityFactory: Atlas source not found: {atlasSource}");
            return null;
        }

        // Parse tile type
        if (!Enum.TryParse<TileType>(tileDef.Type, out var tileType))
        {
            Log.Error($"StructuralEntityFactory: Invalid tile type: {tileDef.Type}");
            return null;
        }

        // Get material properties
        TileMaterialDefinition? material = null;
        if (!string.IsNullOrEmpty(materialId))
        {
            material = TileResourceManager.Instance.GetMaterial(materialId);
        }

        // Calculate durability
        int durability = tileDef.BaseDurability;
        if (material != null)
        {
            durability = (int)(durability * material.DurabilityModifier);
        }

        // Build detection difficulties
        var detectionDifficulties = new Dictionary<SenseType, float>();
        foreach (SenseType senseType in Enum.GetValues<SenseType>())
        {
            float baseDifficulty = tileDef.GetDefaultSensoryDifficulty(senseType);
            float materialModifier = material?.GetSensoryModifier(senseType) ?? 1.0f;
            detectionDifficulties[senseType] = baseDifficulty * materialModifier;
        }

        // Resolve tint cascade: per-tile override > variant tint > definition default tint
        Color? tintColor = ParseHexColor(tintOverride);
        if (!tintColor.HasValue)
        {
            var variantTint = (string?)processedVariant.GetValueOrDefault("Tint");
            tintColor = ParseHexColor(variantTint);
        }

        if (!tintColor.HasValue)
        {
            tintColor = ParseHexColor(tileDef.DefaultTint);
        }

        // Create the entity
        var entity = new StructuralEntity(
            tileType,
            materialId ?? "default",
            variantName,
            absoluteGridPosition,
            tileDef.IsWalkable,
            durability,
            (Vector2I)atlasCoords,
            sourceId,
            detectionDifficulties,
            tintColor);

        // Initialize visual using cached atlas texture
        var texture = TileResourceManager.Instance.GetCachedAtlasTexture(
            atlasSource, atlasCoords.Value.Y, atlasCoords.Value.X);
        entity.InitializeVisual(texture);

        return entity;
    }

    /// <summary>
    /// Parse a hex color string into a Godot Color.
    /// Returns null if the string is null, empty, or invalid.
    /// </summary>
    private static Color? ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return null;
        }

        try
        {
            return new Color(hex);
        }
        catch
        {
            Log.Warn($"StructuralEntityFactory: Failed to parse hex color '{hex}'");
            return null;
        }
    }
}
