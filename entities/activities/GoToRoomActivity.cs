using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a room.
/// Completes when the entity reaches a position inside the room.
/// Fails if the room is destroyed or no path can be found.
///
/// When targetStorage is true and the room's storage facility has RequireAdjacent set,
/// the activity will navigate to a position adjacent to the storage facility
/// rather than just entering the room.
/// </summary>
public class GoToRoomActivity : NavigationActivity
{
    private readonly Room _targetRoom;
    private readonly bool _targetStorage;
    private readonly bool _requireInterior;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_BUILDING", _targetRoom.Name);
    public override Room? TargetRoom => _targetRoom;

    protected override bool ShouldCheckQueue => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoToRoomActivity"/> class.
    /// Creates an activity to navigate to a room.
    /// </summary>
    /// <param name="targetRoom">The room to navigate to.</param>
    /// <param name="priority">Action priority.</param>
    /// <param name="targetStorage">If true, navigate to storage access position.</param>
    /// <param name="requireInterior">If false, entity can reach goal by standing adjacent (perimeter).</param>
    public GoToRoomActivity(Room targetRoom, int priority = 0, bool targetStorage = false, bool requireInterior = true)
    {
        _targetRoom = targetRoom;
        _targetStorage = targetStorage;
        _requireInterior = requireInterior;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        _pathFinder = new PathFinder();

        // If targeting storage and room has a storage facility requiring adjacency,
        // use facility goal for precise navigation
        if (_targetStorage)
        {
            var storageFacility = _targetRoom.GetStorageFacility();
            if (storageFacility != null && storageFacility.RequireAdjacent)
            {
                if (_pathFinder.SetFacilityGoal(owner, storageFacility))
                {
                    return;
                }

                // Fall back to room goal if facility goal failed
            }
            else if (storageFacility != null)
            {
                // Storage doesn't require facility adjacency - just need to reach room perimeter
                _pathFinder.SetRoomGoal(owner, _targetRoom, requireInterior: false);
                return;
            }
        }

        _pathFinder.SetRoomGoal(owner, _targetRoom, requireInterior: _requireInterior);
    }

    protected override bool ValidateTarget()
    {
        // Room is a plain C# object (not a GodotObject), so we check IsDestroyed
        if (_targetRoom.IsDestroyed)
        {
            DebugLog("GO_TO_ROOM", "FAILING: target room destroyed", 0);
            return false;
        }

        return true;
    }

    protected override void OnStuckFailed(Vector2I position)
    {
        DebugLog("GO_TO_ROOM", $"Failed: stuck at {position} trying to reach {_targetRoom.Name}");
    }
}
