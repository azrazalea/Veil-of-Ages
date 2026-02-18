using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for navigating to be adjacent to a facility in a building.
/// Completes when the entity reaches a position adjacent to the specified facility.
/// Fails if the building no longer exists, facility not found, or no path can be found.
/// </summary>
public class GoToFacilityActivity : NavigationActivity
{
    private readonly Building _building;
    private readonly string _facilityId;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_FACILITY", _facilityId);
    public override Building? TargetBuilding => _building;
    public override string? TargetFacilityId => _facilityId;

    protected override bool ShouldCheckQueue => true;

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
        if (!_pathFinder.SetFacilityGoal(owner, _building, _facilityId))
        {
            Log.Warn($"{owner.Name}: No accessible {_facilityId} in {_building.BuildingName}");
            Fail();
        }
    }

    protected override bool ValidateTarget()
    {
        if (!GodotObject.IsInstanceValid(_building))
        {
            Log.Warn($"{_owner!.Name}: Building destroyed while navigating to {_facilityId}");
            return false;
        }

        return true;
    }

    protected override void OnStuckFailed(Vector2I position)
    {
        Log.Warn($"{_owner!.Name}: Stuck trying to reach {_facilityId}");
    }
}
