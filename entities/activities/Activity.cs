using System.Collections.Generic;
using Godot;
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
    public int Priority { get; protected set; } = 0;

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
}
