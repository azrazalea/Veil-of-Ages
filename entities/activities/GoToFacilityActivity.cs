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
    // Nullable to support standalone facilities that have no owner building.
    private readonly Building? _building;
    private readonly string _facilityId;

    // Optional direct facility reference — used by the Facility constructor overload.
    private readonly Facility? _facility;

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
        _facility = null;
        Priority = priority;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToFacilityActivity"/> class.
    /// Create an activity to navigate adjacent to a specific facility using the
    /// <see cref="Facility"/> object directly. Backward-compat fields are populated
    /// from <paramref name="facility"/>'s owner and ID.
    /// </summary>
    /// <param name="facility">The facility to navigate to.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public GoToFacilityActivity(Facility facility, int priority = 0)
    {
        _facility = facility;

        // Populate backward-compat fields so ValidateTarget and DisplayName still work.
        // Owner is nullable (standalone facilities have no owner), so _building may be null.
        _building = facility.Owner;
        _facilityId = facility.Id;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();

        bool goalSet;
        if (_facility != null)
        {
            // Use the Facility-direct overload — delegates to building overload internally
            // if the facility has an owner, otherwise handles standalone facilities.
            goalSet = _pathFinder.SetFacilityGoal(owner, _facility);
        }
        else if (_building != null)
        {
            goalSet = _pathFinder.SetFacilityGoal(owner, _building, _facilityId);
        }
        else
        {
            Log.Warn($"{owner.Name}: GoToFacilityActivity has no facility or building reference");
            Fail();
            return;
        }

        if (!goalSet)
        {
            Log.Warn($"{owner.Name}: No accessible {_facilityId} in {_building?.BuildingName ?? "(standalone)"}");
            Fail();
        }
    }

    protected override bool ValidateTarget()
    {
        // If no building reference (standalone facility), skip building validation.
        if (_building == null)
        {
            return true;
        }

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
