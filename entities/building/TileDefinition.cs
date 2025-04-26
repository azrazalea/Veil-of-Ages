using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Represents a tile definition that can be loaded from JSON
    /// </summary>
    public class TileDefinition
    {
        // Unique identifier for the tile
        public string Id { get; set; }
        
        // Display name for the tile
        public string Name { get; set; }
        
        // Description of the tile
        public string Description { get; set; }
        
        // Default tile type
        public string Type { get; set; }
        
        // Default material ID
        public string DefaultMaterial { get; set; }
        
        // Is this tile walkable by default?
        public bool IsWalkable { get; set; }
        
        // Base durability of the tile
        public int BaseDurability { get; set; } = 100;
        
        // Atlas source name reference
        public string AtlasSource { get; set; }
        
        // Atlas coordinates within the source
        public Vector2I AtlasCoords { get; set; }
        
        // Default sensory detection difficulties
        public Dictionary<string, float> DefaultSensoryDifficulties { get; set; } = new();
        
        // Additional properties specific to this tile type
        public Dictionary<string, string> Properties { get; set; } = new();

        /// <summary>
        /// Load a tile definition from a JSON file
        /// </summary>
        /// <param name="path">Path to the JSON file</param>
        /// <returns>TileDefinition instance</returns>
        public static TileDefinition LoadFromJson(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new Vector2IConverter() }
                };

                return JsonSerializer.Deserialize<TileDefinition>(jsonContent, options);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading tile definition: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the default detection difficulty for a specific sense type
        /// </summary>
        /// <param name="senseType">The sense type to get the difficulty for</param>
        /// <returns>The difficulty value (default 0.0 if not specified)</returns>
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
        /// Validate this tile definition
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate()
        {
            // Basic validation
            if (string.IsNullOrEmpty(Id)) return false;
            if (string.IsNullOrEmpty(Name)) return false;
            if (string.IsNullOrEmpty(Type)) return false;
            if (string.IsNullOrEmpty(AtlasSource)) return false;
            
            return true;
        }
    }
}