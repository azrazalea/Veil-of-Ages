using System;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.BeingServices;

public class MovementController
{
    private readonly Being _owner;

    // Movement state
    private Vector2 _targetPosition;
    private Vector2 _startPosition;
    private Vector2 _direction = Vector2.Zero;
    private Vector2I _currentGridPos;

    // Movement points system
    private float _movementPointsPerTick; // Points gained per tick
    private float _movementPointsAccumulator; // Accumulated movement points

    // Visual movement tracking
    private bool _isMoving;
    private float _currentMoveCost; // Total cost of current move

    // Blocking state - set when movement fails due to another entity
    // Cleared when consumed by Think() to generate appropriate action
    private Being? _lastBlockingEntity;
    private Vector2I _lastBlockedTargetPosition;

    public PathFinder MyPathfinder { get; set; } = new PathFinder();

    /// <summary>
    /// Get and clear the last entity that blocked our movement.
    /// Returns null if not blocked by an entity.
    /// </summary>
    public (Being? blocker, Vector2I targetPosition) ConsumeBlockingEntity()
    {
        var blocker = _lastBlockingEntity;
        var target = _lastBlockedTargetPosition;
        _lastBlockingEntity = null;
        _lastBlockedTargetPosition = Vector2I.Zero;
        return (blocker, target);
    }

    public MovementController(Being owner, float movementPointsPerTick = 0.55f)
    {
        _owner = owner;
        _movementPointsPerTick = movementPointsPerTick;
    }

    public void Initialize(Vector2I startGridPos)
    {
        _currentGridPos = startGridPos;
        _targetPosition = Utils.GridToWorld(_currentGridPos);
        _startPosition = _targetPosition;

        // Position the entity at the grid position
        _owner.Position = _targetPosition;

        _owner.GetGridArea()?.AddEntity(_currentGridPos, _owner);
    }

    // Process movement progress for this tick
    public void ProcessMovementTick()
    {
        // Only accumulate points and update movement if we're moving
        if (_isMoving)
        {
            // Add movement points for this tick
            _movementPointsAccumulator += _movementPointsPerTick;

            // Calculate movement progress (0.0 to 1.0)
            float progress = Math.Min(1.0f, _movementPointsAccumulator / _currentMoveCost);

            // Update position based on interpolation
            _owner.Position = _startPosition.Lerp(_targetPosition, progress);

            // Check if movement is complete
            if (_movementPointsAccumulator >= _currentMoveCost)
            {
                CompleteMove();
            }
        }
    }

    // Try to move to a specific grid position
    public bool TryMoveToGridPosition(Vector2I targetGridPos)
    {
        // Already moving, can't start another move
        if (_isMoving)
        {
            return false;
        }

        // Check we are only moving one distance at a time
        float distanceSquared = _currentGridPos.DistanceSquaredTo(targetGridPos);

        // Determine movement cost based on distance
        float movementCost;
        if (distanceSquared <= 1) // Cardinal direction (up, down, left, right)
        {
            movementCost = 1.0f;
        }
        else if (distanceSquared <= 2) // Diagonal movement
        {
            movementCost = 1.414f; // √2 for diagonal
        }
        else
        {
            return false; // We are trying to move too far in one step
        }

        // Get grid area
        var gridArea = _owner.GridArea;
        if (gridArea == null)
        {
            return false;
        }

        // Check if target cell is walkable (terrain/structures)
        if (!gridArea.IsCellWalkable(targetGridPos))
        {
            // Blocked by non-walkable terrain or structure — no entity interaction
            return false;
        }

        // Check if another Being occupies the target cell
        // (separate from terrain walkability — Beings are walkable for A* but block movement at runtime)
        var occupant = gridArea.EntitiesGridSystem.GetCell(targetGridPos);
        if (occupant is Being blockingBeing && blockingBeing != _owner)
        {
            // Store the blocking entity so Think() can decide how to respond
            // (communication action for sapient, push action for mindless)
            // This costs a turn - the entity must take an action to interact
            _lastBlockingEntity = blockingBeing;
            _lastBlockedTargetPosition = targetGridPos;
            return false;
        }

        // Apply terrain difficulty modifier
        float terrainDifficulty = (gridArea.GetTerrainDifficulty(targetGridPos) +
                                  gridArea.GetTerrainDifficulty(_currentGridPos)) / 2;

        // Calculate total movement cost
        float totalCost = movementCost * terrainDifficulty;

        // Store the movement cost
        _currentMoveCost = totalCost;

        // IMPORTANT: Update grid immediately
        // Remove from current cell first
        gridArea.RemoveEntity(_currentGridPos);

        // Update current grid position
        _currentGridPos = targetGridPos;

        // Add to new cell
        gridArea.AddEntity(_currentGridPos, _owner);

        // Start moving visually
        _startPosition = _owner.Position;
        _targetPosition = Utils.GridToWorld(_currentGridPos);
        _isMoving = true;

        // Update direction for animation
        _direction = (_targetPosition - _startPosition).Normalized();

        // Handle animation
        UpdateSpriteDirection();

        // Check if we can complete the move instantly
        if (_movementPointsAccumulator >= _currentMoveCost)
        {
            CompleteMove();
        }

        return true;
    }

    private void CompleteMove()
    {
        // Ensure exact position
        _owner.Position = _targetPosition;

        // Consume exactly the required points
        _movementPointsAccumulator -= _currentMoveCost;

        // End movement
        _isMoving = false;

        // Update animation
        UpdateSpriteDirection();
    }

    // Update sprite facing direction based on movement
    private void UpdateSpriteDirection()
    {
        var sprites = _owner.SpriteLayers;

        if (sprites.Count == 0)
        {
            var fallback = _owner.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (fallback != null)
            {
                FlipSprite(fallback);
            }

            return;
        }

        foreach (var sprite in sprites.Values)
        {
            FlipSprite(sprite);
        }
    }

    private void FlipSprite(Sprite2D sprite)
    {
        if (_direction.X > 0)
        {
            sprite.FlipH = false;
        }
        else if (_direction.X < 0)
        {
            sprite.FlipH = true;
        }
    }

    /// <summary>
    /// Directly set the grid position without animation or movement cost.
    /// Used for area transitions (teleporting between areas).
    /// </summary>
    public void SetGridPositionDirect(Vector2I newPos)
    {
        _currentGridPos = newPos;
        _targetPosition = Utils.GridToWorld(newPos);
        _startPosition = _targetPosition;
        _owner.Position = _targetPosition;
        _isMoving = false;
        _movementPointsAccumulator = 0;
    }

    public PathFinder GetPathfinder()
    {
        return MyPathfinder;
    }

    // Set the movement points per tick
    public void SetMovementPointsPerTick(float pointsPerTick)
    {
        _movementPointsPerTick = pointsPerTick;
    }

    // Get the current grid position
    public Vector2I GetCurrentGridPosition()
    {
        return _currentGridPos;
    }

    // Get the movement direction
    public Vector2 GetDirection()
    {
        return _direction;
    }

    // Set a new direction
    public void SetDirection(Vector2 newDirection)
    {
        _direction = newDirection;
        UpdateSpriteDirection();
    }

    // Check if entity is currently moving
    public bool IsMoving()
    {
        return _isMoving;
    }

    // Get facing direction for interaction
    public Vector2I GetFacingDirection()
    {
        Sprite2D? sprite = null;

        if (_owner.SpriteLayers.Count > 0)
        {
            // Get first sprite from dictionary
            foreach (var s in _owner.SpriteLayers.Values)
            {
                sprite = s;
                break;
            }
        }

        sprite ??= _owner.GetNodeOrNull<Sprite2D>("Sprite2D");

        if (sprite?.FlipH == true)
        {
            return new Vector2I(-1, 0); // Facing left
        }

        return new Vector2I(1, 0); // Facing right (default)
    }
}
