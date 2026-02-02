using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a building.
/// Completes when the entity reaches a position adjacent to the building.
/// Fails if the building no longer exists or no path can be found.
///
/// When targetStorage is true and the building's storage facility has RequireAdjacent set,
/// the activity will navigate to a position adjacent to the storage facility
/// rather than just the building entrance.
/// </summary>
public class GoToBuildingActivity : Activity
{
    private readonly Building _targetBuilding;
    private readonly bool _targetStorage;
    private PathFinder? _pathFinder;
    private int _stuckTicks;
    private const int MAXSTUCKTICKS = 50;

    public override string DisplayName => $"Going to {_targetBuilding.BuildingType}";
    public override Building? TargetBuilding => _targetBuilding;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToBuildingActivity"/> class.
    /// Creates an activity to navigate to a building.
    /// </summary>
    /// <param name="targetBuilding">The building to navigate to.</param>
    /// <param name="priority">Action priority.</param>
    /// <param name="targetStorage">If true, navigate to storage access position (handles facility's RequireAdjacent automatically).</param>
    public GoToBuildingActivity(Building targetBuilding, int priority = 0, bool targetStorage = false)
    {
        _targetBuilding = targetBuilding;
        _targetStorage = targetStorage;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();

        // If targeting storage and building requires facility navigation, use SetFacilityGoal
        if (_targetStorage && _targetBuilding.RequiresStorageFacilityNavigation())
        {
            if (!_pathFinder.SetFacilityGoal(_targetBuilding, "storage"))
            {
                // No valid storage facility position found - fall back to building goal
                _pathFinder.SetBuildingGoal(owner, _targetBuilding);
            }
        }
        else if (_targetStorage)
        {
            // Storage doesn't require facility adjacency - just need to reach building perimeter
            // This is used for buildings like wells where entities access storage from outside
            _pathFinder.SetBuildingGoal(owner, _targetBuilding, requireInterior: false);
        }
        else
        {
            _pathFinder.SetBuildingGoal(owner, _targetBuilding);
        }
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

        // Check if we're stuck (but not if we're in a queue - that's intentional waiting)
        bool hasValidPath = _pathFinder.HasValidPath();
        if (!hasValidPath && !_owner.IsInQueue)
        {
            _stuckTicks++;
            if (_stuckTicks > MAXSTUCKTICKS)
            {
                DebugLog("GO_TO_BUILDING", $"Failed: stuck at {position} trying to reach {_targetBuilding.BuildingName}");
                Fail();
                return null;
            }
        }
        else if (_owner.IsInQueue)
        {
            // Reset stuck counter while in queue - we're waiting, not stuck
            _stuckTicks = 0;
        }

        // Return movement action
        return new MoveAlongPathAction(_owner, this, _pathFinder, Priority);
    }
}
