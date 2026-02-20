using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// A purely visual decoration sprite placed in a building.
/// Decorations can optionally block walkability for non-interactive props like tombstones.
/// Implements IEntity&lt;Trait&gt; so it registers as a proper grid entity for pathfinding.
/// Static decorations use AtlasTexture from the shared atlas system.
/// Animated decorations use AnimatedSprite2D with SpriteAnimationDefinition.
/// </summary>
public partial class Decoration : Sprite2D, IEntity<Trait>
{
    public string DecorationId { get; private set; } = string.Empty;
    public Vector2I GridPosition { get; private set; }
    public Vector2I PixelOffset { get; private set; }

    /// <summary>
    /// Gets a value indicating whether entities can walk through this decoration's tiles.
    /// </summary>
    public bool IsWalkable { get; private set; } = true;

    /// <summary>
    /// Gets all grid positions this decoration occupies (primary + additional).
    /// Positions are relative to the parent building.
    /// </summary>
    public List<Vector2I> AllPositions { get; private set; } = new ();

    /// <summary>
    /// Gets or sets the absolute grid position of this decoration's primary tile.
    /// Set by Building during initialization.
    /// </summary>
    public Vector2I AbsoluteGridPosition { get; set; }

    /// <summary>
    /// Gets or sets reference to the grid area this decoration is registered in.
    /// </summary>
    public VeilOfAges.Grid.Area? GridArea { get; set; }

    /// <summary>
    /// Gets decorations have no traits.
    /// </summary>
    public SortedSet<Trait> Traits { get; } = [];

    /// <summary>
    /// Gets non-walkable decorations block line of sight; walkable ones do not.
    /// </summary>
    public Dictionary<SenseType, float> DetectionDifficulties { get; private set; } = [];

    /// <summary>
    /// Initialize this decoration with its definition, position, and optional pixel offset.
    /// </summary>
    public void Initialize(DecorationDefinition definition, Vector2I gridPosition,
        Vector2I pixelOffset, bool isWalkable = true, List<Vector2I>? additionalPositions = null)
    {
        DecorationId = definition.Id;
        GridPosition = gridPosition;
        PixelOffset = pixelOffset;
        IsWalkable = isWalkable;

        // Build the full list of positions
        AllPositions = new List<Vector2I> { gridPosition };
        if (additionalPositions != null)
        {
            AllPositions.AddRange(additionalPositions);
        }

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

        // Non-walkable decorations block sight
        if (!isWalkable)
        {
            DetectionDifficulties[SenseType.Sight] = 1.0f;
        }
    }

    /// <summary>
    /// Gets the absolute grid position of this decoration's primary tile.
    /// Used by the ISensable interface for spatial awareness.
    /// </summary>
    public Vector2I GetCurrentGridPosition()
    {
        return AbsoluteGridPosition;
    }

    /// <summary>
    /// Decorations are sensed as objects (not beings or buildings).
    /// </summary>
    public SensableType GetSensableType()
    {
        return SensableType.WorldObject;
    }

    private void SetupStatic(DecorationDefinition definition)
    {
        var atlasTexture = TileResourceManager.Instance.GetCachedAtlasTexture(
            definition.AtlasSource!, definition.AtlasCoords.Y, definition.AtlasCoords.X,
            definition.TileSize.X, definition.TileSize.Y);
        if (atlasTexture == null)
        {
            Log.Error($"Decoration '{definition.Id}': Failed to get atlas texture for '{definition.AtlasSource}'");
            return;
        }

        Texture = atlasTexture;
    }

    private void SetupAnimated(string animationId)
    {
        var spriteFrames = TileResourceManager.Instance.GetCachedSpriteFrames(animationId);
        if (spriteFrames == null)
        {
            Log.Error($"Decoration animation '{animationId}' not found");
            return;
        }

        // Hide parent Sprite2D texture, use AnimatedSprite2D child instead
        Texture = null;
        var animSprite = new AnimatedSprite2D
        {
            Centered = false,
            SpriteFrames = spriteFrames
        };

        if (animSprite.SpriteFrames.HasAnimation("idle"))
        {
            animSprite.Play("idle");
        }

        AddChild(animSprite);
    }
}
