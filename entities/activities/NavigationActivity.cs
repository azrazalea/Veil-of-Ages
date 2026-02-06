using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Base class for navigation activities that move an entity to a target location.
/// Handles PathFinder management, stuck detection, interruption resumption, and alternate path finding.
/// Subclasses define the goal (via Initialize/PathFinder setup) and may override hooks for
/// target validation, queue behavior, and failure logging.
/// </summary>
public abstract class NavigationActivity : Activity
{
    protected PathFinder? _pathFinder;
    private int _stuckTicks;
    private const int MAXSTUCKTICKS = 50;

    /// <summary>
    /// Gets a value indicating whether whether GetNextAction should check IsInQueue and idle while queued.
    /// Override to return true for activities that navigate to buildings/facilities
    /// where entities may need to wait in queue.
    /// </summary>
    protected virtual bool ShouldCheckQueue => false;

    /// <summary>
    /// Validate that the navigation target is still valid (e.g., building still exists).
    /// Called at the start of each GetNextAction. Return false to fail the activity.
    /// Default returns true (no validation needed).
    /// </summary>
    protected virtual bool ValidateTarget() => true;

    /// <summary>
    /// Called when the activity fails due to being stuck for too long.
    /// Override to add custom logging or cleanup. Base implementation does nothing.
    /// The activity will be marked as Failed after this call.
    /// </summary>
    /// <param name="position">The position where the entity is stuck.</param>
    protected virtual void OnStuckFailed(Vector2I position)
    {
    }

    protected override void OnResume()
    {
        base.OnResume();
        _stuckTicks = 0;
        _pathFinder?.ClearPath(); // Force recalculation from current position
    }

    /// <summary>
    /// Try to find an alternate path around a blocking entity.
    /// Clears the current path and recalculates with perception-aware pathfinding.
    /// </summary>
    public override bool TryFindAlternatePath(Perception perception)
    {
        if (_owner == null || _pathFinder == null)
        {
            return false;
        }

        // Force path recalculation
        _pathFinder.ClearPath();

        // Try to calculate a new path - perception will mark the blocker as blocked
        bool foundPath = _pathFinder.CalculatePathIfNeeded(_owner, perception);

        if (foundPath && _pathFinder.HasValidPath())
        {
            _stuckTicks = 0; // Reset stuck counter on finding alternate path
            return true;
        }

        return false;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null || _pathFinder == null)
        {
            Fail();
            return null;
        }

        // Already failed during initialization (e.g., no valid facility position)
        if (State == ActivityState.Failed)
        {
            return null;
        }

        // Subclass-specific target validation (e.g., building still exists)
        if (!ValidateTarget())
        {
            Fail();
            return null;
        }

        // Check if we've reached the goal
        if (_pathFinder.IsGoalReached(_owner))
        {
            Complete();
            return null;
        }

        // If in queue, just idle - we're intentionally waiting
        if (ShouldCheckQueue && _owner.IsInQueue)
        {
            _stuckTicks = 0;
            return new IdleAction(_owner, this, Priority);
        }

        // Calculate path if needed (A* runs here on Think thread, not in Execute)
        if (!_pathFinder.CalculatePathIfNeeded(_owner, perception))
        {
            // Path calculation failed
            if (!WasInterrupted)
            {
                _stuckTicks++;
            }

            if (_stuckTicks > MAXSTUCKTICKS)
            {
                OnStuckFailed(position);
                Fail();
                return null;
            }

            // Return idle to wait for next think cycle
            return new IdleAction(_owner, this, Priority);
        }

        // Reset stuck counter on successful path
        _stuckTicks = 0;

        // Return movement action (Execute will only follow pre-calculated path)
        return new MoveAlongPathAction(_owner, this, _pathFinder, Priority);
    }
}
