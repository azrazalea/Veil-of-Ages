using Godot;
using System;
using System.Collections.Generic;

// This class manages the grid-based collision system
public partial class GridSystem : Node
{
    [Export]
    public int TileSize = 8;

    [Export]
    public int WorldOffset = 5; // Fix visual difference with grid

    [Export]
    public Vector2I GridSize = new(100, 100);
    [Export]
    public Vector2I WaterAtlasCoords = new(3, 16);

    // Dictionary to track which grid cells are occupied
    // The bool could be extended to an enum or object to track different types of entities
    private Dictionary<Vector2I, bool> _occupiedCells = [];

    public void DebugGridCellStatus(Vector2I gridPos)
    {
        var groundLayer = GetNode<TileMapLayer>("/root/World/GroundLayer");

        if (gridPos.X < 0 || gridPos.X >= GridSize.X ||
    gridPos.Y < 0 || gridPos.Y >= GridSize.Y)
        {
            GD.Print($"Grid position {gridPos} is out of bounds");
            return;
        }

        bool isOccupied = IsCellOccupied(gridPos);
        var atlasCoords = groundLayer.GetCellAtlasCoords(gridPos);

        GD.Print($"Grid position {gridPos}:");
        GD.Print($"  - Is occupied: {isOccupied}");
        GD.Print($"  - Tile atlas coords: {atlasCoords}");
        GD.Print($"  - Is water: {(atlasCoords == WaterAtlasCoords)}");
    }

    public override void _Ready()
    {
        // Initialize the grid as empty
        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                _occupiedCells[new Vector2I(x, y)] = false;
            }
        }

        GD.Print($"Grid system initialized with size {GridSize.X}x{GridSize.Y}");
    }

    // Convert world position to grid coordinates, accounting for visual offset
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        worldPos.Y += WorldOffset;

        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / TileSize),
            Mathf.FloorToInt(worldPos.Y / TileSize)
        );
    }

    // Convert grid coordinates to world position (centered in the tile), accounting for visual offset
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        Vector2 worldPos = new(
            gridPos.X * TileSize + TileSize / 2,
            gridPos.Y * TileSize + TileSize / 2
        );

        worldPos.Y -= WorldOffset;

        return worldPos;
    }

    // Check if a grid cell is occupied
    public bool IsCellOccupied(Vector2I gridPos)
    {
        // Check if position is within grid bounds
        if (gridPos.X < 0 || gridPos.X >= GridSize.X ||
            gridPos.Y < 0 || gridPos.Y >= GridSize.Y)
        {
            return true; // Consider out-of-bounds as occupied
        }

        return _occupiedCells.ContainsKey(gridPos) && _occupiedCells[gridPos];
    }

    // Check if a grid cell is occupied based on world position
    public bool IsCellOccupied(Vector2 worldPos)
    {
        return IsCellOccupied(WorldToGrid(worldPos));
    }

    // Set a cell as occupied or free
    public void SetCellOccupied(Vector2I gridPos, bool occupied)
    {
        if (gridPos.X >= 0 && gridPos.X < GridSize.X &&
            gridPos.Y >= 0 && gridPos.Y < GridSize.Y)
        {
            _occupiedCells[gridPos] = occupied;
        }
    }

    // Set multiple cells as occupied or free (for multi-tile objects)
    public void SetMultipleCellsOccupied(Vector2I baseGridPos, Vector2I size, bool occupied)
    {
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Vector2I gridPos = new(baseGridPos.X + x, baseGridPos.Y + y);
                SetCellOccupied(gridPos, occupied);
            }
        }
    }

    // Find the first free cell near a given position (useful for placing objects)
    public Vector2I FindNearestFreeCell(Vector2I startPos, int maxSearchRadius = 5)
    {
        // First check the starting position
        if (!IsCellOccupied(startPos))
        {
            return startPos;
        }

        // Search in expanding squares around the start position
        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            // Top and bottom rows
            for (int x = -radius; x <= radius; x++)
            {
                Vector2I topPos = new(startPos.X + x, startPos.Y - radius);
                if (!IsCellOccupied(topPos))
                {
                    return topPos;
                }

                Vector2I bottomPos = new(startPos.X + x, startPos.Y + radius);
                if (!IsCellOccupied(bottomPos))
                {
                    return bottomPos;
                }
            }

            // Left and right columns (excluding corners already checked)
            for (int y = -radius + 1; y <= radius - 1; y++)
            {
                Vector2I leftPos = new(startPos.X - radius, startPos.Y + y);
                if (!IsCellOccupied(leftPos))
                {
                    return leftPos;
                }

                Vector2I rightPos = new(startPos.X + radius, startPos.Y + y);
                if (!IsCellOccupied(rightPos))
                {
                    return rightPos;
                }
            }
        }

        // If no free cell found within the search radius, return the start position
        // (this can be handled by the caller)
        return startPos;
    }

    // Helper method to convert a path of world positions to grid positions
    public Vector2I[] WorldPathToGridPath(Vector2[] worldPath)
    {
        Vector2I[] gridPath = new Vector2I[worldPath.Length];
        for (int i = 0; i < worldPath.Length; i++)
        {
            gridPath[i] = WorldToGrid(worldPath[i]);
        }
        return gridPath;
    }

    // Helper method to convert a path of grid positions to world positions
    public Vector2[] GridPathToWorldPath(Vector2I[] gridPath)
    {
        Vector2[] worldPath = new Vector2[gridPath.Length];
        for (int i = 0; i < gridPath.Length; i++)
        {
            worldPath[i] = GridToWorld(gridPath[i]);
        }
        return worldPath;
    }

    // Method to visualize the grid (for debugging)
    public void DebugPrintOccupiedCells()
    {
        string gridString = "Grid Occupancy:\n";

        for (int y = 0; y < 10; y++) // Print just a small section for debug
        {
            for (int x = 0; x < 10; x++)
            {
                Vector2I pos = new(x, y);
                gridString += IsCellOccupied(pos) ? "X " : ". ";
            }
            gridString += "\n";
        }

        GD.Print(gridString);
    }
}
