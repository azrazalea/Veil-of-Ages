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
    /// Represents a material definition for a tile that can be loaded from JSON
    /// </summary>
    public class TileMaterialDefinition
    {
        // Unique identifier for the material
        public string Id { get; set; }
        
        // Display name for the material
        public string Name { get; set; }
        
        // Description of the material
        public string Description { get; set; }
        
        // Base durability modifier (percentage)
        public float DurabilityModifier { get; set; } = 1.0f;
        
        // Sensory detection difficulty modifiers
        public Dictionary<string, float> SensoryModifiers { get; set; } = new();
        
        // Additional properties specific to this material
        public Dictionary<string, string> Properties { get; set; } = new();

        /// <summary>
        /// Load a material definition from a JSON file
        /// </summary>
        /// <param name="path">Path to the JSON file</param>
        /// <returns>TileMaterialDefinition instance</returns>
        public static TileMaterialDefinition LoadFromJson(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<TileMaterialDefinition>(jsonContent, options);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading material definition: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the detection difficulty modifier for a specific sense type
        /// </summary>
        /// <param name="senseType">The sense type to get the modifier for</param>
        /// <returns>The modifier value (default 1.0 if not specified)</returns>
        public float GetSensoryModifier(SenseType senseType)
        {
            string senseTypeName = senseType.ToString();
            if (SensoryModifiers.TryGetValue(senseTypeName, out float modifier))
            {
                return modifier;
            }
            return 1.0f; // Default modifier
        }

        /// <summary>
        /// Validate this material definition
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate()
        {
            // Basic validation
            if (string.IsNullOrEmpty(Id)) return false;
            if (string.IsNullOrEmpty(Name)) return false;
            
            return true;
        }
    }
}