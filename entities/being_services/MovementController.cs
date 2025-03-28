using Godot;
using System;
using VeilOfAges.Core.Lib;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.BeingServices
{
    public class MovementController
    {
        private Being _owner;
        private AnimatedSprite2D? _animatedSprite;

        // Movement state
        private Vector2 _targetPosition;
        private Vector2 _startPosition;
        private Vector2 _direction = Vector2.Zero;
        private Vector2I _currentGridPos;

        // Movement points system
        private float _movementPointsPerTick; // Points gained per tick
        private float _movementPointsAccumulator = 0f; // Accumulated movement points

        // Visual movement tracking
        private bool _isMoving = false;
        private float _currentMoveCost = 0f; // Total cost of current move

        public PathFinder MyPathfinder { get; set; } = new PathFinder();

        public MovementController(Being owner, float movementPointsPerTick = 0.3f)
        {
            _owner = owner;
            _movementPointsPerTick = movementPointsPerTick;

            // Get the animated sprite reference from the owner
            _animatedSprite = _owner.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
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
                if (_movementPointsAccumulator >= _currentMoveCost) CompleteMove();
            }
        }

        // Try to move to a specific grid position
        public bool TryMoveToGridPosition(Vector2I targetGridPos)
        {
            // Already moving, can't start another move
            if (_isMoving)
                return false;

            // Check we are only moving one distance at a time
            float distanceSquared = _currentGridPos.DistanceSquaredTo(targetGridPos);

            // Determine movement cost based on distance
            float movementCost;
            if (distanceSquared <= 1) // Cardinal direction (up, down, left, right)
                movementCost = 1.0f;
            else if (distanceSquared <= 2) // Diagonal movement
                movementCost = 1.414f; // √2 for diagonal
            else
                return false; // We are trying to move too far in one step

            // Get grid area and check if target cell is walkable
            var gridArea = _owner.GridArea;
            if (gridArea == null || !gridArea.IsCellWalkable(targetGridPos))
                return false;

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
            UpdateAnimation();

            // Check if we can complete the move instantly
            if (_movementPointsAccumulator >= _currentMoveCost) CompleteMove();
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
            UpdateAnimation();
        }

        // Update the animation based on movement state
        private void UpdateAnimation()
        {
            if (_animatedSprite != null)
            {
                if (_direction.X > 0)
                    _animatedSprite.FlipH = false;
                else if (_direction.X < 0)
                    _animatedSprite.FlipH = true;

                if (_isMoving)
                    _animatedSprite.CallDeferred("play", "walk");
                else
                    _animatedSprite.CallDeferred("play", "idle");
            }
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
            UpdateAnimation();
        }

        // Check if entity is currently moving
        public bool IsMoving()
        {
            return _isMoving;
        }

        // Get facing direction for interaction
        public Vector2I GetFacingDirection()
        {
            // Determine facing direction based on sprite orientation
            if (_animatedSprite?.FlipH == true)
                return new Vector2I(-1, 0); // Facing left
            else
                return new Vector2I(1, 0);  // Facing right
        }
    }
}
