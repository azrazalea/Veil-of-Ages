using Godot;
using System;

public partial class Tree : Node2D
{
    [Export]
    public Vector2I GridSize = new(5, 6); // Size in tiles (most trees are 1x1)

    private Vector2I _gridPosition;
    private GridSystem _gridSystem;

    public override void _Ready()
    {
        // Find the grid system
        var world = GetTree().GetFirstNodeInGroup("World") as World;
        if (world != null)
        {
            _gridSystem = world.GetNode<GridSystem>("GridSystem");
        }
        else
        {
            GD.PrintErr("Tree: Could not find World node with GridSystem!");
            return;
        }

        // Get grid position
        _gridPosition = _gridSystem.WorldToGrid(GlobalPosition);

        // Snap to grid
        GlobalPosition = _gridSystem.GridToWorld(_gridPosition);

        // Mark the grid cells as occupied
        _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, true);

        GD.Print($"Tree registered at grid position {_gridPosition}");
    }

    // Initialize with an external grid system (useful for programmatic placement)
    public void Initialize(GridSystem gridSystem, Vector2I gridPos)
    {
        _gridSystem = gridSystem;
        _gridPosition = gridPos;

        // Update the actual position
        GlobalPosition = _gridSystem.GridToWorld(_gridPosition);

        // Register with the grid system
        _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, true);
    }

    public override void _ExitTree()
    {
        // When the tree is removed, mark its cells as unoccupied (if grid system exists)
        if (_gridSystem != null)
        {
            _gridSystem.SetMultipleCellsOccupied(_gridPosition, GridSize, false);
        }
    }

    // Method for when player interacts with tree
    public void Interact()
    {
        GD.Print("Player is interacting with tree");
        // This could later handle resource gathering, cutting down the tree, etc.
    }
}
