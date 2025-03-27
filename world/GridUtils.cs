
using Godot;

namespace VeilOfAges.Grid
{
    public class Utils
    {
        public const uint TileSize = 8;
        public const uint WorldOffset = 5; // Fix visual difference with grid
        public static Vector2I WaterAtlasCoords = new(3, 16);
        public Vector2I GridSize = new(100, 100);

        public static bool WithinProximityRangeOf(Vector2I currentPos, Vector2I targetPos, float proximityRange = 1)
        {
            // The 1.5fs account for diagonals
            return currentPos.DistanceSquaredTo(targetPos) < (proximityRange * 1.5f * proximityRange * 1.5f);
        }

        // Convert world position to grid coordinates, accounting for visual offset
        public static Vector2I WorldToGrid(Vector2 worldPos)
        {
            worldPos.Y += WorldOffset;

            return new Vector2I(
                Mathf.FloorToInt(worldPos.X / TileSize),
                Mathf.FloorToInt(worldPos.Y / TileSize)
            );
        }

        // Convert grid coordinates to world position (centered in the tile), accounting for visual offset
        public static Vector2 GridToWorld(Vector2I gridPos)
        {
            Vector2 worldPos = new(
                gridPos.X * TileSize + TileSize / 2,
                gridPos.Y * TileSize + TileSize / 2
            );

            worldPos.Y -= WorldOffset;

            return worldPos;
        }

        // Helper method to convert a path of world positions to grid positions
        public static Vector2I[] WorldPathToGridPath(Vector2[] worldPath)
        {
            Vector2I[] gridPath = new Vector2I[worldPath.Length];
            for (int i = 0; i < worldPath.Length; i++)
            {
                gridPath[i] = WorldToGrid(worldPath[i]);
            }
            return gridPath;
        }

        // Helper method to convert a path of grid positions to world positions
        public static Vector2[] GridPathToWorldPath(Vector2I[] gridPath)
        {
            Vector2[] worldPath = new Vector2[gridPath.Length];
            for (int i = 0; i < gridPath.Length; i++)
            {
                worldPath[i] = GridToWorld(gridPath[i]);
            }
            return worldPath;
        }

    }
}
