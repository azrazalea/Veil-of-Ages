using System.Text.Json.Serialization;
using Godot;

namespace VeilOfAges.Entities;

/// <summary>
/// JSON-serializable definition for terrain tiles (grass, dirt, path, water, etc.).
/// Loaded from resources/tiles/terrain/*.json.
/// </summary>
public class TerrainTileDefinition
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("AtlasSource")]
    public string AtlasSource { get; set; } = string.Empty;

    [JsonPropertyName("AtlasCoords")]
    public Vector2I AtlasCoords { get; set; }

    [JsonPropertyName("IsWalkable")]
    public bool IsWalkable { get; set; } = true;

    [JsonPropertyName("WalkDifficulty")]
    public float WalkDifficulty { get; set; }

    public bool Validate()
    {
        return !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(AtlasSource);
    }
}
