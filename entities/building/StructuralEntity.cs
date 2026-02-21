using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// An individual structural element (wall, floor, door, fence, etc.) that renders as a Sprite2D.
/// Replaces the TileMapLayer-based rendering from the old Building system.
/// Each structural tile is its own node in the scene tree, allowing per-tile damage, removal, etc.
/// </summary>
public partial class StructuralEntity : Sprite2D, IEntity<Trait>
{
    // Core identity
    public TileType Type { get; }
    public new string Material { get; }
    public string Variant { get; }

    // Grid integration
    public Vector2I GridPosition { get; }
    public VeilOfAges.Grid.Area? GridArea { get; set; }

    // Walkability and room detection

    /// <summary>
    /// Gets or sets a value indicating whether whether entities can walk through this tile. True for floors, doors, gates. False for walls, fences, columns.
    /// </summary>
    public bool IsWalkable { get; set; }

    /// <summary>
    /// Gets a value indicating whether whether this tile blocks flood fill for room detection.
    /// True for Wall, Fence, Window, Column — these form room boundaries.
    /// </summary>
    public bool IsRoomBoundary { get; }

    /// <summary>
    /// Gets a value indicating whether whether this tile divides rooms. True for Door, Gate.
    /// Also blocks flood fill, but belongs to adjacent rooms rather than a single room.
    /// </summary>
    public bool IsRoomDivider { get; }

    // Durability
    public int Durability { get; set; }
    public int MaxDurability { get; }

    // Sensory
    public Dictionary<SenseType, float> DetectionDifficulties { get; private set; } = [];

    // IEntity implementation
    public SortedSet<Trait> Traits { get; } = [];

    // Visual data (for reconstruction/serialization)
    public Vector2I AtlasCoords { get; }
    public int SourceId { get; }
    public Color? TintColor { get; }

    /// <summary>
    /// Gets the template layer this tile came from (e.g., "Ground", null for default).
    /// Used to determine Z-ordering: ground layer tiles render below normal floor tiles.
    /// </summary>
    public string? Layer { get; }

    public StructuralEntity(
        TileType type,
        string material,
        string variant,
        Vector2I gridPosition,
        bool isWalkable,
        int durability,
        Vector2I atlasCoords,
        int sourceId,
        Dictionary<SenseType, float> detectionDifficulties,
        Color? tintColor = null,
        string? layer = null)
    {
        Type = type;
        Material = material;
        Variant = variant;
        GridPosition = gridPosition;
        IsWalkable = isWalkable;
        Durability = durability;
        MaxDurability = durability;
        AtlasCoords = atlasCoords;
        SourceId = sourceId;
        DetectionDifficulties = detectionDifficulties;
        TintColor = tintColor;
        Layer = layer;

        // Determine room boundary/divider based on tile type
        IsRoomBoundary = type is TileType.Wall or TileType.Fence or TileType.Window or TileType.Column;
        IsRoomDivider = type is TileType.Door or TileType.Gate;
    }

    /// <summary>
    /// Initialize the visual representation. Must be called after construction,
    /// before adding to scene tree.
    /// </summary>
    public void InitializeVisual(AtlasTexture? texture)
    {
        Centered = false;

        if (texture != null)
        {
            Texture = texture;
        }

        // Position based on absolute grid position
        Position = new Vector2(
            GridPosition.X * VeilOfAges.Grid.Utils.TileSize,
            GridPosition.Y * VeilOfAges.Grid.Utils.TileSize);

        // Z-ordering: ground layer=1, floors=2, walls/doors/other=3, facilities/decorations=4
        if (string.Equals(Layer, "Ground", StringComparison.OrdinalIgnoreCase))
        {
            ZIndex = 1;
        }
        else
        {
            ZIndex = Type == TileType.Floor ? 2 : 3;
        }

        // Apply tint if configured
        if (TintColor.HasValue)
        {
            Modulate = TintColor.Value;
        }
    }

    public Vector2I GetCurrentGridPosition() => GridPosition;

    public SensableType GetSensableType() => SensableType.WorldObject;

    public IEntity<Trait> SelfAsEntity() => this;

    /// <summary>
    /// Apply damage to this structural entity.
    /// </summary>
    /// <returns>True if destroyed (durability reached 0).</returns>
    public bool TakeDamage(int amount)
    {
        Durability -= amount;
        if (Durability <= 0)
        {
            Durability = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Repair this structural entity.
    /// </summary>
    public void Repair(int amount)
    {
        Durability += amount;
        if (Durability > MaxDurability)
        {
            Durability = MaxDurability;
        }
    }

    /// <summary>
    /// Get the current condition as a percentage (0.0 to 1.0).
    /// </summary>
    public float GetConditionPercentage()
    {
        if (MaxDurability <= 0)
        {
            return 1.0f;
        }

        return (float)Durability / MaxDurability;
    }

    public override void _ExitTree()
    {
        // Skip grid unregistration when the parent area is being deactivated (removed from tree
        // for rendering purposes only). Without this guard, removing an area from the tree would
        // corrupt the AStarGrid by unregistering all walls.
        if (GridArea is { IsDeactivating: true })
        {
            return;
        }

        // Unregister from grid when genuinely removed from scene tree
        if (GridArea != null)
        {
            if (!IsWalkable)
            {
                GridArea.RemoveEntity(this, GridPosition);
            }
        }
    }
}
