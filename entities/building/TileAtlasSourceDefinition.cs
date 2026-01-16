using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities;

/// <summary>
/// Represents a tile atlas source definition that can be loaded from JSON.
/// </summary>
public class TileAtlasSourceDefinition
{
    // Unique identifier for the atlas source
    public required string Id { get; set; }

    // Display name for the atlas source
    public required string Name { get; set; }

    // Description of the atlas source
    public string Description { get; set; } = string.Empty;

    // Path to the texture file
    public required string TexturePath { get; set; }

    // Tile size in pixels
    public Vector2I TileSize { get; set; } = new Vector2I(16, 16);

    // Separation between tiles in pixels
    public Vector2I Separation { get; set; } = new Vector2I(0, 0);

    // Margin in pixels
    public Vector2I Margin { get; set; } = new Vector2I(0, 0);

    // Additional properties specific to this atlas source
    public Dictionary<string, string> Properties { get; set; } = new ();

    /// <summary>
    /// Load a tile atlas source definition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>TileAtlasSourceDefinition instance or null on error.</returns>
    public static TileAtlasSourceDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TileAtlasSourceDefinition>(jsonContent, WithGodotTypes);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading tile atlas source definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate this atlas source definition.
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

        if (string.IsNullOrEmpty(TexturePath))
        {
            return false;
        }

        if (TileSize.X <= 0 || TileSize.Y <= 0)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// JSON converter for Rect2I.
/// </summary>
public class Rect2IJsonConverter : JsonConverter<Rect2I>
{
    public override Rect2I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int x = 0, y = 0, width = 0, height = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName?.ToLowerInvariant())
                    {
                        case "x":
                            x = reader.GetInt32();
                            break;
                        case "y":
                            y = reader.GetInt32();
                            break;
                        case "width":
                            width = reader.GetInt32();
                            break;
                        case "height":
                            height = reader.GetInt32();
                            break;
                    }
                }
            }

            return new Rect2I(x, y, width, height);
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            int x = reader.GetInt32();
            reader.Read();
            int y = reader.GetInt32();
            reader.Read();
            int width = reader.GetInt32();
            reader.Read();
            int height = reader.GetInt32();
            reader.Read(); // End array
            return new Rect2I(x, y, width, height);
        }

        throw new JsonException("Expected Rect2I as array or object");
    }

    public override void Write(Utf8JsonWriter writer, Rect2I value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Position.X);
        writer.WriteNumberValue(value.Position.Y);
        writer.WriteNumberValue(value.Size.X);
        writer.WriteNumberValue(value.Size.Y);
        writer.WriteEndArray();
    }
}
