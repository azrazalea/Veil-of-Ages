using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

public class SkeletonTrait : UndeadBehaviorTrait
{
    // Skeleton-specific properties
    public float TerritoryRange { get; set; } = 12.0f;
    public float DetectionRange { get; set; } = 8.0f;
    public uint IntimidationTime { get; set; } = 40;

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters (all optional):
    /// - "TerritoryRange" (float): How far from spawn position the skeleton considers its territory (default: 12.0)
    /// - "DetectionRange" (float): How far the skeleton can detect intruders (default: 8.0)
    /// - "IntimidationTime" (int): Ticks the skeleton remains alert after detecting an intruder (default: 40).
    /// </summary>
    /// <remarks>
    /// All parameters have sensible defaults, so no configuration is strictly required.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // All parameters are optional with sensible defaults
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        var territory = config.GetFloat("TerritoryRange");
        if (territory.HasValue)
        {
            TerritoryRange = territory.Value;
        }

        var detection = config.GetFloat("DetectionRange");
        if (detection.HasValue)
        {
            DetectionRange = detection.Value;
        }

        var intimidation = config.GetInt("IntimidationTime");
        if (intimidation.HasValue)
        {
            IntimidationTime = (uint)intimidation.Value;
        }
    }

    private enum SkeletonState
    {
        Idle,
        Wandering,
        Defending
    }

    private SkeletonState _currentState = SkeletonState.Idle;

    private Being? _intruder;
    private uint _intimidationTimer;
    private bool _hasRattled;
    private Vector2I? _lastIntruderPosition;

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue)
    {
        base.Initialize(owner, health, initQueue);

        // Skeleton-specific initialization
        WanderProbability = 0.2f; // Skeletons wander less than zombies
        WanderRange = 10.0f;      // And stay closer to their area
        IdleTime = 15;            // Stand idle longer

        Log.Print($"{owner.Name}: Skeleton behavior initialized");
    }

    protected override EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Update intimidation timer
        if (_intimidationTimer > 0)
        {
            _intimidationTimer--;
        }

        // Check for intruders in territory if not already defending
        if (_currentState != SkeletonState.Defending)
        {
            CheckForIntruders(currentPerception);
        }

        // Process current state
        switch (_currentState)
        {
            case SkeletonState.Idle:
                return ProcessIdleState(currentPerception);
            case SkeletonState.Wandering:
                return ProcessWanderingState(currentPerception);
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

        foreach (var (entity, _) in entities)
        {
            // Skip if it's an undead entity
            if (entity.SelfAsEntity().HasTrait<UndeadTrait>())
            {
                continue;
            }

            // Check if they're in our territory
            Vector2I entityPos = entity.GetCurrentGridPosition();
            Vector2I relativePos = entityPos - _spawnPosition;

            if (Mathf.Abs(relativePos.X) <= TerritoryRange &&
                Mathf.Abs(relativePos.Y) <= TerritoryRange)
            {
                // Intruder detected!
                _intruder = entity;
                _lastIntruderPosition = entityPos;
                _currentState = SkeletonState.Defending;
                _intimidationTimer = IntimidationTime;

                Log.Print($"{_owner?.Name}: Intruder detected in territory!");
                return;
            }
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
                _currentState = SkeletonState.Wandering;
                _stateTimer = (uint)_rng.RandiRange(60, 180);
                return TryToWander(perception);
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
                return TryToWander(perception);
            }
        }

        return new IdleAction(_owner, this);
    }

    private EntityAction? ProcessDefendingState(Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if we've lost interest or the intruder left the territory
        if (_intimidationTimer == 0 || !IsIntruderInTerritory())
        {
            Log.Print($"{_owner.Name}: Intruder gone, returning to patrol");
            _currentState = SkeletonState.Wandering;
            _stateTimer = (uint)_rng.RandiRange(60, 180);
            _intruder = null;
            _lastIntruderPosition = null;
            _hasRattled = false;

            // Return to spawn area if we strayed too far
            if (IsOutsideWanderRange())
            {
                return StartReturnToSpawn();
            }

            return new IdleAction(_owner, this);
        }

        bool intruderVisible = false;
        Vector2I? currentIntruderPosition = null;

        // Try to find the intruder in perception
        foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
        {
            if (entity == _intruder)
            {
                intruderVisible = true;
                currentIntruderPosition = position;
                _lastIntruderPosition = position;

                // Reset intimidation timer since we can still see the intruder
                _intimidationTimer = IntimidationTime;
                break;
            }
        }

        // Determine target position for pursuit
        Vector2I? targetPosition = intruderVisible ? currentIntruderPosition : _lastIntruderPosition;

        if (targetPosition.HasValue)
        {
            var action = ContinuePursuit(targetPosition.Value);

            // Add rattle sound on first defend action
            if (action != null && !_hasRattled)
            {
                action.SoundEffect = "ambient";
                _hasRattled = true;
            }

            return action;
        }

        // If we have no target info, just idle
        return new IdleAction(_owner, this);
    }

    /// <summary>
    /// Start returning to spawn position using GoToLocationActivity.
    /// </summary>
    private EntityAction? StartReturnToSpawn()
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if already navigating back to spawn
        if (_owner.GetCurrentActivity() is GoToLocationActivity)
        {
            return null; // Let the activity handle navigation
        }

        // Start a new return activity
        var returnActivity = new GoToLocationActivity(_spawnPosition, priority: -1);
        return new StartActivityAction(_owner, this, returnActivity, priority: -1);
    }

    /// <summary>
    /// Start pursuit of a target position using GoToLocationActivity.
    /// Uses BDI pattern: goes to believed position, not tracking entity directly.
    /// </summary>
    private EntityAction? ContinuePursuit(Vector2I targetPosition)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if already pursuing via an activity
        if (_owner.GetCurrentActivity() is GoToLocationActivity)
        {
            return null; // Let the activity handle navigation
        }

        // Start a new pursuit activity to the target position
        var pursueActivity = new GoToLocationActivity(targetPosition, priority: -2);
        return new StartActivityAction(_owner, this, pursueActivity, priority: -2);
    }

    private bool IsIntruderInTerritory()
    {
        if (_intruder == null)
        {
            return false;
        }

        // Check if intruder is still in our territory
        Vector2I intruderPos = _intruder.GetCurrentGridPosition();
        Vector2I relativePos = intruderPos - _spawnPosition;

        return Mathf.Abs(relativePos.X) <= TerritoryRange &&
               Mathf.Abs(relativePos.Y) <= TerritoryRange;
    }
}
