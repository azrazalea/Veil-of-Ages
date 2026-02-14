using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// JSON-serializable class representing static sprite definitions for entities.
/// Replaces SpriteAnimationDefinition for entities that use single-frame sprites
/// rather than animated sprite sheets. Supports both single-layer and multi-layer
/// composite sprites (e.g., body + clothing + hair overlays).
/// </summary>
public class SpriteDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this sprite definition.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets human-readable name for this sprite definition.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets path to the texture atlas (res://assets/...).
    /// </summary>
    public string? TexturePath { get; set; }

    /// <summary>
    /// Gets or sets sprite size as [width, height] (e.g., [32, 32]).
    /// </summary>
#pragma warning disable SA1018 // Nullable type symbol should not be preceded by a space
    public int[] ? SpriteSize { get; set; }
#pragma warning restore SA1018

    /// <summary>
    /// Gets or sets the row in the atlas for single-layer shorthand.
    /// Used when <see cref="Layers"/> is not specified.
    /// </summary>
    public int? Row { get; set; }

    /// <summary>
    /// Gets or sets the column in the atlas for single-layer shorthand.
    /// Used when <see cref="Layers"/> is not specified.
    /// </summary>
    public int? Col { get; set; }

    /// <summary>
    /// Gets or sets the list of sprite layers for multi-layer composite sprites.
    /// When present, each layer defines a separate atlas region that is rendered
    /// on top of the previous layers.
    /// </summary>
    public List<SpriteLayerData>? Layers { get; set; }

    /// <summary>
    /// Load a SpriteDefinition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file (res:// or absolute path).</param>
    /// <returns>The loaded definition, or null if loading failed.</returns>
    public static SpriteDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpriteDefinition>(jsonContent, Default);
        }
        catch (System.Exception e)
        {
            Log.Error($"Error loading sprite definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate that all required fields are present and consistent.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("SpriteDefinition: Missing required field 'Id'");
            return false;
        }

        if (SpriteSize == null || SpriteSize.Length != 2)
        {
            Log.Error($"SpriteDefinition '{Id}': Invalid or missing 'SpriteSize' (expected [width, height])");
            return false;
        }

        bool hasLayers = Layers != null && Layers.Count > 0;
        bool hasTopLevel = Row.HasValue && Col.HasValue;

        if (!hasLayers && !hasTopLevel)
        {
            Log.Error($"SpriteDefinition '{Id}': Must have either 'Layers' or top-level 'Row'/'Col'");
            return false;
        }

        if (hasLayers)
        {
            foreach (var layer in Layers!)
            {
                if (string.IsNullOrEmpty(layer.Name))
                {
                    Log.Error($"SpriteDefinition '{Id}': Layer has empty Name");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the effective layer list. If <see cref="Layers"/> is defined and non-empty,
    /// returns it directly. Otherwise, creates a single layer named "body" from the
    /// top-level <see cref="Row"/> and <see cref="Col"/> values.
    /// </summary>
    /// <returns>A list of sprite layer data representing all layers for this definition.</returns>
    public List<SpriteLayerData> GetEffectiveLayers()
    {
        if (Layers != null && Layers.Count > 0)
        {
            return Layers;
        }

        return
        [
            new SpriteLayerData
            {
                Name = "body",
                Row = Row ?? 0,
                Col = Col ?? 0,
                TexturePath = null
            }

        ];
    }

    /// <summary>
    /// Creates AtlasTexture resources for all effective layers.
    /// Returns a list of (layerName, atlasTexture) tuples ordered by layer definition order.
    /// </summary>
    /// <returns>A list of named atlas textures, one per layer.</returns>
    public List<(string Name, AtlasTexture Texture)> CreateAtlasTextures()
    {
        var result = new List<(string Name, AtlasTexture Texture)>();
        int width = SpriteSize![0];
        int height = SpriteSize[1];
        string defaultTexturePath = TexturePath ?? string.Empty;

        foreach (var layer in GetEffectiveLayers())
        {
            string texturePath = layer.TexturePath ?? defaultTexturePath;
            var atlasTexture = CreateAtlasTexture(texturePath, layer.Row, layer.Col, width, height);

            if (atlasTexture != null)
            {
                result.Add((layer.Name, atlasTexture));
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a single AtlasTexture from atlas coordinates.
    /// </summary>
    /// <param name="texturePath">Path to the texture atlas resource.</param>
    /// <param name="row">Row index in the atlas (0-indexed).</param>
    /// <param name="col">Column index in the atlas (0-indexed).</param>
    /// <param name="width">Width of the sprite region in pixels.</param>
    /// <param name="height">Height of the sprite region in pixels.</param>
    /// <returns>The created AtlasTexture, or null if the texture could not be loaded.</returns>
    public static AtlasTexture? CreateAtlasTexture(string texturePath, int row, int col, int width, int height)
    {
        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture == null)
        {
            Log.Error($"SpriteDefinition: Failed to load texture: {texturePath}");
            return null;
        }

        return new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(col * width, row * height, width, height)
        };
    }
}

/// <summary>
/// Data for a single layer within a multi-layer sprite definition.
/// Each layer references a region in a texture atlas that is composited
/// with other layers to form the complete entity sprite.
/// </summary>
public class SpriteLayerData
{
    /// <summary>
    /// Gets or sets the layer name (e.g., "body", "hair", "clothing_outer").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row index in the texture atlas (0-indexed).
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Gets or sets the column index in the texture atlas (0-indexed).
    /// </summary>
    public int Col { get; set; }

    /// <summary>
    /// Gets or sets an optional per-layer texture path override.
    /// When null, the parent <see cref="SpriteDefinition.TexturePath"/> is used.
    /// </summary>
    public string? TexturePath { get; set; }
}
