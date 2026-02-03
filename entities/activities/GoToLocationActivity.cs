using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a specific grid position.
/// Completes when the entity reaches the target position.
/// Fails if no path can be found.
/// </summary>
public class GoToLocationActivity : Activity
{
    private readonly Vector2I _targetPosition;
    private PathFinder? _pathFinder;
    private int _stuckTicks;
    private const int MAXSTUCKTICKS = 50;

    public override string DisplayName => $"Going to ({_targetPosition.X}, {_targetPosition.Y})";

    public GoToLocationActivity(Vector2I targetPosition, int priority = 0)
    {
        _targetPosition = targetPosition;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();
        _pathFinder.SetPositionGoal(owner, _targetPosition);
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

        // Check if we've reached the goal
        if (_pathFinder.IsGoalReached(_owner))
        {
            Complete();
            return null;
        }

        // Calculate path if needed (A* runs here on Think thread, not in Execute)
        if (!_pathFinder.CalculatePathIfNeeded(_owner, perception))
        {
            // Path calculation failed
            _stuckTicks++;
            if (_stuckTicks > MAXSTUCKTICKS)
            {
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
