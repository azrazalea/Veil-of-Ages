using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;

namespace VeilOfAges.Entities;

/// <summary>
/// A purely visual decoration sprite placed in a building.
/// Decorations are divorced from the tile/walkability system.
/// Static decorations use AtlasTexture from the shared atlas system.
/// Animated decorations use AnimatedSprite2D with SpriteAnimationDefinition.
/// </summary>
public partial class Decoration : Sprite2D
{
    public string DecorationId { get; private set; } = string.Empty;
    public Vector2I GridPosition { get; private set; }
    public Vector2I PixelOffset { get; private set; }

    /// <summary>
    /// Initialize this decoration with its definition, position, and optional pixel offset.
    /// </summary>
    public void Initialize(DecorationDefinition definition, Vector2I gridPosition,
        Vector2I pixelOffset)
    {
        DecorationId = definition.Id;
        GridPosition = gridPosition;
        PixelOffset = pixelOffset;

        // Sprite2D defaults: top-left origin
        Centered = false;

        if (definition.AnimationId != null)
        {
            SetupAnimated(definition.AnimationId);
        }
        else if (definition.AtlasSource != null)
        {
            SetupStatic(definition);
        }

        // Position relative to parent Building node
        Position = new Vector2(
            (gridPosition.X * VeilOfAges.Grid.Utils.TileSize) + pixelOffset.X,
            (gridPosition.Y * VeilOfAges.Grid.Utils.TileSize) + pixelOffset.Y);
    }

    private void SetupStatic(DecorationDefinition definition)
    {
        var atlasInfo = TileResourceManager.Instance.GetAtlasInfo(definition.AtlasSource!);
        if (atlasInfo == null)
        {
            Log.Error($"Decoration '{definition.Id}': Atlas source '{definition.AtlasSource}' not found");
            return;
        }

        var (texture, tileSize, margin, separation) = atlasInfo.Value;

        var atlasTexture = new AtlasTexture { Atlas = texture };
        int px = margin.X + (definition.AtlasCoords.X * (tileSize.X + separation.X));
        int py = margin.Y + (definition.AtlasCoords.Y * (tileSize.Y + separation.Y));
        int pw = (definition.TileSize.X * tileSize.X) + ((definition.TileSize.X - 1) * separation.X);
        int ph = (definition.TileSize.Y * tileSize.Y) + ((definition.TileSize.Y - 1) * separation.Y);
        atlasTexture.Region = new Rect2(px, py, pw, ph);

        Texture = atlasTexture;
    }

    private void SetupAnimated(string animationId)
    {
        var animDef = TileResourceManager.Instance.GetDecorationAnimation(animationId);
        if (animDef == null)
        {
            Log.Error($"Decoration animation '{animationId}' not found");
            return;
        }

        // Hide parent Sprite2D texture, use AnimatedSprite2D child instead
        Texture = null;
        var animSprite = new AnimatedSprite2D
        {
            Centered = false,
            SpriteFrames = animDef.CreateSpriteFrames()
        };

        if (animSprite.SpriteFrames.HasAnimation("idle"))
        {
            animSprite.Play("idle");
        }

        AddChild(animSprite);
    }
}
