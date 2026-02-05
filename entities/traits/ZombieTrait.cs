using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

public class ZombieTrait : UndeadBehaviorTrait
{
    // Zombie-specific properties
    private enum ZombieState
    {
        Idle,
        Wandering
    }

    private ZombieState _currentState = ZombieState.Idle;

    private bool _hasGroaned;

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Home graveyard is managed by HomeTrait, not directly on ZombieTrait.
    /// </summary>
    /// <remarks>
    /// If no home graveyard is provided via HomeTrait, the zombie will still function but won't have access
    /// to graveyard storage for feeding. This is acceptable for zombies spawned in the wild.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // Home graveyard is managed by HomeTrait, not ZombieTrait
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        // Home graveyard is managed by HomeTrait, not ZombieTrait
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue)
    {
        base.Initialize(owner, health, initQueue);

        if (owner?.Health == null)
        {
            return;
        }

        // NOTE: The hunger need and ItemConsumptionBehaviorTrait are now defined in JSON
        // (mindless_zombie.json) rather than programmatically added here.
        // This follows the ECS architecture where trait composition is data-driven.
        // ZombieLivingTrait handles registering the brain hunger need.

        // Zombie-specific initialization
        WanderProbability = 0.3f; // Zombies wander more often
        WanderRange = 15.0f;      // And further from spawn

        Log.Print($"{owner.Name}: Zombie trait initialized");
    }

    protected override EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Process current state
        switch (_currentState)
        {
            case ZombieState.Idle:
                return ProcessIdleState(currentPerception);
            case ZombieState.Wandering:
                return ProcessWanderingState(currentPerception);
            default:
                return new IdleAction(_owner, this);
        }
    }

    private EntityAction? ProcessIdleState(Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_stateTimer == 0)
        {
            // Chance to start wandering
            if (_rng.Randf() < WanderProbability)
            {
                _currentState = ZombieState.Wandering;
                _stateTimer = (uint)_rng.RandiRange(60, 180);

                var wanderAction = TryToWander(perception);

                // Occasionally add groan sound to the wander action
                if (wanderAction != null && !_hasGroaned && _rng.Randf() < 0.3f)
                {
                    wanderAction.SoundEffect = "groan";
                    _hasGroaned = true;
                }

                return wanderAction;
            }
            else
            {
                // Reset idle timer
                _stateTimer = IdleTime;
            }
        }

        return new IdleAction(_owner, this);
    }

    private EntityAction? ProcessWanderingState(Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if we're currently executing a GoToLocationActivity (returning to spawn)
        if (_owner.GetCurrentActivity() is GoToLocationActivity)
        {
            // Let the activity handle navigation
            return null;
        }

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
                return TryToWander(perception);
            }
        }

        // Check if we've wandered too far from spawn
        if (IsOutsideWanderRange())
        {
            // Return to spawn area using GoToLocationActivity
            var returnActivity = new GoToLocationActivity(_spawnPosition, priority: 1);
            return new StartActivityAction(_owner, this, returnActivity, priority: 1);
        }

        return new IdleAction(_owner, this);
    }
}
