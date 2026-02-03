namespace VeilOfAges.Entities;

/// <summary>
/// Marker interface for entities that should block A* pathfinding.
/// Entities implementing this interface will be marked as solid in the AStarGrid2D
/// when added to a GridArea.
/// </summary>
public interface IBlocksPathfinding
{
}
