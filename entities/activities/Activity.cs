using System;
using System.Collections.Generic;
using Godot;
using Stateless;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Base class for all activities. Activities represent "what an entity is currently doing" -
/// the execution layer between trait decisions and atomic actions.
///
/// Traits DECIDE what to do, Activities EXECUTE multi-step behaviors, Actions are ATOMIC.
/// </summary>
public abstract class Activity : ISubActivityRunner
{
    public enum ActivityState
    {
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// Result of running a sub-activity via RunSubActivity helper.
    /// </summary>
    public enum SubActivityResult
    {
        /// <summary>Sub-activity is still running, use the returned action.</summary>
        Continue,

        /// <summary>Sub-activity completed successfully, proceed to next phase.</summary>
        Completed,

        /// <summary>Sub-activity failed.</summary>
        Failed
    }

    /// <summary>
    /// Gets or sets current state of the activity. Check this to know if activity is done.
    /// </summary>
    public ActivityState State { get; protected set; } = ActivityState.Running;

    /// <summary>
    /// Gets human-readable name for UI display (e.g., "Eating at Farm", "Going home").
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets or sets default priority for actions returned by this activity.
    /// Lower values = higher priority.
    /// </summary>
    public int Priority { get; protected set; }

    /// <summary>
    /// Gets the room this activity is targeting, if any.
    /// Used for queue communication - when another entity asks us to move,
    /// we tell them what room we're using so they can queue.
    /// </summary>
    public virtual Room? TargetRoom => null;

    /// <summary>
    /// Gets the facility ID this activity is using within the building, if any.
    /// Used for queue comparison - only queue if same building AND same facility.
    /// </summary>
    public virtual string? TargetFacilityId => null;

    /// <summary>
    /// Gets alternative goal positions within this activity's work area.
    /// Used for step-aside behavior when another entity needs to pass through.
    /// Default returns empty list. Override in activities with defined work areas.
    /// </summary>
    /// <param name="entity">The entity that needs alternative positions.</param>
    /// <returns>List of valid positions sorted by distance from entity.</returns>
    public virtual List<Vector2I> GetAlternativeGoalPositions(Being entity) => new ();

    /// <summary>
    /// Gets a value indicating whether whether this activity can be interrupted by stepping aside.
    /// If false, the entity will never step aside and others must queue or go around.
    /// </summary>
    public virtual bool IsInterruptible => true;

    /// <summary>
    /// Reasons an activity can be interrupted.
    /// </summary>
    public enum InterruptionReason
    {
        Command,
        Pushed,
        HighPriorityTrait
    }

    /// <summary>
    /// Gets or sets a value indicating whether tracks whether this activity has been interrupted by a command/push.
    /// Reset when the interruption ends (e.g., command completes).
    /// </summary>
    protected bool WasInterrupted { get; set; }

    /// <summary>
    /// Process entity events from perception before suggesting action.
    /// Called by Being.Think() before GetNextAction() - activities do not need to call this manually.
    /// Override to customize event handling.
    /// </summary>
    /// <param name="perception">Current perception with entity events.</param>
    public virtual void ProcessEntityEvents(Perception perception)
    {
        // Check for command interruption
        if (perception.HasEntityEvent(EntityEventType.CommandAssigned))
        {
            OnInterrupted(InterruptionReason.Command);
        }

        // Check for push
        if (perception.HasEntityEvent(EntityEventType.EntityPushed))
        {
            OnInterrupted(InterruptionReason.Pushed);
        }

        // Check for command completion (can resume)
        if (perception.HasEntityEvent(EntityEventType.CommandCompleted))
        {
            OnResume();
        }
    }

    /// <summary>
    /// Called when this activity is interrupted.
    /// Default: Sets WasInterrupted flag.
    /// Override for custom behavior (e.g., reset phase, cancel).
    /// </summary>
    protected virtual void OnInterrupted(InterruptionReason reason)
    {
        WasInterrupted = true;
        DebugLog("ACTIVITY", $"Interrupted by {reason}", 0);
    }

    /// <summary>
    /// Called when interruption ends and activity can resume.
    /// Default: Clears WasInterrupted flag.
    /// Override for custom resumption behavior.
    /// </summary>
    protected virtual void OnResume()
    {
        if (WasInterrupted)
        {
            DebugLog("ACTIVITY", "Resuming after interruption", 0);
            WasInterrupted = false;
        }
    }

    /// <summary>
    /// Try to find an alternate path around a blocking entity.
    /// Called by Being when movement was blocked to see if we can path around.
    /// Default implementation returns false - override in activities with pathfinders.
    /// </summary>
    /// <param name="perception">Current perception data (blocker should be visible).</param>
    /// <returns>True if an alternate path was found, false if no path exists.</returns>
    public virtual bool TryFindAlternatePath(Perception perception) => false;

    /// <summary>
    /// Check if a requester wants to access the same room and facility as this activity.
    /// Used to determine if they should queue behind us or find another path.
    /// </summary>
    /// <param name="requesterRoom">The room the requester is heading to.</param>
    /// <param name="requesterFacility">The facility the requester wants to use.</param>
    /// <returns>True if they want the same target and should queue.</returns>
    public bool RequesterWantsSameTarget(Room? requesterRoom, string? requesterFacility)
    {
        return TargetRoom != null &&
               requesterRoom == TargetRoom &&
               requesterFacility == TargetFacilityId;
    }

    /// <summary>
    /// Handle a move request from another entity trying to pass through our position.
    /// Default behavior: if same room+facility, they queue. Otherwise try to step aside.
    /// </summary>
    /// <param name="requester">The entity requesting us to move.</param>
    /// <param name="requesterRoom">The room the requester is heading to.</param>
    /// <param name="requesterFacility">The facility the requester wants to use.</param>
    /// <returns>True if handled, false if Being should use default step-aside.</returns>
    public virtual bool HandleMoveRequest(Being requester, Room? requesterRoom, string? requesterFacility)
    {
        if (_owner == null)
        {
            return false;
        }

        // If not interruptible, only queue them if they want the same target
        if (!IsInterruptible)
        {
            if (RequesterWantsSameTarget(requesterRoom, requesterFacility))
            {
                requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetRoom));
            }
            else
            {
                requester.QueueEvent(EntityEventType.StuckNotification, _owner, null);
            }

            return true;
        }

        // If requester wants the same room AND facility, they must queue
        if (RequesterWantsSameTarget(requesterRoom, requesterFacility))
        {
            requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetRoom));
            return true;
        }

        // Try to step aside within our work area using activity-specific alternative positions
        var alternatives = GetAlternativeGoalPositions(_owner);
        if (alternatives.Count > 0)
        {
            // Set the side-step target - Being.Think() will convert this to a MoveAction
            // Return true so Being.cs doesn't overwrite with its own TryStepAside
            _owner.SetSideStepTarget(alternatives[0]);
            return true;
        }

        // Can't step aside - only tell them to queue if they want the same room AND facility
        // (Don't queue random passers-by who are going somewhere else)
        if (RequesterWantsSameTarget(requesterRoom, requesterFacility))
        {
            requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetRoom));
            return true;
        }

        // Either no room, or requester wants different destination - let Being handle step-aside
        return false;
    }

    /// <summary>
    /// Gets or sets maps need IDs to decay rate multipliers for this activity.
    /// Needs not in this dictionary use the default multiplier of 1.0.
    /// Example: Sleep sets hunger to 0.25 (1/4 decay rate).
    /// </summary>
    protected Dictionary<string, float> NeedDecayMultipliers { get; set; } = new ();

    /// <summary>
    /// Gets the decay multiplier for a specific need while this activity is active.
    /// Returns the configured multiplier if set, otherwise 1.0 (normal decay).
    /// </summary>
    /// <returns></returns>
    public float GetNeedDecayMultiplier(string needId)
    {
        return NeedDecayMultipliers.TryGetValue(needId, out var multiplier) ? multiplier : 1.0f;
    }

    /// <summary>
    /// The entity performing this activity.
    /// </summary>
    protected Being? _owner;

    /// <summary>
    /// Gets explicit ISubActivityRunner implementation — exposes _owner for the interface's default RunSubActivity.
    /// </summary>
    Being? ISubActivityRunner.SubActivityOwner => _owner;

    /// <summary>
    /// Called when the activity is started. Sets up initial state.
    /// </summary>
    public virtual void Initialize(Being owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Returns the next action to perform, or null if no action this tick.
    /// The activity may set State to Completed or Failed during this call.
    /// Caller should check State after calling this method.
    /// </summary>
    /// <returns></returns>
    public abstract EntityAction? GetNextAction(Vector2I position, Perception perception);

    /// <summary>
    /// Called when the activity ends (completed, failed, or interrupted).
    /// Override to release resources.
    /// </summary>
    public virtual void Cleanup()
    {
    }

    /// <summary>
    /// Marks the activity as successfully completed.
    /// </summary>
    protected void Complete() => State = ActivityState.Completed;

    /// <summary>
    /// Marks the activity as failed.
    /// </summary>
    protected void Fail() => State = ActivityState.Failed;

    /// <summary>
    /// Runs a sub-activity and handles the common pattern where the sub-activity may
    /// complete immediately (returning null) on the same tick. This prevents the parent
    /// activity from being overwritten by other traits when the sub-activity completes.
    /// </summary>
    /// <param name="subActivity">The sub-activity to run.</param>
    /// <param name="position">Current position to pass to sub-activity.</param>
    /// <param name="perception">Current perception to pass to sub-activity.</param>
    /// <returns>
    /// A tuple of (result, action):
    /// - Continue: Sub-activity is running, return the action
    /// - Completed: Sub-activity finished, proceed to next phase
    /// - Failed: Sub-activity failed, handle the failure.
    /// </returns>
    /// <example>
    /// var (result, action) = RunSubActivity(_goToPhase, position, perception);
    /// switch (result)
    /// {
    ///     case SubActivityResult.Failed:
    ///         Fail();
    ///         return null;
    ///     case SubActivityResult.Continue:
    ///         return action;
    ///     case SubActivityResult.Completed:
    ///         // Fall through to next phase
    ///         break;
    /// }.
    /// </example>
    protected (SubActivityResult result, EntityAction? action) RunSubActivity(
        Activity subActivity,
        Vector2I position,
        Perception perception)
    {
        return ((ISubActivityRunner)this).RunSubActivity(subActivity, position, perception, Priority);
    }

    /// <summary>
    /// Log a debug message if the owner has debugging enabled.
    /// </summary>
    /// <param name="category">Category of the message (e.g., "ACTIVITY").</param>
    /// <param name="message">The message to log.</param>
    /// <param name="tickInterval">Minimum ticks between logs for this category (0 = no limit).</param>
    protected void DebugLog(string category, string message, int tickInterval = 100)
    {
        if (_owner?.DebugEnabled == true)
        {
            Log.EntityDebug(_owner.Name, category, message, tickInterval);
        }
    }
}

/// <summary>
/// Base class for activities that use a Stateless state machine for phase management.
/// Subclasses define TState (phase enum) and TTrigger (transition triggers enum).
/// The state machine handles interruption/resumption automatically via triggers.
///
/// Threading: The state machine is only touched by the owning entity's think thread,
/// so no locking is needed.
/// </summary>
public abstract class StatefulActivity<TState, TTrigger> : Activity
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// The Stateless state machine that drives phase transitions.
    /// Subclasses configure this in their constructor via ConfigureStateMachine().
    /// </summary>
    protected StateMachine<TState, TTrigger> _machine;

    /// <summary>
    /// Gets the trigger to fire when the activity is interrupted by a command or push.
    /// Subclasses must return their enum value for the Interrupted trigger.
    /// </summary>
    protected abstract TTrigger InterruptedTrigger { get; }

    /// <summary>
    /// Gets the trigger to fire when the interruption ends and the activity can resume.
    /// Subclasses must return their enum value for the Resumed trigger.
    /// </summary>
    protected abstract TTrigger ResumedTrigger { get; }

    /// <summary>
    /// The currently active sub-activity for the current state.
    /// Automatically nulled when the state machine transitions to a new state.
    /// Subclasses create sub-activities lazily in their Process* methods.
    /// </summary>
    protected Activity? _currentSubActivity;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatefulActivity{TState, TTrigger}"/> class.
    /// Creates the state machine with the given initial state.
    /// Subclasses should call ConfigureStateMachine() in their constructor after this.
    /// </summary>
    /// <param name="initialState">The starting state for the state machine.</param>
    protected StatefulActivity(TState initialState)
    {
        _machine = new StateMachine<TState, TTrigger>(initialState);
        _machine.OnTransitioned(transition =>
        {
            _currentSubActivity = null;
        });
    }

    /// <summary>
    /// Gets current state of the activity's state machine.
    /// </summary>
    public TState CurrentState => _machine.State;

    /// <summary>
    /// Run the current sub-activity via the base RunSubActivity method.
    /// Creates the sub-activity lazily if null using the provided factory.
    /// </summary>
    protected (SubActivityResult result, EntityAction? action) RunCurrentSubActivity(
        Func<Activity> factory, Vector2I position, Perception perception)
    {
        if (_currentSubActivity == null)
        {
            _currentSubActivity = factory();
            _currentSubActivity.Initialize(_owner!);
        }

        return RunSubActivity(_currentSubActivity, position, perception);
    }

    /// <summary>
    /// Processes entity events by firing Interrupted/Resumed triggers on the state machine
    /// instead of using the manual OnInterrupted/OnResume virtual methods.
    /// </summary>
    /// <param name="perception">Current perception with entity events.</param>
    public override void ProcessEntityEvents(Perception perception)
    {
        // Check for interruption (command assigned or entity pushed)
        if (perception.HasEntityEvent(EntityEventType.CommandAssigned) ||
            perception.HasEntityEvent(EntityEventType.EntityPushed))
        {
            if (_machine.CanFire(InterruptedTrigger))
            {
                _machine.Fire(InterruptedTrigger);
                WasInterrupted = true;
                DebugLog("ACTIVITY", $"State machine interrupted, now in {_machine.State}", 0);
            }
        }

        // Check for resumption (command completed)
        if (perception.HasEntityEvent(EntityEventType.CommandCompleted))
        {
            if (WasInterrupted && _machine.CanFire(ResumedTrigger))
            {
                _machine.Fire(ResumedTrigger);
                DebugLog("ACTIVITY", $"State machine resumed, now in {_machine.State}", 0);
            }

            WasInterrupted = false;
        }
    }
}
