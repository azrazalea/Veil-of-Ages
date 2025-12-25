using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a building.
/// Completes when the entity reaches a position adjacent to the building.
/// Fails if the building no longer exists or no path can be found.
/// </summary>
public class GoToBuildingActivity : Activity
{
    private readonly Building _targetBuilding;
    private PathFinder? _pathFinder;
    private int _stuckTicks = 0;
    private const int MAXSTUCKTICKS = 50;

    public override string DisplayName => $"Going to {_targetBuilding.BuildingType}";

    public GoToBuildingActivity(Building targetBuilding, int priority = 0)
    {
        _targetBuilding = targetBuilding;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();
        _pathFinder.SetBuildingGoal(owner, _targetBuilding);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null || _pathFinder == null)
        {
            Fail();
            return null;
        }

        // Check if building still exists
        if (!GodotObject.IsInstanceValid(_targetBuilding))
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
