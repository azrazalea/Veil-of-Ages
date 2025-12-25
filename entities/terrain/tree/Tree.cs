using System;
using Godot;
using VeilOfAges;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

public partial class Tree : Node2D
{
    [Export]
    public Vector2I GridSize = new (5, 6); // Size in tiles (most trees are 1x1)

    private Vector2I _gridPosition;
    private VeilOfAges.Grid.Area? gridArea;

    public override void _Ready()
    {
        // Find the grid system
        if (GetTree().GetFirstNodeInGroup("World") is not World)
        {
            Log.Error("Building: Could not find World node with GridSystem!");
            return;
        }

        // Snap to grid
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(VeilOfAges.Grid.Area gridArea, Vector2I gridPos)
    {
        this.gridArea = gridArea;
        _gridPosition = gridPos;

        ZIndex = 1;
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    public override void _ExitTree()
    {
        // When the tree is removed, mark its cells as unoccupied (if grid system exists)
        gridArea?.RemoveEntity(_gridPosition, GridSize);
    }

    // Method for when player interacts with tree
    public void Interact()
    {
        Log.Print("Player is interacting with tree");

        // This could later handle resource gathering, cutting down the tree, etc.
    }
}
