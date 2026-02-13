using Godot;
using VeilOfAges.Grid;

namespace VeilOfAges;

/// <summary>
/// Represents one side of a bidirectional area transition link.
/// Two TransitionPoints are linked together to form a passage between areas.
/// </summary>
public class TransitionPoint
{
    /// <summary>
    /// Gets the area this transition point is in.
    /// </summary>
    public Area SourceArea { get; }

    /// <summary>
    /// Gets the grid position within the source area.
    /// </summary>
    public Vector2I SourcePosition { get; }

    /// <summary>
    /// Gets or sets the linked transition point on the other side.
    /// </summary>
    public TransitionPoint? LinkedPoint { get; set; }

    /// <summary>
    /// Gets the display label for this transition (e.g., "Trapdoor", "Ladder").
    /// </summary>
    public string Label { get; }

    public TransitionPoint(Area sourceArea, Vector2I sourcePosition, string label)
    {
        SourceArea = sourceArea;
        SourcePosition = sourcePosition;
        Label = label;
    }

    /// <summary>
    /// Link two transition points bidirectionally.
    /// </summary>
    public static void Link(TransitionPoint a, TransitionPoint b)
    {
        a.LinkedPoint = b;
        b.LinkedPoint = a;
    }
}
