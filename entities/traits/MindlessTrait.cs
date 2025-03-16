using Godot;
using System;
using NecromancerKingdom.Entities;

namespace NecromancerKingdom.Entities.Traits
{
    public class MindlessTrait : ITrait
    {
        protected Being _owner;
        private RandomNumberGenerator _rng = new RandomNumberGenerator();

        // Mindless behavior properties
        public float WanderProbability { get; set; } = 0.2f;
        public float WanderRange { get; set; } = 10f;
        public float IdleTime { get; set; } = 2.0f;

        private float _stateTimer = 0f;
        private Vector2I _spawnGridPos;
        private enum MindlessState { Idle, Wandering }
        private MindlessState _currentState = MindlessState.Idle;

        public virtual void Initialize(Being owner)
        {
            _owner = owner;
            _rng.Randomize();
            _stateTimer = IdleTime;

            // Store spawn position as anchor point
            _spawnGridPos = _owner.GetCurrentGridPosition();

            GD.Print($"{_owner.Name}: Mindless trait initialized");
        }

        public virtual void Process(double delta)
        {
            // Only process AI if movement is complete
            if (_owner.IsMoving())
                return;

            // Update state timer
            _stateTimer -= (float)delta;

            switch (_currentState)
            {
                case MindlessState.Idle:
                    ProcessIdleState();
                    break;

                case MindlessState.Wandering:
                    ProcessWanderingState();
                    break;
            }
        }

        private void ProcessIdleState()
        {
            if (_stateTimer <= 0)
            {
                // Chance to start wandering
                if (_rng.Randf() < WanderProbability)
                {
                    _currentState = MindlessState.Wandering;
                    _stateTimer = _rng.RandfRange(2.0f, 5.0f);
                    TryToWander();
                }
                else
                {
                    // Reset idle timer
                    _stateTimer = IdleTime;
                }
            }
        }

        private void ProcessWanderingState()
        {
            if (_stateTimer <= 0)
            {
                // Either continue wandering or return to idle
                if (_rng.Randf() < 0.3f)
                {
                    _currentState = MindlessState.Idle;
                    _stateTimer = IdleTime;
                    _owner.SetDirection(Vector2.Zero);
                }
                else
                {
                    _stateTimer = _rng.RandfRange(1.0f, 3.0f);
                    TryToWander();
                }
            }
        }

        private void TryToWander()
        {
            // Pick a random direction
            int randomDir = _rng.RandiRange(0, 3);
            Vector2 newDirection = Vector2.Zero;

            switch (randomDir)
            {
                case 0: newDirection = Vector2.Right; break;
                case 1: newDirection = Vector2.Left; break;
                case 2: newDirection = Vector2.Down; break;
                case 3: newDirection = Vector2.Up; break;
            }

            _owner.SetDirection(newDirection);

            // Calculate target grid position
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I targetGridPos = currentPos + new Vector2I(
                (int)newDirection.X,
                (int)newDirection.Y
            );

            // Check if the target position is within wander range
            Vector2I distanceFromSpawn = targetGridPos - _spawnGridPos;

            if (Mathf.Abs(distanceFromSpawn.X) > WanderRange ||
                Mathf.Abs(distanceFromSpawn.Y) > WanderRange)
            {
                // Too far from spawn, try to move back toward spawn
                Vector2 towardSpawn = (_owner.GetGridSystem().GridToWorld(_spawnGridPos) - _owner.Position).Normalized();

                // Find the cardinal direction closest to the direction to spawn
                if (Mathf.Abs(towardSpawn.X) > Mathf.Abs(towardSpawn.Y))
                {
                    // Move horizontally
                    newDirection = new Vector2(Mathf.Sign(towardSpawn.X), 0);
                }
                else
                {
                    // Move vertically
                    newDirection = new Vector2(0, Mathf.Sign(towardSpawn.Y));
                }

                _owner.SetDirection(newDirection);

                // Recalculate target position
                targetGridPos = currentPos + new Vector2I(
                    (int)newDirection.X,
                    (int)newDirection.Y
                );
            }

            // Try to move to the target position
            if (!_owner.TryMoveToGridPosition(targetGridPos))
            {
                // Movement failed, return to idle
                _currentState = MindlessState.Idle;
                _stateTimer = IdleTime;
                _owner.SetDirection(Vector2.Zero);
            }
        }

        public virtual void OnEvent(string eventName, params object[] args)
        {
        }
    }
}
