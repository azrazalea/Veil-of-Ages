using Godot;
using System;

public partial class Player : Node2D
{
    [Export]
    public float MoveSpeed = 0.2f; // Time in seconds to move one tile

    private Vector2 _targetPosition;
    private Vector2 _startPosition;
    private float _moveProgress = 1.0f; // 1.0 means movement complete
    private Vector2 _direction = Vector2.Zero;
    private AnimatedSprite2D _animatedSprite;
    private Vector2I _currentGridPos;

    // Reference to the grid system
    private GridSystem _gridSystem;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public void Initialize(GridSystem gridSystem, Vector2I startGridPos)
    {
        _gridSystem = gridSystem;
        _currentGridPos = startGridPos;

        // Set initial position aligned to the grid
        Position = _gridSystem.GridToWorld(_currentGridPos);
        _targetPosition = Position;
        _startPosition = Position;

        // Mark player's initial position as occupied
        _gridSystem.SetCellOccupied(_currentGridPos, true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gridSystem == null)
            return;

        if (Input.IsKeyPressed(Key.D))
        {
            _gridSystem.DebugGridCellStatus(_currentGridPos);
        }

        // If we're at the target position, check for new input
        if (_moveProgress >= 1.0f)
        {
            _direction = Vector2.Zero;

            if (Input.IsActionPressed("ui_right"))
                _direction = Vector2.Right;
            else if (Input.IsActionPressed("ui_left"))
                _direction = Vector2.Left;
            else if (Input.IsActionPressed("ui_down"))
                _direction = Vector2.Down;
            else if (Input.IsActionPressed("ui_up"))
                _direction = Vector2.Up;

            // If we have a new direction, try to move to the next tile
            if (_direction != Vector2.Zero)
            {
                Vector2I targetGridPos = _currentGridPos + new Vector2I(
                    (int)_direction.X,
                    (int)_direction.Y
                );

                // Check if the target cell is free
                if (!_gridSystem.IsCellOccupied(targetGridPos))
                {
                    // Free the current cell
                    _gridSystem.SetCellOccupied(_currentGridPos, false);

                    // Update current grid position
                    _currentGridPos = targetGridPos;

                    // Mark new cell as occupied
                    _gridSystem.SetCellOccupied(_currentGridPos, true);

                    // Start moving
                    _startPosition = Position;
                    _targetPosition = _gridSystem.GridToWorld(_currentGridPos);
                    _moveProgress = 0.0f;

                    // Handle animation and facing direction
                    if (_direction.X > 0)
                        _animatedSprite.FlipH = false;
                    else if (_direction.X < 0)
                        _animatedSprite.FlipH = true;

                    _animatedSprite.Play("walk");
                }
            }
            else
            {
                _animatedSprite.Play("idle");
            }
        }

        // If we're moving between tiles
        if (_moveProgress < 1.0f)
        {
            // Update progress
            _moveProgress += (float)delta / MoveSpeed;
            _moveProgress = Mathf.Min(_moveProgress, 1.0f);

            // Interpolate position
            Position = _startPosition.Lerp(_targetPosition, _moveProgress);
        }
    }

    // Called when the player interacts with objects
    public void Interact()
    {
        // Get the grid position in front of the player
        Vector2I interactGridPos = _currentGridPos + new Vector2I(
            (int)_direction.X,
            (int)_direction.Y
        );

        // Check if there's an interactable object at that position
        // This would be implemented later
        GD.Print($"Attempting to interact with object at grid position {interactGridPos}");
    }
}
