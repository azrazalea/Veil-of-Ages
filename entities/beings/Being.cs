using Godot;
using System;
using System.Collections.Generic;

namespace NecromancerKingdom.Entities
{
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

        // Trait system
        protected List<ITrait> _traits = new List<ITrait>();

        public override void _Ready()
        {
            _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            _animatedSprite.Play("idle");
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

            // Initialize all traits
            foreach (var trait in _traits)
            {
                trait.Initialize(this);
            }
        }

        // Trait management
        public void AddTrait<T>() where T : ITrait, new()
        {
            var trait = new T();
            _traits.Add(trait);

            // If we're already initialized, initialize the trait immediately
            if (_gridSystem != null)
            {
                trait.Initialize(this);
            }
        }

        public void AddTrait(ITrait trait)
        {
            _traits.Add(trait);

            // If we're already initialized, initialize the trait immediately
            if (_gridSystem != null)
            {
                trait.Initialize(this);
            }
        }

        public T GetTrait<T>() where T : ITrait
        {
            foreach (var trait in _traits)
            {
                if (trait is T typedTrait)
                {
                    return typedTrait;
                }
            }

            return default;
        }

        public bool HasTrait<T>() where T : ITrait
        {
            foreach (var trait in _traits)
            {
                if (trait is T)
                {
                    return true;
                }
            }

            return false;
        }

        // Event system for traits
        public void OnTraitEvent(string eventName, params object[] args)
        {
            foreach (var trait in _traits)
            {
                trait.OnEvent(eventName, args);
            }
        }

        // Move to a specific grid position if possible
        public bool TryMoveToGridPosition(Vector2I targetGridPos)
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
        public void SetDirection(Vector2 newDirection)
        {
            _direction = newDirection;
        }

        // Get the current grid position
        public Vector2I GetCurrentGridPosition()
        {
            return _currentGridPos;
        }

        // Check if the being is currently moving
        public bool IsMoving()
        {
            return _moveProgress < 1.0f;
        }

        // Get the grid system (for traits that need it)
        public GridSystem GetGridSystem()
        {
            return _gridSystem;
        }

        // Process traits in the physics update
        public override void _PhysicsProcess(double delta)
        {
            // Process all traits
            foreach (var trait in _traits)
            {
                trait.Process(delta);
            }

            // Update movement
            UpdateMovement(delta);
        }
    }
}
