using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

// This class manages the grid-based collision system
namespace VeilOfAges.Grid;

public class System<T>
{
    public Vector2I GridSize = new (100, 100);

    // Dictionary to track which grid cells are occupied (supports multiple items per cell)
    public virtual Dictionary<Vector2I, List<T>> OccupiedCells { get; protected set; } = [];

    public System(Vector2I? gridSize)
    {
        if (gridSize != null)
        {
            GridSize = (Vector2I)gridSize;
        }

        Log.Print($"Grid system initialized with size {GridSize.X}x{GridSize.Y}");
    }

    /// <summary>
    /// Returns all items at the given cell, or null if empty.
    /// </summary>
    public List<T>? GetCell(Vector2I gridPos)
    {
        return OccupiedCells.GetValueOrDefault(gridPos);
    }

    /// <summary>
    /// Returns the first item at the given cell, or default if empty.
    /// Use for grid systems that store one item per cell (ground, objects).
    /// </summary>
    public T? GetFirstCell(Vector2I gridPos)
    {
        var list = OccupiedCells.GetValueOrDefault(gridPos);
        return list is { Count: > 0 } ? list[0] : default;
    }

    // Set a cell as occupied. Base implementation replaces existing content (single item per cell).
    public virtual void SetCell(Vector2I gridPos, T item)
    {
        if (gridPos.X >= 0 && gridPos.X < GridSize.X &&
            gridPos.Y >= 0 && gridPos.Y < GridSize.Y)
        {
            OccupiedCells[gridPos] = [item];
        }
    }

    /// <summary>
    /// Remove a specific item from a cell. Returns true if the item was found and removed.
    /// If the cell becomes empty, the key is removed from the dictionary.
    /// </summary>
    public bool RemoveFromCell(Vector2I gridPos, T item)
    {
        if (!OccupiedCells.TryGetValue(gridPos, out var list))
        {
            return false;
        }

        bool removed = list.Remove(item);
        if (list.Count == 0)
        {
            OccupiedCells.Remove(gridPos);
        }

        return removed;
    }

    // Set multiple cells as occupied or free (for multi-tile objects)
    public void SetMultipleCellsOccupied(Vector2I baseGridPos, Vector2I size, T obj)
    {
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Vector2I gridPos = new (baseGridPos.X + x, baseGridPos.Y + y);
                SetCell(gridPos, obj);
            }
        }
    }

    // Check if a grid cell is occupied
    public bool IsCellOccupied(Vector2I gridPos)
    {
        // Check if position is within grid bounds
        if (gridPos.X < 0 || gridPos.X >= GridSize.X ||
            gridPos.Y < 0 || gridPos.Y >= GridSize.Y)
        {
            Log.Print("Out of bounds!");
            return true; // Consider out-of-bounds as occupied
        }

        return OccupiedCells.ContainsKey(gridPos);
    }

    // Check if a grid cell is occupied based on world position
    public bool IsCellOccupied(Vector2 worldPos)
    {
        return IsCellOccupied(Utils.WorldToGrid(worldPos));
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
                Vector2I topPos = new (startPos.X + x, startPos.Y - radius);
                if (!IsCellOccupied(topPos))
                {
                    return topPos;
                }

                Vector2I bottomPos = new (startPos.X + x, startPos.Y + radius);
                if (!IsCellOccupied(bottomPos))
                {
                    return bottomPos;
                }
            }

            // Left and right columns (excluding corners already checked)
            for (int y = -radius + 1; y <= radius - 1; y++)
            {
                Vector2I leftPos = new (startPos.X - radius, startPos.Y + y);
                if (!IsCellOccupied(leftPos))
                {
                    return leftPos;
                }

                Vector2I rightPos = new (startPos.X + radius, startPos.Y + y);
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

    // Method to visualize the grid (for debugging)
    public void DebugPrintOccupiedCells()
    {
        string gridString = "Grid Occupancy:\n";

        for (int y = 0; y < 10; y++) // Print just a small section for debug
        {
            for (int x = 0; x < 10; x++)
            {
                Vector2I pos = new (x, y);
                gridString += IsCellOccupied(pos) ? "X " : ". ";
            }

            gridString += "\n";
        }

        Log.Print(gridString);
    }
}
