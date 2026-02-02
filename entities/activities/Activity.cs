using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Base class for all activities. Activities represent "what an entity is currently doing" -
/// the execution layer between trait decisions and atomic actions.
///
/// Traits DECIDE what to do, Activities EXECUTE multi-step behaviors, Actions are ATOMIC.
/// </summary>
public abstract class Activity
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
    /// Gets the building this activity is using, if any.
    /// Used for queue communication - when another entity asks us to move,
    /// we tell them what building we're using so they can queue.
    /// </summary>
    public virtual Building? TargetBuilding => null;

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
    /// Handle a move request from another entity trying to pass through our position.
    /// Default behavior: if same building+facility, they queue. Otherwise try to step aside.
    /// </summary>
    /// <param name="requester">The entity requesting us to move.</param>
    /// <param name="requesterBuilding">The building the requester is heading to.</param>
    /// <param name="requesterFacility">The facility the requester wants to use.</param>
    /// <returns>True if handled, false if Being should use default step-aside.</returns>
    public virtual bool HandleMoveRequest(Being requester, Building? requesterBuilding, string? requesterFacility)
    {
        if (_owner == null)
        {
            return false;
        }

        // If not interruptible, always tell them to queue
        if (!IsInterruptible)
        {
            if (TargetBuilding != null)
            {
                requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetBuilding));
            }
            else
            {
                requester.QueueEvent(EntityEventType.StuckNotification, _owner, null);
            }

            return true;
        }

        // If requester wants the same building AND facility, they must queue
        if (TargetBuilding != null && requesterBuilding == TargetBuilding && requesterFacility == TargetFacilityId)
        {
            requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetBuilding));
            return true;
        }

        // Try to step aside within our work area
        var alternatives = GetAlternativeGoalPositions(_owner);
        if (alternatives.Count > 0)
        {
            // Move to nearest alternative position
            _owner.TryMoveToGridPosition(alternatives[0]);
            return true;
        }

        // Can't step aside - only tell them to queue if they want the same building AND facility
        // (Don't queue random passers-by who are going somewhere else)
        if (TargetBuilding != null && requesterBuilding == TargetBuilding && requesterFacility == TargetFacilityId)
        {
            requester.QueueEvent(EntityEventType.QueueRequest, _owner, new QueueResponseData(TargetBuilding));
            return true;
        }

        // Either no building, or requester wants different destination - let Being handle step-aside
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
        // Already failed
        if (subActivity.State == ActivityState.Failed)
        {
            return (SubActivityResult.Failed, null);
        }

        // Already completed
        if (subActivity.State == ActivityState.Completed)
        {
            return (SubActivityResult.Completed, null);
        }

        // Try to get next action
        var action = subActivity.GetNextAction(position, perception);

        // Got an action - sub-activity is running
        if (action != null)
        {
            return (SubActivityResult.Continue, action);
        }

        // Action was null - state may have changed during GetNextAction
        if (subActivity.State == ActivityState.Completed)
        {
            return (SubActivityResult.Completed, null);
        }

        if (subActivity.State == ActivityState.Failed)
        {
            return (SubActivityResult.Failed, null);
        }

        // Still running but returned null - return idle to hold our slot
        return (SubActivityResult.Continue, new IdleAction(_owner!, this, Priority));
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
