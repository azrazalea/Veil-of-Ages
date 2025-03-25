using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class SkeletonTrait : UndeadBehaviorTrait
    {
        // Skeleton-specific properties
        public float TerritoryRange { get; set; } = 12.0f;
        public float DetectionRange { get; set; } = 8.0f;
        public uint IntimidationTime { get; set; } = 40;

        private enum SkeletonState { Idle, Wandering, Defending }
        private SkeletonState _currentState = SkeletonState.Idle;

        private Being? _intruder;
        private uint _intimidationTimer = 0;
        private bool _hasRattled = false;

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            // Skeleton-specific initialization
            WanderProbability = 0.2f; // Skeletons wander less than zombies
            WanderRange = 10.0f;      // And stay closer to their area
            IdleTime = 15;            // Stand idle longer

            GD.Print($"{owner.Name}: Skeleton behavior initialized");
        }

        protected override EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;

            // Update intimidation timer
            if (_intimidationTimer > 0)
                _intimidationTimer--;

            // Check for intruders in territory if not already defending
            if (_currentState != SkeletonState.Defending)
            {
                CheckForIntruders(currentPerception);
            }

            // Process current state
            switch (_currentState)
            {
                case SkeletonState.Idle:
                    return ProcessIdleState();
                case SkeletonState.Wandering:
                    return ProcessWanderingState();
                case SkeletonState.Defending:
                    return ProcessDefendingState(currentPerception);
                default:
                    return new IdleAction(_owner, this);
            }
        }

        private void CheckForIntruders(Perception currentPerception)
        {
            // Look for living beings (not undead) in territory
            var entities = currentPerception.GetEntitiesOfType<Being>();

            foreach (var (entity, position) in entities)
            {
                // Skip if it's an undead entity
                if (entity.selfAsEntity().HasTrait<UndeadTrait>())
                    continue;

                // Check if they're in our territory
                Vector2I entityPos = entity.GetCurrentGridPosition();
                Vector2I relativePos = entityPos - _spawnPosition;

                if (Mathf.Abs(relativePos.X) <= TerritoryRange &&
                    Mathf.Abs(relativePos.Y) <= TerritoryRange)
                {
                    // Intruder detected!
                    _intruder = entity;
                    _currentState = SkeletonState.Defending;
                    _intimidationTimer = IntimidationTime;

                    // Make a skeleton rattle when first seeing an intruder
                    if (!_hasRattled)
                    {
                        GD.Print($"{_owner?.Name}: *bones rattle menacingly*");
                        // Play sound effect
                        PlayBoneRattle();
                        _hasRattled = true;
                    }

                    // Calculate a path to the intruder
                    _currentPath = FindPathTo(position);
                    _currentPathIndex = 0;

                    GD.Print($"{_owner?.Name}: Intruder detected in territory!");
                    return;
                }
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
                    _currentState = SkeletonState.Wandering;
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
                    _currentState = SkeletonState.Idle;
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

        private EntityAction? ProcessDefendingState(Perception currentPerception)
        {
            if (_owner == null) return null;

            // Check if we've lost interest or the intruder left the territory
            if (_intimidationTimer == 0 || !IsIntruderInTerritory())
            {
                GD.Print($"{_owner.Name}: Intruder gone, returning to patrol");
                _currentState = SkeletonState.Wandering;
                _stateTimer = (uint)_rng.RandiRange(60, 180);
                _intruder = null;
                _hasRattled = false;

                // Return to spawn area if we strayed too far
                if (IsOutsideWanderRange())
                {
                    _currentPath = FindPathTo(_spawnPosition);
                    _currentPathIndex = 0;

                    if (_currentPath.Count > 0)
                    {
                        return MoveToNextPathPosition();
                    }
                }

                return new IdleAction(_owner, this);
            }

            // Check if intruder is still visible
            // bool intruderVisible = false;
            foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
            {
                if (entity == _intruder)
                {
                    // intruderVisible = true;

                    // Update path to intruder
                    _currentPath = FindPathTo(position);
                    _currentPathIndex = 0;

                    // Reset intimidation timer since we can still see the intruder
                    _intimidationTimer = IntimidationTime;
                    break;
                }
            }

            // Follow the path to the intruder
            if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                // Higher priority when defending
                return MoveToNextPathPosition(10);
            }

            // If we have no path, just idle
            return new IdleAction(_owner, this);
        }

        private bool IsIntruderInTerritory()
        {
            if (_intruder == null)
                return false;

            // Check if intruder is still in our territory
            Vector2I intruderPos = _intruder.GetCurrentGridPosition();
            Vector2I relativePos = intruderPos - _spawnPosition;

            return Mathf.Abs(relativePos.X) <= TerritoryRange &&
                   Mathf.Abs(relativePos.Y) <= TerritoryRange;
        }

        private void PlayBoneRattle()
        {
            // Access the AudioStreamPlayer2D component
            (_owner as MindlessSkeleton)?.CallDeferred("PlayBoneRattle");
        }
    }
}
