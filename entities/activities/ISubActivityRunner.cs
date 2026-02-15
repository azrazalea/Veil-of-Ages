using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Shared interface for running sub-activities. Implemented by both Activity (for activity composition)
/// and EntityCommand (for command-driven activities). Provides a single source of truth for the
/// RunSubActivity pattern that handles immediate completion, null actions, and state transitions.
/// </summary>
public interface ISubActivityRunner
{
    /// <summary>
    /// Gets the Being that owns this runner (entity performing the activity/command).
    /// </summary>
    Being? SubActivityOwner { get; }

    /// <summary>
    /// Runs a sub-activity and returns its result. Handles the common pattern where a sub-activity
    /// may complete immediately (returning null) on the same tick. Propagates entity events,
    /// checks for state transitions, and returns an IdleAction to hold the action slot if needed.
    /// </summary>
    /// <param name="subActivity">The sub-activity to run.</param>
    /// <param name="position">Current grid position to pass to sub-activity.</param>
    /// <param name="perception">Current perception to pass to sub-activity.</param>
    /// <param name="priority">Priority for the fallback IdleAction if sub-activity returns null while still running.</param>
    /// <returns>
    /// A tuple of (result, action):
    /// - Continue: Sub-activity is running, return the action
    /// - Completed: Sub-activity finished, proceed to next phase
    /// - Failed: Sub-activity failed, handle the failure.
    /// </returns>
    (Activity.SubActivityResult result, EntityAction? action) RunSubActivity(
        Activity subActivity, Vector2I position, Perception perception, int priority)
    {
        // Already failed
        if (subActivity.State == Activity.ActivityState.Failed)
        {
            return (Activity.SubActivityResult.Failed, null);
        }

        // Already completed
        if (subActivity.State == Activity.ActivityState.Completed)
        {
            return (Activity.SubActivityResult.Completed, null);
        }

        // Propagate entity events to sub-activity (may change state)
        subActivity.ProcessEntityEvents(perception);

        // Check state after event processing
        if (subActivity.State == Activity.ActivityState.Failed)
        {
            return (Activity.SubActivityResult.Failed, null);
        }

        if (subActivity.State == Activity.ActivityState.Completed)
        {
            return (Activity.SubActivityResult.Completed, null);
        }

        // Try to get next action
        var action = subActivity.GetNextAction(position, perception);

        // Got an action - sub-activity is running
        if (action != null)
        {
            return (Activity.SubActivityResult.Continue, action);
        }

        // Action was null - state may have changed during GetNextAction
        if (subActivity.State == Activity.ActivityState.Completed)
        {
            return (Activity.SubActivityResult.Completed, null);
        }

        if (subActivity.State == Activity.ActivityState.Failed)
        {
            return (Activity.SubActivityResult.Failed, null);
        }

        // Still running but returned null - return idle to hold our slot
        return (Activity.SubActivityResult.Continue, new IdleAction(SubActivityOwner!, this, priority));
    }
}
