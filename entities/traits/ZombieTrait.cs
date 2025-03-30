using System.Linq;
using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Needs.Strategies;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class ZombieTrait : UndeadBehaviorTrait
    {
        // Zombie-specific properties
        private enum ZombieState { Idle, Wandering }
        private ZombieState _currentState = ZombieState.Idle;

        private bool _hasGroaned = false;

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            if (owner?.Health == null) return;

            // Initialize zombie-specific hunger need directly in this trait
            var needsSystem = owner.NeedsSystem;
            if (needsSystem != null)
            {
                // Zombies get hungrier much slower than living beings
                var brainHunger = new Need("hunger", "Brain Hunger", 60f, 0.0015f, 15f, 40f, 90f);
                needsSystem.AddNeed(brainHunger);
            }

            // Add ConsumptionBehaviorTrait for brain hunger
            var consumptionTrait = new ConsumptionBehaviorTrait(
                "hunger",
                new GraveyardSourceIdentifier(),
                new GraveyardAcquisitionStrategy(),
                new ZombieConsumptionEffect(),
                new ZombieCriticalHungerHandler(),
                365  // Zombies take longer to feed as they're messier eaters
            );

            consumptionTrait.Initialize(owner, owner.Health);
            // Add the consumption trait with a priority just above this trait
            owner.selfAsEntity().AddTrait(consumptionTrait, this.Priority - 1);

            // Zombie-specific initialization
            WanderProbability = 0.3f; // Zombies wander more often
            WanderRange = 15.0f;      // And further from spawn

            GD.Print($"{owner.Name}: Zombie trait initialized with brain hunger");
        }

        protected override EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;

            // Process current state
            switch (_currentState)
            {
                case ZombieState.Idle:
                    return ProcessIdleState();
                case ZombieState.Wandering:
                    return ProcessWanderingState();
                default:
                    return new IdleAction(_owner, this);
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

                    // Occasionally make zombie sounds
                    if (!_hasGroaned && _rng.Randf() < 0.3f)
                    {
                        GD.Print($"{_owner.Name}: *groans*");
                        PlayZombieGroan();
                        _hasGroaned = true;
                    }

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
                    _hasGroaned = false;
                }
                else
                {
                    _stateTimer = (uint)_rng.RandiRange(60, 180);
                    return TryToWander();
                }
            }

            // Check if we've wandered too far from spawn
            if (IsOutsideWanderRange())
            {
                // Return to spawn area
                MyPathfinder.SetPositionGoal(_owner, _spawnPosition);
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
            }

            return new IdleAction(_owner, this);
        }

        private void PlayZombieGroan()
        {
            // Access the AudioStreamPlayer2D component
            (_owner as MindlessZombie)?.CallDeferred("PlayZombieGroan");
        }
    }
}
