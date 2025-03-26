using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class ZombieTrait : UndeadBehaviorTrait
    {
        // Zombie-specific properties
        public float DetectionRange { get; set; } = 8.0f;
        public float ChaseRange { get; set; } = 15.0f;
        public uint LoseInterestTime { get; set; } = 30;

        private enum ZombieState { Idle, Wandering, Chasing }
        private ZombieState _currentState = ZombieState.Idle;

        private Being? _chaseTarget;
        private uint _chaseTimer = 0;
        private bool _hasGroaned = false;

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            // Zombie-specific initialization
            WanderProbability = 0.3f; // Zombies wander more often
            WanderRange = 15.0f;      // And further from spawn

            GD.Print($"{_owner?.Name}: Zombie behavior initialized");
        }

        protected override EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;

            // Update chase timer
            if (_chaseTimer > 0)
                _chaseTimer--;

            // Check for nearby living beings to chase if not already chasing
            if (_currentState != ZombieState.Chasing)
            {
                CheckForLivingToChase(currentPerception);
            }

            // Process current state
            switch (_currentState)
            {
                case ZombieState.Idle:
                    return ProcessIdleState();
                case ZombieState.Wandering:
                    return ProcessWanderingState();
                case ZombieState.Chasing:
                    return ProcessChasingState(currentPerception);
                default:
                    return new IdleAction(_owner, this);
            }
        }

        private void CheckForLivingToChase(Perception currentPerception)
        {
            if (MyPathfinder == null) return;

            // Look for living beings (not undead)
            var entities = currentPerception.GetEntitiesOfType<Being>();
            foreach (var (entity, position) in entities)
            {
                // Skip if it's an undead entity
                if (entity.selfAsEntity().HasTrait<UndeadTrait>())
                    continue;

                // If it's a living entity, start chasing!
                _chaseTarget = entity;
                _currentState = ZombieState.Chasing;
                _chaseTimer = LoseInterestTime;

                // Make a zombie groan when first seeing a living being
                if (!_hasGroaned)
                {
                    GD.Print($"{_owner?.Name}: *groans hungrily*");
                    // Play sound effect
                    PlayZombieGroan();
                    _hasGroaned = true;
                }

                if (_owner == null) return;

                // Calculate a path to the target
                MyPathfinder.SetPathTo(_owner, position);

                GD.Print($"{_owner?.Name}: Spotted living being, starting chase!");
                return;
            }
        }

        private EntityAction? ProcessIdleState()
        {
            if (_owner == null) return null;
            if (_stateTimer == 0)
            {
                // Chance to start wandering
                if (_rng.Randf() < WanderProbability)
                {
                    _currentState = ZombieState.Wandering;
                    _stateTimer = (uint)_rng.RandiRange(60, 180);
                    return TryToWander();
                }
                else
                {
                    // Reset idle timer
                    _stateTimer = IdleTime;
                }
            }

            return new IdleAction(_owner, this);
        }

        private EntityAction? ProcessWanderingState()
        {
            if (_owner == null) return null;

            if (_stateTimer == 0)
            {
                // Either continue wandering or return to idle
                if (_rng.Randf() < 0.3f)
                {
                    _currentState = ZombieState.Idle;
                    _stateTimer = IdleTime;
                    _owner.SetDirection(Vector2.Zero);
                }
                else
                {
                    _stateTimer = (uint)_rng.RandiRange(60, 180);
                    return TryToWander();
                }
            }

            return new IdleAction(_owner, this);
        }

        private EntityAction? ProcessChasingState(Perception currentPerception)
        {
            if (_owner == null || MyPathfinder == null) return null;

            // Check if we've lost interest or gone too far
            if (_chaseTimer == 0 || IsOutsideChaseRange())
            {
                GD.Print($"{_owner.Name}: Lost interest in chase, returning to wander area");
                _currentState = ZombieState.Wandering;
                _stateTimer = (uint)_rng.RandiRange(60, 180);
                _chaseTarget = null;
                _hasGroaned = false;

                // Return to spawn area
                MyPathfinder.SetPathTo(_owner, _spawnPosition);

                if (MyPathfinder.CurrentPath.Count > 0)
                {
                    return new MoveAlongPathAction(_owner, this, priority: 1);
                }
                return new IdleAction(_owner, this, 1);
            }

            // Check if target is still visible
            foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
            {
                if (entity == _chaseTarget)
                {
                    // Update path to target
                    MyPathfinder.SetPathTo(_owner, position);

                    // Reset chase timer since we can still see the target
                    _chaseTimer = LoseInterestTime;
                    break;
                }
            }

            // Follow the path to the target
            if (!MyPathfinder.IsPathComplete())
            {
                // Higher priority when chasing
                return new MoveAlongPathAction(_owner, this, priority: 1);
            }

            // If we have no path, just idle
            return new IdleAction(_owner, this);
        }

        private bool IsOutsideChaseRange()
        {
            if (_owner == null) return true;

            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I distanceFromSpawn = currentPos - _spawnPosition;

            return Mathf.Abs(distanceFromSpawn.X) > ChaseRange ||
                   Mathf.Abs(distanceFromSpawn.Y) > ChaseRange;
        }

        private void PlayZombieGroan()
        {
            // Access the AudioStreamPlayer2D component
            (_owner as MindlessZombie)?.CallDeferred("PlayZombieGroan");
        }
    }
}
