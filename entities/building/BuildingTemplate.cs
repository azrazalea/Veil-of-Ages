using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Grid;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities;

/// <summary>
/// Represents a template for a building that can be loaded from JSON.
/// </summary>
public class BuildingTemplate
{
    // Basic building information
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Vector2I Size { get; set; }

    // Culture or style information
    public string? Culture { get; set; }
    public string? Style { get; set; }

    // Building tile data
    public List<BuildingTileData> Tiles { get; set; } = new ();

    // Optional room definitions
    public List<RoomData> Rooms { get; set; } = new ();

    // Entrance position(s)
    public List<Vector2I> EntrancePositions { get; set; } = new ();

    // Metadata
    public string? BuildingType { get; set; }

    // TileMap node path - used to find the appropriate tilemap in the scene tree
    public string TileMapNodePath { get; set; } = string.Empty;

    // TileMap resource path - will be used to dynamically load the appropriate tilemap
    public string TileMapPath { get; set; } = "res://resources/tilesets/buildings_tileset.tres";
    public int Capacity { get; set; }
    public StorageConfig? Storage { get; set; }
    public List<FacilityData> Facilities { get; set; } = new ();
    public Dictionary<string, string> Metadata { get; set; } = new ();

    /// <summary>
    /// Load a building template from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>BuildingTemplate instance.</returns>
    public static BuildingTemplate? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BuildingTemplate>(jsonContent, WithVector2I);
        }
        catch (Exception e)
        {
            Log.Error($"Error loading building template: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save this building template to a JSON file.
    /// </summary>
    /// <param name="path">Path to save the JSON file.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool SaveToJson(string path)
    {
        try
        {
            string jsonContent = JsonSerializer.Serialize(this, WriteIndented);
            File.WriteAllText(path, jsonContent);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Error saving building template: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validate this template to ensure it has all required data.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        // Basic validation
        if (string.IsNullOrEmpty(Name))
        {
            Log.Error($"Building validation failed: Name is null or empty");
            return false;
        }

        if (Size.X <= 0 || Size.Y <= 0)
        {
            Log.Error($"Building validation failed: Invalid size dimensions ({Size.X}, {Size.Y}). Both dimensions must be positive");
            return false;
        }

        if (Tiles.Count == 0)
        {
            Log.Error($"Building validation failed: Building '{Name}' has no tiles defined");
            return false;
        }

        // Validate that all tiles are within the building size
        foreach (var tile in Tiles)
        {
            if (tile.Position.X < 0 || tile.Position.X >= Size.X ||
                tile.Position.Y < 0 || tile.Position.Y >= Size.Y)
            {
                Log.Error($"Building validation failed: Tile at position ({tile.Position.X}, {tile.Position.Y}) is outside the building boundaries (0-{Size.X - 1}, 0-{Size.Y - 1})");
                return false;
            }
        }

        // Validate that all entrance positions are valid
        foreach (var entrance in EntrancePositions)
        {
            if (entrance.X < 0 || entrance.X >= Size.X ||
                entrance.Y < 0 || entrance.Y >= Size.Y)
            {
                Log.Error($"Building validation failed: Entrance position ({entrance.X}, {entrance.Y}) is outside the building boundaries (0-{Size.X - 1}, 0-{Size.Y - 1})");
                return false;
            }

            // Verify that the entrance position has a door or is walkable
            string badEntrances = string.Empty;
            TileResourceManager.Instance.Initialize();
            foreach (var tile in Tiles)
            {
                if (tile.Type == null)
                {
                    Log.Error($"Tile at {tile.Position} does not have a Type");
                    return false;
                }

                var tileDef = TileResourceManager.Instance.GetTileDefinition(tile.Type.ToLowerInvariant());
                if (tileDef == null)
                {
                    Log.Error($"Tile at {tile.Position} of type {tile.Type} has no corresponding tile definition");
                    return false;
                }

                if (tile.Position == entrance &&
                    tileDef.Name != "Door" && tileDef.Name != "Gate" && !tileDef.IsWalkable)
                {
                    badEntrances += $"{tile.Type},";
                }
            }

            if (badEntrances.Length > 0)
            {
                Log.Error($"Building validation failed: Entrances {badEntrances} at position ({entrance.X}, {entrance.Y}) are not a door or walkable tile");
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Data structure for a tile in a building template.
/// </summary>
public class BuildingTileData
{
    public Vector2I Position { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? Material { get; set; }
    public string? Variant { get; set; }
    public bool IsWalkable { get; set; }
    public int Durability { get; set; } = 100;
    public Vector2I AtlasCoords { get; set; }
    public int SourceId { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new ();
}

/// <summary>
/// Data structure for a room in a building template.
/// </summary>
public class RoomData
{
    public string? Name { get; set; }
    public string? Purpose { get; set; }
    public Vector2I TopLeft { get; set; }
    public Vector2I Size { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new ();
}

/// <summary>
/// Data structure for a facility location in a building template.
/// </summary>
public class FacilityData
{
    public string Id { get; set; } = string.Empty;
    public Vector2I Position { get; set; }
}

/// <summary>
/// Configuration for building storage capability.
/// </summary>
public class StorageConfig
{
    /// <summary>
    /// Gets or sets maximum volume capacity in cubic meters.
    /// </summary>
    public float VolumeCapacity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets maximum weight capacity in kilograms. -1 means unlimited.
    /// </summary>
    public float WeightCapacity { get; set; } = -1;

    /// <summary>
    /// Gets or sets modifier for decay rate of stored items. Lower = slower decay.
    /// </summary>
    public float DecayRateModifier { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets list of facility types available (e.g., "hearth", "cold_storage").
    /// </summary>
    public List<string> Facilities { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether entities must be adjacent to the storage facility position
    /// (defined in the building's Facilities array with Id "storage") to access storage.
    /// If false (default), entities can access storage from anywhere adjacent to the building.
    /// </summary>
    public bool RequireAdjacentToFacility { get; set; }
}

/// <summary>
/// JSON converter for Vector2I.
/// </summary>
public class Vector2IConverter : JsonConverter<Vector2I>
{
    public override Vector2I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            int x = reader.GetInt32();
            reader.Read();
            int y = reader.GetInt32();
            reader.Read(); // Read EndArray
            return new Vector2I(x, y);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            int x = 0, y = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? propertyName = reader.GetString();
                    reader.Read();
                    if (propertyName is "x" or "X")
                    {
                        x = reader.GetInt32();
                    }
                    else if (propertyName is "y" or "Y")
                    {
                        y = reader.GetInt32();
                    }
                }
            }

            return new Vector2I(x, y);
        }

        throw new JsonException("Expected Vector2I as array or object");
    }

    public override void Write(Utf8JsonWriter writer, Vector2I value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
