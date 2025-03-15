using Godot;
using System;

// Base class for all beings (living and undead)
public abstract partial class Being : CharacterBody2D
{
    [Export]
    public float MoveSpeed = 0.2f; // Time in seconds to move one tile

    protected Vector2 _targetPosition;
    protected Vector2 _startPosition;
    protected float _moveProgress = 1.0f; // 1.0 means movement complete
    protected Vector2 _direction = Vector2.Zero;
    protected AnimatedSprite2D _animatedSprite;
    protected Vector2I _currentGridPos;

    // Reference to the grid system
    protected GridSystem _gridSystem;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public virtual void Initialize(GridSystem gridSystem, Vector2I startGridPos)
    {
        _gridSystem = gridSystem;
        _currentGridPos = startGridPos;

        // Set initial position aligned to the grid
        Position = _gridSystem.GridToWorld(_currentGridPos);
        _targetPosition = Position;
        _startPosition = Position;

        // Mark being's initial position as occupied
        _gridSystem.SetCellOccupied(_currentGridPos, true);
    }

    // Move to a specific grid position if possible
    protected bool TryMoveToGridPosition(Vector2I targetGridPos)
    {
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

            return true;
        }

        return false;
    }

    // Update position based on movement progress
    protected void UpdateMovement(double delta)
    {
        // If we're moving between tiles
        if (_moveProgress < 1.0f)
        {
            // Update progress
            _moveProgress += (float)delta / MoveSpeed;
            _moveProgress = Mathf.Min(_moveProgress, 1.0f);

            // Interpolate position
            Position = _startPosition.Lerp(_targetPosition, _moveProgress);
        }
        else
        {
            // If movement complete and no direction, play idle animation
            if (_direction == Vector2.Zero)
            {
                _animatedSprite.Play("idle");
            }
        }
    }

    // Set a new direction for the being
    protected void SetDirection(Vector2 newDirection)
    {
        _direction = newDirection;
    }

    // Get the current grid position
    public Vector2I GetCurrentGridPosition()
    {
        return _currentGridPos;
    }
}
