using Godot;

namespace VeilOfAges.Entities;

/// <summary>
/// Defines a decoration (visual prop) that can be placed in buildings.
/// Static decorations use the atlas system; animated ones use SpriteAnimationDefinition.
/// </summary>
public class DecorationDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Static mode — references an existing tile atlas
    public string? AtlasSource { get; set; }
    public Vector2I AtlasCoords { get; set; }
    public Vector2I TileSize { get; set; } = new (1, 1);

    // Animated mode — references a SpriteAnimationDefinition
    public string? AnimationId { get; set; }

    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return false;
        }

        return AtlasSource != null || AnimationId != null;
    }
}
