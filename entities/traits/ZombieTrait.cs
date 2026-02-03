using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
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

    // Home graveyard for this zombie
    private Building? _homeGraveyard;
    public Building? HomeGraveyard => _homeGraveyard;

    private bool _hasGroaned;

    /// <summary>
    /// Set the home graveyard for this zombie.
    /// Called by spawning systems when creating zombies.
    /// </summary>
    public void SetHomeGraveyard(Building graveyard)
    {
        _homeGraveyard = graveyard;
        Log.Print($"{_owner?.Name}: Home graveyard set to {graveyard.BuildingName}");
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "homeGraveyard" or "home" (Building): The graveyard building this zombie is associated with (optional).
    /// </summary>
    /// <remarks>
    /// If no home graveyard is provided, the zombie will still function but won't have access
    /// to graveyard storage for feeding. This is acceptable for zombies spawned in the wild.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // Home graveyard is optional - zombies can function without it
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        var graveyard = config.GetBuilding("homeGraveyard") ?? config.GetBuilding("home");
        if (graveyard != null)
        {
            SetHomeGraveyard(graveyard);
        }
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue)
    {
        base.Initialize(owner, health, initQueue);

        if (owner?.Health == null)
        {
            return;
        }

        // Initialize zombie-specific hunger need directly in this trait
        var needsSystem = owner.NeedsSystem;
        if (needsSystem != null)
        {
            // Zombies get hungrier much slower than living beings
            var brainHunger = new Need("hunger", "Brain Hunger", 60f, 0.0015f, 15f, 40f, 90f);
            needsSystem.AddNeed(brainHunger);
        }

        // Add ItemConsumptionBehaviorTrait for brain hunger
        // Uses item system: checks inventory then graveyard storage for "zombie_food" tagged items (corpses)
        var consumptionTrait = new ItemConsumptionBehaviorTrait(
            needId: "hunger",
            foodTag: "zombie_food",
            getHome: () => _homeGraveyard,
            restoreAmount: 70f,  // Zombies get more from feeding
            consumptionDuration: 365);  // Zombies take longer to feed as they're messier eaters

        // Add the consumption trait with a priority just above this trait
        owner.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);

        // Zombie-specific initialization
        WanderProbability = 0.3f; // Zombies wander more often
        WanderRange = 15.0f;      // And further from spawn

        Log.Print($"{owner.Name}: Zombie trait initialized with brain hunger");
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
                return ProcessIdleState();
            case ZombieState.Wandering:
                return ProcessWanderingState();
            default:
                return new IdleAction(_owner, this);
        }
    }

    private EntityAction? ProcessIdleState()
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

                // Occasionally make zombie sounds
                if (!_hasGroaned && _rng.Randf() < 0.3f)
                {
                    Log.Print($"{_owner.Name}: *groans*");
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
                return TryToWander();
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

    private void PlayZombieGroan()
    {
        // Play the ambient sound (zombie groan) via GenericBeing's audio system
        (_owner as GenericBeing)?.CallDeferred("PlaySound", "ambient");
    }
}
