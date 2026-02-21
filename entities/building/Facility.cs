using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Entities.WorkOrders;

namespace VeilOfAges.Entities;

/// <summary>
/// A functional component within a building (e.g., oven, well, storage area).
/// Extends Sprite2D to own its own visual representation and grid presence.
/// Implements IEntity&lt;Trait&gt; so it registers as a proper grid entity for pathfinding.
/// </summary>
public partial class Facility : Sprite2D, IEntity<Trait>
{
    public string Id { get; }
    public List<Vector2I> Positions { get; }
    public bool RequireAdjacent { get; }
    public SortedSet<Trait> Traits { get; } = [];

    /// <summary>
    /// Gets or sets the room this facility belongs to.
    /// Set when Room.AddFacility() is called.
    /// </summary>
    public Room? ContainingRoom { get; set; }

    /// <summary>
    /// Gets the absolute grid position of this facility's primary tile (Positions[0]).
    /// Set during entity registration via GridArea.AddEntity().
    /// </summary>
    public Vector2I GridPosition { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether entities can walk through this facility's tiles.
    /// When false, all positions are marked solid in the A* grid via AddEntity().
    /// </summary>
    public bool IsWalkable { get; set; } = true;

    /// <summary>
    /// Gets or sets reference to the grid area this facility is registered in.
    /// </summary>
    public VeilOfAges.Grid.Area? GridArea { get; set; }

    /// <summary>
    /// Gets or sets the interaction handler for this facility, if any.
    /// </summary>
    public IFacilityInteractable? Interactable { get; set; }

    /// <summary>
    /// Gets non-walkable facilities block line of sight; walkable ones do not.
    /// </summary>
    public Dictionary<SenseType, float> DetectionDifficulties { get; private set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Facility"/> class.
    /// Positions are always absolute grid coordinates.
    /// </summary>
    public Facility(string id, List<Vector2I> positions, bool requireAdjacent)
    {
        Id = id;
        Positions = positions;
        RequireAdjacent = requireAdjacent;
    }

    /// <summary>
    /// Initialize the visual representation of this facility using a decoration definition.
    /// Sets up the sprite using the same atlas pattern as Decoration.Initialize().
    /// </summary>
    /// <param name="definition">The decoration definition to use for the sprite.</param>
    /// <param name="gridPosition">The relative grid position within the building.</param>
    /// <param name="pixelOffset">Optional pixel offset for fine positioning.</param>
    public void InitializeVisual(DecorationDefinition definition, Vector2I gridPosition,
        Vector2I pixelOffset)
    {
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

        // Position relative to parent node
        Position = new Vector2(
            (gridPosition.X * VeilOfAges.Grid.Utils.TileSize) + pixelOffset.X,
            (gridPosition.Y * VeilOfAges.Grid.Utils.TileSize) + pixelOffset.Y);

        // Non-walkable facilities block sight
        if (!IsWalkable)
        {
            DetectionDifficulties[SenseType.Sight] = 1.0f;
        }
    }

    private void SetupStatic(DecorationDefinition definition)
    {
        var atlasTexture = TileResourceManager.Instance.GetCachedAtlasTexture(
            definition.AtlasSource!, definition.AtlasCoords.Y, definition.AtlasCoords.X,
            definition.TileSize.X, definition.TileSize.Y);
        if (atlasTexture == null)
        {
            Log.Error($"Facility '{Id}': Failed to get atlas texture for '{definition.AtlasSource}'");
            return;
        }

        Texture = atlasTexture;
    }

    private void SetupAnimated(string animationId)
    {
        var spriteFrames = TileResourceManager.Instance.GetCachedSpriteFrames(animationId);
        if (spriteFrames == null)
        {
            Log.Error($"Facility '{Id}': Animation '{animationId}' not found");
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

    /// <summary>
    /// Returns this facility as its interface type, allowing access to default
    /// interface method implementations (AddTrait, GetTrait, HasTrait, etc.).
    /// Same pattern as Being.SelfAsEntity().
    /// </summary>
    public IEntity<Trait> SelfAsEntity()
    {
        return this;
    }

    /// <summary>
    /// Get all absolute grid positions of this facility.
    /// Positions are always absolute in the new architecture.
    /// </summary>
    /// <returns>The absolute grid positions.</returns>
    public List<Vector2I> GetAbsolutePositions()
    {
        return new List<Vector2I>(Positions);
    }

    /// <summary>
    /// Gets the absolute grid position of this facility's primary tile.
    /// Used by the ISensable interface for spatial awareness.
    /// </summary>
    public Vector2I GetCurrentGridPosition()
    {
        return GridPosition;
    }

    /// <summary>
    /// Facilities are sensed as objects (not beings or buildings).
    /// </summary>
    public SensableType GetSensableType()
    {
        return SensableType.WorldObject;
    }

    /// <summary>
    /// Sets the absolute grid position of this facility's primary tile.
    /// Called during initialization after placement.
    /// </summary>
    internal void SetGridPosition(Vector2I absolutePosition)
    {
        GridPosition = absolutePosition;
    }

    /// <summary>
    /// Gets the currently active work order on this facility, if any.
    /// </summary>
    public WorkOrder? ActiveWorkOrder { get; private set; }

    /// <summary>
    /// Start a work order on this facility.
    /// </summary>
    public void StartWorkOrder(WorkOrder order)
    {
        if (ActiveWorkOrder != null)
        {
            Log.Warn($"Facility {Id}: Cannot start work order - already has active order");
            return;
        }

        ActiveWorkOrder = order;
    }

    /// <summary>
    /// Complete and clear the active work order.
    /// </summary>
    public void CompleteWorkOrder()
    {
        ActiveWorkOrder = null;
    }

    /// <summary>
    /// Cancel the active work order (progress is lost).
    /// </summary>
    public void CancelWorkOrder()
    {
        ActiveWorkOrder = null;
    }
}
