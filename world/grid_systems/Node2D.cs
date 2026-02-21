using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Grid;

public class Node2DSystem(Vector2I? gridSize): System<Node2D>(gridSize)
{
    /// <summary>
    /// Override SetCell to append instead of replace, supporting multiple entities per cell.
    /// Decorations, facilities, and beings can coexist at the same grid position.
    /// </summary>
    public override void SetCell(Vector2I gridPos, Node2D item)
    {
        if (gridPos.X >= 0 && gridPos.X < GridSize.X &&
            gridPos.Y >= 0 && gridPos.Y < GridSize.Y)
        {
            if (!OccupiedCells.TryGetValue(gridPos, out var list))
            {
                list = new List<Node2D>();
                OccupiedCells[gridPos] = list;
            }

            list.Add(item);
        }
    }
}
