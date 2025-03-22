using Godot;

namespace VeilOfAges.Grid
{
    public class Tile(
        int sourceId,
        Vector2I atlasCoords,
        bool isWalkable,
        float walkDifficulty = 0
    )
    {
        public int SourceId { get; private set; } = sourceId;
        public Vector2I AtlasCoords { get; private set; } = atlasCoords;
        public bool IsWalkable { get; private set; } = isWalkable;
        public float WalkDifficulty { get; private set; } = walkDifficulty;
    }
}
