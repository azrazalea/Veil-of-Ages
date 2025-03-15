using Godot;
using System;

public partial class World : Node2D
{
    // Layers
    private TileMapLayer _groundLayer;
    private TileMapLayer _objectsLayer;
    private TileMapLayer _entitiesLayer;

    // The grid system
    private GridSystem _gridSystem;

    // Entities container
    private Node2D _entitiesContainer;

    // References
    private Player _player;

    [Export]
    public int TileSize = 32;

    [Export]
    public Vector2I WorldSizeInTiles = new Vector2I(100, 100);

    public override void _Ready()
    {
        // Get references to nodes
        _groundLayer = GetNode<TileMapLayer>("GroundLayer");
        _objectsLayer = GetNode<TileMapLayer>("ObjectsLayer");
        _entitiesLayer = GetNode<TileMapLayer>("EntitiesLayer");
        _entitiesContainer = GetNode<Node2D>("Entities");
        _gridSystem = GetNode<GridSystem>("GridSystem");

        // Get reference to the Player scene instance
        _player = GetNode<Player>("Entities/Player");

        // Initialize grid system with world bounds
        _gridSystem.GridSize = WorldSizeInTiles;
        _gridSystem.TileSize = TileSize;

        // For debugging - register occupied cells from collidable tiles
        RegisterCollidableTiles();

        // Register the player with the grid system
        if (_player != null)
        {
            Vector2I playerGridPos = _gridSystem.WorldToGrid(_player.Position);
            _player.Initialize(_gridSystem, playerGridPos);
        }
        else
        {
            GD.PrintErr("Player node not found! Make sure you've instanced Player.tscn as a child of Entities.");
        }
    }

    // Register all collidable tiles with the grid system
    private void RegisterCollidableTiles()
    {
        // Get all used cells from the collidables layer
        var collidableCells = _entitiesLayer.GetUsedCells();

        // Mark each as occupied in the grid system
        foreach (Vector2I cellPos in collidableCells)
        {
            _gridSystem.SetCellOccupied(cellPos, true);
        }

        GD.Print($"Registered {collidableCells.Count} collidable tiles with the grid system");
    }

    // Converts a world position to a grid position
    public Vector2I WorldToGrid(Vector2 worldPosition)
    {
        return _gridSystem.WorldToGrid(worldPosition);
    }

    // Converts a grid position to a world position
    public Vector2 GridToWorld(Vector2I gridPosition)
    {
        return _gridSystem.GridToWorld(gridPosition);
    }

    // Check if a grid cell is occupied
    public bool IsCellOccupied(Vector2I gridPosition)
    {
        return _gridSystem.IsCellOccupied(gridPosition);
    }

    // Set a cell as occupied or free in the grid
    public void SetCellOccupied(Vector2I gridPosition, bool occupied)
    {
        _gridSystem.SetCellOccupied(gridPosition, occupied);
    }
}
