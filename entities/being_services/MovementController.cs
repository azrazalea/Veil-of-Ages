using Godot;
using System;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
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

        // Movement timing
        private uint _baseMoveTicks = 4; // Default value, will be overridden by owner
        private uint _currentBaseMoveTicks;
        private uint _remainingMoveTicks = 0;
        private bool _isMoving = false;

        public PathFinder MyPathfinder { get; set; } = new PathFinder();

        public MovementController(Being owner, uint baseMoveTicks = 4)
        {
            _owner = owner;
            _baseMoveTicks = baseMoveTicks;
            _currentBaseMoveTicks = _baseMoveTicks;

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

        // Try to move to a specific grid position
        public bool TryMoveToGridPosition(Vector2I targetGridPos)
        {
            // Check we are only moving one distance at a time
            float distanceTo = _currentGridPos.DistanceSquaredTo(targetGridPos);
            if (distanceTo == 2)
            {
                distanceTo = 1.5f; // normalize cost to a distinct value 
            }
            else if (distanceTo > 2)
            {
                return false; // We are trying to move too far in one step
            }

            // Check if the target cell is free
            var gridArea = _owner.GridArea;
            if (gridArea != null && gridArea.IsCellWalkable(targetGridPos))
            {
                // Average terrain difficultly between source and destination cell
                var difficulty = (gridArea.GetTerrainDifficulty(targetGridPos) +
                                  gridArea.GetTerrainDifficulty(_currentGridPos)) / 2;

                // Free the current cell
                gridArea.RemoveEntity(_currentGridPos);

                // Update current grid position
                _currentGridPos = targetGridPos;

                // Mark new cell as occupied
                gridArea.AddEntity(_currentGridPos, _owner);

                // Start moving
                _startPosition = _owner.Position;
                _targetPosition = Utils.GridToWorld(_currentGridPos);
                // This should calculate via difficulty and account for higher cost for diagonal movement
                _remainingMoveTicks = (uint)float.Round(_baseMoveTicks * distanceTo * difficulty);
                _currentBaseMoveTicks = _remainingMoveTicks;
                _isMoving = true;

                // Update direction for animation
                _direction = (_targetPosition - _startPosition).Normalized();

                // Handle animation and facing direction
                UpdateAnimation();

                return true;
            }

            return false;
        }

        // Process movement progress for this tick
        public void ProcessMovementTick()
        {
            if (_isMoving && _remainingMoveTicks > 0)
            {
                _remainingMoveTicks--;

                // Calculate movement progress
                float progress = 1.0f - (_remainingMoveTicks / (float)_currentBaseMoveTicks);

                // Update position based on interpolation
                _owner.Position = _startPosition.Lerp(_targetPosition, progress);

                // Check if movement is complete
                if (_remainingMoveTicks <= 0)
                {
                    _owner.Position = _targetPosition; // Ensure exact position
                    _isMoving = false;
                    _currentBaseMoveTicks = _baseMoveTicks;

                    // If no direction, play idle animation
                    if (_direction == Vector2.Zero)
                    {
                        UpdateAnimation();
                    }
                }
            }
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

        public bool MoveAlongPath()
        {
            return MyPathfinder.MoveAlongPath(_owner);
        }

        public PathFinder GetPathfinder()
        {
            return MyPathfinder;
        }

        // Set the base movement speed
        public void SetBaseMoveTicks(uint ticks)
        {
            _baseMoveTicks = ticks;
            if (!_isMoving)
                _currentBaseMoveTicks = _baseMoveTicks;
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
