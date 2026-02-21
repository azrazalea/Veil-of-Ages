using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for navigating to be adjacent to a facility in a room.
/// Completes when the entity reaches a position adjacent to the specified facility.
/// Fails if the facility/room no longer exists, or no path can be found.
/// </summary>
public class GoToFacilityActivity : NavigationActivity
{
    private readonly string _facilityId;

    // Optional direct facility reference — used by the Facility constructor overload.
    private readonly Facility? _facility;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_FACILITY", _facilityId);
    public override Room? TargetRoom => _facility?.ContainingRoom;
    public override string? TargetFacilityId => _facilityId;

    protected override bool ShouldCheckQueue => true;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        return _pathFinder?.GetAlternativeGoalPositions(entity) ?? new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToFacilityActivity"/> class.
    /// Create an activity to navigate adjacent to a specific facility using the
    /// <see cref="Facility"/> object directly.
    /// </summary>
    /// <param name="facility">The facility to navigate to.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public GoToFacilityActivity(Facility facility, int priority = 0)
    {
        _facility = facility;
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
            goalSet = _pathFinder.SetFacilityGoal(owner, _facility);
        }
        else
        {
            Log.Warn($"{owner.Name}: GoToFacilityActivity has no facility reference");
            Fail();
            return;
        }

        if (!goalSet)
        {
            Log.Warn($"{owner.Name}: No accessible {_facilityId} in {_facility?.ContainingRoom?.Name ?? "(unknown)"}");
            Fail();
        }
    }

    protected override bool ValidateTarget()
    {
        if (_facility == null)
        {
            return true; // facilityId-only mode, no runtime validation needed
        }

        if (!GodotObject.IsInstanceValid(_facility))
        {
            Log.Warn($"{_owner!.Name}: Facility destroyed while navigating to {_facilityId}");
            return false;
        }

        return true;
    }

    protected override void OnStuckFailed(Vector2I position)
    {
        Log.Warn($"{_owner!.Name}: Stuck trying to reach {_facilityId}");
    }
}
