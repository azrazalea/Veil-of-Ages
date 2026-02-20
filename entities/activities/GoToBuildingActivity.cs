using Godot;
using VeilOfAges.Core.Lib;

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
public class GoToBuildingActivity : NavigationActivity
{
    private readonly Building _targetBuilding;
    private readonly bool _targetStorage;
    private readonly bool _requireInterior;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_BUILDING", _targetBuilding.BuildingType);
    public override Building? TargetBuilding => _targetBuilding;

    protected override bool ShouldCheckQueue => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToBuildingActivity"/> class.
    /// Creates an activity to navigate to a building.
    /// </summary>
    /// <param name="targetBuilding">The building to navigate to.</param>
    /// <param name="priority">Action priority.</param>
    /// <param name="targetStorage">If true, navigate to storage access position (handles facility's RequireAdjacent automatically).</param>
    /// <param name="requireInterior">If false, entity can reach goal by standing adjacent to building (perimeter). Default true.</param>
    public GoToBuildingActivity(Building targetBuilding, int priority = 0, bool targetStorage = false, bool requireInterior = true)
    {
        _targetBuilding = targetBuilding;
        _targetStorage = targetStorage;
        _requireInterior = requireInterior;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();

        // If targeting storage and building requires facility navigation, use SetFacilityGoal
        var room = _targetBuilding.GetDefaultRoom();
        var storageFacilities = room?.GetFacilities("storage");
        bool requiresFacilityNav = storageFacilities != null && storageFacilities.Count > 0 && storageFacilities[0].RequireAdjacent;
        if (_targetStorage && requiresFacilityNav)
        {
            if (!_pathFinder.SetFacilityGoal(owner, _targetBuilding, "storage"))
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
            _pathFinder.SetBuildingGoal(owner, _targetBuilding, requireInterior: _requireInterior);
        }
    }

    protected override bool ValidateTarget()
    {
        if (!GodotObject.IsInstanceValid(_targetBuilding))
        {
            DebugLog("GO_TO_BUILDING", "FAILING: target building invalid", 0);
            return false;
        }

        return true;
    }

    protected override void OnStuckFailed(Vector2I position)
    {
        DebugLog("GO_TO_BUILDING", $"Failed: stuck at {position} trying to reach {_targetBuilding.BuildingName}");
    }
}
