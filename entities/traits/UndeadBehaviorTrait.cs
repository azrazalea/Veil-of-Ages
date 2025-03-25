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
        protected List<Vector2I> _currentPath = new();
        protected int _currentPathIndex = 0;

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
            // Only process AI if movement is complete
            if (_owner?.IsMoving() != false)
                return null;

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
            Vector2I distanceFromSpawn = targetGridPos - _spawnPosition;

            if (Mathf.Abs(distanceFromSpawn.X) > WanderRange ||
                Mathf.Abs(distanceFromSpawn.Y) > WanderRange)
            {
                // Too far from spawn, try to move back toward spawn
                Vector2 towardSpawn = (Grid.Utils.GridToWorld(_spawnPosition) - Grid.Utils.GridToWorld(currentPos)).Normalized();

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
            return new MoveAction(_owner, this, targetGridPos, 5);
        }

        // Common method for following a path
        protected EntityAction? MoveToNextPathPosition(int priority = 5)
        {
            if (_owner == null) return null;

            if (_currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
            {
                return new IdleAction(_owner, this);
            }

            Vector2I nextPos = _currentPath[_currentPathIndex];
            _currentPathIndex++;

            // Check if the next position is walkable
            if (_owner.GetGridArea()?.IsCellWalkable(nextPos) == true)
            {
                return new MoveAction(_owner, this, nextPos, priority);
            }
            else
            {
                // If obstacle encountered, just stop
                return new IdleAction(_owner, this);
            }
        }

        // Common method for finding a path
        protected List<Vector2I> FindPathTo(Vector2I target)
        {
            var gridArea = _owner?.GetGridArea();
            if (gridArea == null || _owner == null) return [];

            Vector2I currentPos = _owner.GetCurrentGridPosition();
            return PathFinder.FindPath(gridArea, currentPos, target);
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
