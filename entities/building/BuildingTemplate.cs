using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using VeilOfAges.Grid;
using System.Linq;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Represents a template for a building that can be loaded from JSON
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
        public List<BuildingTileData> Tiles { get; set; } = new();

        // Optional room definitions
        public List<RoomData> Rooms { get; set; } = new();

        // Entrance position(s)
        public List<Vector2I> EntrancePositions { get; set; } = new();

        // Metadata
        public string? BuildingType { get; set; }

        // TileMap node path - used to find the appropriate tilemap in the scene tree
        public string TileMapNodePath { get; set; } = "";

        // TileMap resource path - will be used to dynamically load the appropriate tilemap
        public string TileMapPath { get; set; } = "res://resources/tilesets/buildings_tileset.tres";
        public int Capacity { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Load a building template from a JSON file
        /// </summary>
        /// <param name="path">Path to the JSON file</param>
        /// <returns>BuildingTemplate instance</returns>
        public static BuildingTemplate? LoadFromJson(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new Vector2IConverter() }
                };

                return JsonSerializer.Deserialize<BuildingTemplate>(jsonContent, options);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading building template: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save this building template to a JSON file
        /// </summary>
        /// <param name="path">Path to save the JSON file</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveToJson(string path)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new Vector2IConverter() }
                };

                string jsonContent = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, jsonContent);
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error saving building template: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate this template to ensure it has all required data
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate()
        {
            // Basic validation
            if (string.IsNullOrEmpty(Name))
            {
                GD.PrintErr($"Building validation failed: Name is null or empty");
                return false;
            }

            if (Size.X <= 0 || Size.Y <= 0)
            {
                GD.PrintErr($"Building validation failed: Invalid size dimensions ({Size.X}, {Size.Y}). Both dimensions must be positive");
                return false;
            }

            if (Tiles.Count == 0)
            {
                GD.PrintErr($"Building validation failed: Building '{Name}' has no tiles defined");
                return false;
            }

            // Validate that all tiles are within the building size
            foreach (var tile in Tiles)
            {
                if (tile.Position.X < 0 || tile.Position.X >= Size.X ||
                    tile.Position.Y < 0 || tile.Position.Y >= Size.Y)
                {
                    GD.PrintErr($"Building validation failed: Tile at position ({tile.Position.X}, {tile.Position.Y}) is outside the building boundaries (0-{Size.X - 1}, 0-{Size.Y - 1})");
                    return false;
                }
            }

            // Validate that all entrance positions are valid
            foreach (var entrance in EntrancePositions)
            {
                if (entrance.X < 0 || entrance.X >= Size.X ||
                    entrance.Y < 0 || entrance.Y >= Size.Y)
                {
                    GD.PrintErr($"Building validation failed: Entrance position ({entrance.X}, {entrance.Y}) is outside the building boundaries (0-{Size.X - 1}, 0-{Size.Y - 1})");
                    return false;
                }

                // Verify that the entrance position has a door or is walkable
                string badEntrances = "";
                TileResourceManager.Instance.Initialize();
                foreach (var tile in Tiles)
                {
                    if (tile.Type == null)
                    {
                        GD.PrintErr($"Tile at {tile.Position} does not have a Type");
                        return false;
                    }

                    var tileDef = TileResourceManager.Instance.GetTileDefinition(tile.Type.ToLower());
                    if (tileDef == null)
                    {
                        GD.PrintErr($"Tile at {tile.Position} of type {tile.Type} has no corresponding tile definition");
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
                    GD.PrintErr($"Building validation failed: Entrances {badEntrances} at position ({entrance.X}, {entrance.Y}) are not a door or walkable tile");
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Data structure for a tile in a building template
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
        public Dictionary<string, string> Properties { get; set; } = new();
    }
    /// <summary>
    /// Data structure for a room in a building template
    /// </summary>
    public class RoomData
    {
        public string? Name { get; set; }
        public string? Purpose { get; set; }
        public Vector2I TopLeft { get; set; }
        public Vector2I Size { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    /// <summary>
    /// JSON converter for Vector2I
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
                        if (propertyName == "x" || propertyName == "X")
                        {
                            x = reader.GetInt32();
                        }
                        else if (propertyName == "y" || propertyName == "Y")
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
}
