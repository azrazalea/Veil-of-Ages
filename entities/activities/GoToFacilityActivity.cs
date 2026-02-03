using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for navigating to be adjacent to a facility in a building.
/// Completes when the entity reaches a position adjacent to the specified facility.
/// Fails if the building no longer exists, facility not found, or no path can be found.
/// </summary>
public class GoToFacilityActivity : Activity
{
    private readonly Building _building;
    private readonly string _facilityId;
    private PathFinder? _pathFinder;
    private int _stuckTicks;
    private const int MAXSTUCKTICKS = 50;

    public override string DisplayName => $"Going to {_facilityId}";
    public override Building? TargetBuilding => _building;
    public override string? TargetFacilityId => _facilityId;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        return _pathFinder?.GetAlternativeGoalPositions(entity) ?? new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToFacilityActivity"/> class.
    /// Create an activity to navigate adjacent to a specific facility in a building.
    /// </summary>
    /// <param name="building">The building containing the facility.</param>
    /// <param name="facilityId">The facility ID (e.g., "oven", "quern", "storage", "crop").</param>
    /// <param name="priority">Action priority (default 0).</param>
    public GoToFacilityActivity(Building building, string facilityId, int priority = 0)
    {
        _building = building;
        _facilityId = facilityId;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();
        if (!_pathFinder.SetFacilityGoal(_building, _facilityId))
        {
            Log.Warn($"{owner.Name}: No accessible {_facilityId} in {_building.BuildingName}");
            Fail();
        }
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null || _pathFinder == null)
        {
            Fail();
            return null;
        }

        // Already failed during initialization
        if (State == ActivityState.Failed)
        {
            return null;
        }

        // Check if building still exists
        if (!GodotObject.IsInstanceValid(_building))
        {
            Log.Warn($"{_owner.Name}: Building destroyed while navigating to {_facilityId}");
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
        if (_owner.IsInQueue)
        {
            _stuckTicks = 0;
            return new IdleAction(_owner, this, Priority);
        }

        // Calculate path if needed (A* runs here on Think thread, not in Execute)
        if (!_pathFinder.CalculatePathIfNeeded(_owner))
        {
            // Path calculation failed
            _stuckTicks++;
            if (_stuckTicks > MAXSTUCKTICKS)
            {
                Log.Warn($"{_owner.Name}: Stuck trying to reach {_facilityId}");
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
