using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a specific grid position.
/// Completes when the entity reaches the target position.
/// Fails if no path can be found.
/// </summary>
public class GoToLocationActivity : NavigationActivity
{
    private readonly Vector2I _targetPosition;

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
}
