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
    /// Gets or sets the atlas source ID (e.g., "dcss", "kenney").
    /// References an atlas source registered in TileResourceManager.
    /// </summary>
    public string? AtlasSource { get; set; }

    /// <summary>
    /// Gets or sets sprite size in atlas tiles as [width, height].
    /// Defaults to [1, 1] (single tile). For multi-tile sprites, e.g., [2, 3] for a 2-wide, 3-tall sprite.
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
    /// Gets the effective width in tiles (defaults to 1).
    /// </summary>
    public int WidthInTiles => SpriteSize is { Length: >= 1 } ? SpriteSize[0] : 1;

    /// <summary>
    /// Gets the effective height in tiles (defaults to 1).
    /// </summary>
    public int HeightInTiles => SpriteSize is { Length: >= 2 } ? SpriteSize[1] : 1;

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

        if (string.IsNullOrEmpty(AtlasSource))
        {
            Log.Error($"SpriteDefinition '{Id}': Missing required field 'AtlasSource'");
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
                AtlasSource = null
            }

        ];
    }

    /// <summary>
    /// Creates AtlasTexture resources for all effective layers using the global cache.
    /// Returns a list of (layerName, atlasTexture) tuples ordered by layer definition order.
    /// </summary>
    /// <returns>A list of named atlas textures, one per layer.</returns>
    public List<(string Name, AtlasTexture Texture)> CreateAtlasTextures()
    {
        var result = new List<(string Name, AtlasTexture Texture)>();
        string defaultAtlasSource = AtlasSource ?? string.Empty;
        int defaultW = WidthInTiles;
        int defaultH = HeightInTiles;

        foreach (var layer in GetEffectiveLayers())
        {
            string atlasSourceId = layer.AtlasSource ?? defaultAtlasSource;
            int w = layer.WidthInTiles ?? defaultW;
            int h = layer.HeightInTiles ?? defaultH;
            var atlasTexture = TileResourceManager.Instance.GetCachedAtlasTexture(
                atlasSourceId, layer.Row, layer.Col, w, h);

            if (atlasTexture != null)
            {
                result.Add((layer.Name, atlasTexture));
            }
            else
            {
                Log.Error($"SpriteDefinition '{Id}': Failed to get atlas texture for layer '{layer.Name}' (source: {atlasSourceId}, row: {layer.Row}, col: {layer.Col})");
            }
        }

        return result;
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
    /// Gets or sets an optional per-layer atlas source ID override.
    /// When null, the parent <see cref="SpriteDefinition.AtlasSource"/> is used.
    /// </summary>
    public string? AtlasSource { get; set; }

    /// <summary>
    /// Gets or sets optional per-layer width in tiles override.
    /// When null, the parent <see cref="SpriteDefinition.WidthInTiles"/> is used.
    /// </summary>
    public int? WidthInTiles { get; set; }

    /// <summary>
    /// Gets or sets optional per-layer height in tiles override.
    /// When null, the parent <see cref="SpriteDefinition.HeightInTiles"/> is used.
    /// </summary>
    public int? HeightInTiles { get; set; }
}
