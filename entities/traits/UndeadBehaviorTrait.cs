using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public abstract class UndeadBehaviorTrait : UndeadTrait
    {
        // Common properties for all undead behaviors
        public float WanderProbability { get; set; } = 0.2f;
        public float WanderRange { get; set; } = 10.0f;
        public uint IdleTime { get; set; } = 10;

        protected Vector2I _spawnPosition;
        protected RandomNumberGenerator _rng = new();
        protected uint _stateTimer = 0;

        // Override this to implement different behavior states
        protected abstract EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception);

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);
            _rng.Randomize();
            _spawnPosition = owner.GetCurrentGridPosition();
            _stateTimer = IdleTime;
            GD.Print($"{owner.Name}: UndeadBehavior trait initialized");
        }

        public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            // Decrement the timer
            if (_stateTimer > 0)
                _stateTimer--;

            // Let derived classes handle their specific behaviors
            return ProcessState(currentOwnerGridPosition, currentPerception);
        }

        // Common method for wandering behavior used by all undead
        protected EntityAction? TryToWander()
        {
            if (_owner == null) return null;
            // Pick a random direction
            int randomDir = _rng.RandiRange(0, 7);
            Vector2I newDirection = Vector2I.Zero;

            switch (randomDir)
            {
                case 0: newDirection = Vector2I.Right; break;
                case 1: newDirection = Vector2I.Left; break;
                case 2: newDirection = Vector2I.Down; break;
                case 3: newDirection = Vector2I.Up; break;
                case 4: newDirection = Vector2I.Left + Vector2I.Up; break;
                case 5: newDirection = Vector2I.Left + Vector2I.Down; break;
                case 6: newDirection = Vector2I.Right + Vector2I.Up; break;
                case 7: newDirection = Vector2I.Right + Vector2I.Down; break;

            }

            // Calculate target grid position
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I targetGridPos = currentPos + new Vector2I(
                newDirection.X,
                newDirection.Y
            );

            // Check if the target position is within wander range
            var distanceFromSpawn = targetGridPos.DistanceSquaredTo(_spawnPosition);
            if (distanceFromSpawn > WanderRange * WanderRange)
            {
                // Too far from spawn, try to move back toward spawn
                Vector2 towardSpawn = (_spawnPosition - currentPos).Sign();

                // Recalculate target position
                targetGridPos = currentPos + new Vector2I(
                    (int)towardSpawn.X,
                    (int)towardSpawn.Y
                );
            }

            // Try to move to the target position
            return new MoveAction(_owner, this, targetGridPos, 5);
        }

        // Check if current position is outside wander range
        protected bool IsOutsideWanderRange()
        {
            if (_owner == null) return true;

            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I distanceFromSpawn = currentPos - _spawnPosition;

            return Mathf.Abs(distanceFromSpawn.X) > WanderRange ||
                   Mathf.Abs(distanceFromSpawn.Y) > WanderRange;
        }
    }
}
