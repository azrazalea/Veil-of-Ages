using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

public class SkeletonTrait : UndeadBehaviorTrait
{
    // Skeleton-specific properties
    public float TerritoryRange { get; set; } = 12.0f;
    public float DetectionRange { get; set; } = 8.0f;
    public uint IntimidationTime { get; set; } = 40;

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

    // Activities for position-based navigation
    private GoToLocationActivity? _returnToSpawnActivity;
    private GoToLocationActivity? _pursueActivity;

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

                // Make a skeleton rattle when first seeing an intruder
                if (!_hasRattled)
                {
                    Log.Print($"{_owner?.Name}: *bones rattle menacingly*");

                    // Play sound effect
                    PlayBoneRattle();
                    _hasRattled = true;
                }

                Log.Print($"{_owner?.Name}: Intruder detected in territory!");
                return;
            }
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
                return TryToWander();
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
            _pursueActivity = null;

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
            return ContinuePursuit(targetPosition.Value);
        }

        // If we have no target info, just idle
        return new IdleAction(_owner, this);
    }

    /// <summary>
    /// Start or continue returning to spawn position using GoToLocationActivity.
    /// </summary>
    private EntityAction? StartReturnToSpawn()
    {
        if (_owner == null)
        {
            return null;
        }

        // Start a new return activity if we don't have one
        if (_returnToSpawnActivity == null)
        {
            _returnToSpawnActivity = new GoToLocationActivity(_spawnPosition, priority: -1);
            _returnToSpawnActivity.Initialize(_owner);
        }

        // Check activity state
        if (_returnToSpawnActivity.State == Activity.ActivityState.Completed)
        {
            _returnToSpawnActivity = null;
            return new IdleAction(_owner, this);
        }

        if (_returnToSpawnActivity.State == Activity.ActivityState.Failed)
        {
            // Reset and try again next tick
            _returnToSpawnActivity = null;
            return new IdleAction(_owner, this);
        }

        // Continue the activity
        return _returnToSpawnActivity.GetNextAction(_owner.GetCurrentGridPosition(), new Perception());
    }

    /// <summary>
    /// Start or continue pursuit of a target position using GoToLocationActivity.
    /// Uses BDI pattern: goes to believed position, not tracking entity directly.
    /// </summary>
    private EntityAction? ContinuePursuit(Vector2I targetPosition)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if we need a new pursuit activity (target moved significantly or no activity)
        bool needNewActivity = _pursueActivity == null ||
                               _pursueActivity.State != Activity.ActivityState.Running;

        // Also restart if target position has changed significantly (entity moved)
        // This allows skeleton to re-path when intruder moves
        if (!needNewActivity && _pursueActivity != null)
        {
            // If we reached the believed position but intruder isn't there, get new activity
            if (_pursueActivity.State == Activity.ActivityState.Completed)
            {
                needNewActivity = true;
            }
        }

        if (needNewActivity)
        {
            _pursueActivity = new GoToLocationActivity(targetPosition, priority: -2);
            _pursueActivity.Initialize(_owner);
        }

        // Check activity state
        if (_pursueActivity!.State == Activity.ActivityState.Completed)
        {
            // Reached the position - if intruder still here, we'll get new position next tick
            _pursueActivity = null;
            return new IdleAction(_owner, this, priority: -2);
        }

        if (_pursueActivity.State == Activity.ActivityState.Failed)
        {
            // Path failed - reset and idle
            _pursueActivity = null;
            return new IdleAction(_owner, this);
        }

        // Continue the activity
        return _pursueActivity.GetNextAction(_owner.GetCurrentGridPosition(), new Perception());
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

    private void PlayBoneRattle()
    {
        // Access the AudioStreamPlayer2D component
        (_owner as MindlessSkeleton)?.CallDeferred("PlayBoneRattle");
    }
}
