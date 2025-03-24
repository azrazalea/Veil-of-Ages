using Godot;
using System;
using VeilOfAges;
using VeilOfAges.Entities;

public partial class Tree : Node2D
{
    [Export]
    public Vector2I GridSize = new(5, 6); // Size in tiles (most trees are 1x1)

    private Vector2I _gridPosition;
    private VeilOfAges.Grid.Area? GridArea;

    public override void _Ready()
    {
        // Find the grid system
        if (GetTree().GetFirstNodeInGroup("World") is not World world)
        {
            GD.PrintErr("Building: Could not find World node with GridSystem!");
            return;
        }

        // Snap to grid
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(VeilOfAges.Grid.Area gridArea, Vector2I gridPos)
    {
        GridArea = gridArea;
        _gridPosition = gridPos;

        ZIndex = 1;
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);
    }

    public override void _ExitTree()
    {
        // When the tree is removed, mark its cells as unoccupied (if grid system exists)
        GridArea?.RemoveEntity(_gridPosition, GridSize);
    }

    // Method for when player interacts with tree
    public void Interact()
    {
        GD.Print("Player is interacting with tree");
        // This could later handle resource gathering, cutting down the tree, etc.
    }
}
