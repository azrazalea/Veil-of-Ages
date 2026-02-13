using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Entities;

/// <summary>
/// A TileMapLayer subclass that supports per-tile tinting via Godot's TileData runtime update system.
/// Used for building tile layers to allow visual differentiation (e.g., darker walls, tinted floors).
/// </summary>
public partial class TintableTileMapLayer : TileMapLayer
{
    private readonly Dictionary<Vector2I, Color> _tintMap = new ();

    /// <summary>
    /// Sets a tint color for a specific tile position.
    /// </summary>
    /// <param name="position">The tile position in the TileMapLayer.</param>
    /// <param name="color">The tint color to apply.</param>
    public void SetTileTint(Vector2I position, Color color)
    {
        _tintMap[position] = color;
    }

    /// <summary>
    /// Called by Godot to determine if a tile needs runtime data updates.
    /// Returns true if the tile has a tint configured.
    /// </summary>
    public override bool _UseTileDataRuntimeUpdate(Vector2I coords)
    {
        return _tintMap.ContainsKey(coords);
    }

    /// <summary>
    /// Called by Godot to apply runtime modifications to tile data.
    /// Sets the Modulate color for tinted tiles.
    /// </summary>
    public override void _TileDataRuntimeUpdate(Vector2I coords, TileData tileData)
    {
        if (_tintMap.TryGetValue(coords, out var color))
        {
            tileData.Modulate = color;
        }
    }
}
