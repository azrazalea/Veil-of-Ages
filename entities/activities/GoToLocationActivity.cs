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

        // Check if we're stuck
        if (!_pathFinder.HasValidPath() && _stuckTicks++ > MAXSTUCKTICKS)
        {
            Fail();
            return null;
        }

        // Return movement action
        return new MoveAlongPathAction(_owner, this, _pathFinder, Priority);
    }
}
