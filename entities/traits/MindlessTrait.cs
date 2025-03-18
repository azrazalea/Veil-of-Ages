using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class MindlessTrait : ITrait
    {
        protected Being _owner;
        private RandomNumberGenerator _rng = new();

        // Mindless behavior properties
        public float WanderProbability { get; set; } = 0.2f;
        public float WanderRange { get; set; } = 10f;
        public uint IdleTime { get; set; } = 10;

        private uint _stateTimer = 0;
        private Vector2I _spawnGridPos;
        private enum MindlessState { Idle, Wandering }
        private MindlessState _currentState = MindlessState.Idle;
        private Vector2 CachedOwnerPosition;

        public virtual void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;
            _rng.Randomize();
            _stateTimer = IdleTime;

            // Store spawn position as anchor point
            _spawnGridPos = _owner.GetCurrentGridPosition();

            _currentState = MindlessState.Idle;
            GD.Print($"{_owner.Name}: Mindless trait initialized");
        }

        public virtual void Process(double delta)
        {

        }
        public EntityAction SuggestAction(Vector2 currentOwnerPosition, Perception currentPerception)
        {
            // Only process AI if movement is complete
            if (_owner.IsMoving())
                return null;

            CachedOwnerPosition = currentOwnerPosition;

            if (_stateTimer > 0)
            {
                _stateTimer--;
            }

            return _currentState switch
            {
                MindlessState.Idle => ProcessIdleState(),
                MindlessState.Wandering => ProcessWanderingState(),
                _ => new IdleAction(_owner),
            };
        }

        private EntityAction ProcessIdleState()
        {
            if (_stateTimer == 0)
            {
                // Chance to start wandering
                if (_rng.Randf() < WanderProbability)
                {
                    _currentState = MindlessState.Wandering;
                    _stateTimer = (uint)_rng.RandiRange(120, 300);
                    return TryToWander();
                }
                else
                {
                    // Reset idle timer
                    _stateTimer = IdleTime;
                }
            }

            return new IdleAction(_owner);
        }

        private EntityAction ProcessWanderingState()
        {
            if (_stateTimer == 0)
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
                    _stateTimer = (uint)_rng.RandiRange(60, 180);
                    return TryToWander();
                }
            }

            return new IdleAction(_owner);
        }

        private MoveAction TryToWander()
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
                Vector2 towardSpawn = (_owner.GetGridSystem().GridToWorld(_spawnGridPos) - CachedOwnerPosition).Normalized();

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
            return new MoveAction(_owner, targetGridPos, 5);
        }

        public virtual void OnEvent(string eventName, params object[] args)
        {
        }
    }
}
