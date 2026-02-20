using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that goes to a facility and observes its storage contents.
/// Handles cross-area navigation when the facility is in a different area.
/// Used when an entity needs to refresh their memory of what's in storage
/// (e.g., hungry but no memory of food locations).
///
/// Phases:
/// 1. Navigate — GoToFacilityActivity to reach storage access position (cross-area capable)
/// 2. Observe — Call AccessFacilityStorage to observe and update memory, then complete
///
/// Interruption behavior: OnResume() nulls navigation, regresses to Navigate.
/// </summary>
public class CheckStorageActivity : Activity
{
    private enum Phase
    {
        Navigate,
        Observing
    }

    private readonly Facility _storageFacility;
    private Phase _currentPhase = Phase.Navigate;
    private Activity? _navActivity;
    private bool _hasObserved;

    public override string DisplayName => _hasObserved
        ? L.TrFmt("activity.CHECKED_STORAGE", _storageFacility.Owner?.BuildingName ?? _storageFacility.Id)
        : L.TrFmt("activity.GOING_TO_CHECK_STORAGE", _storageFacility.Owner?.BuildingName ?? _storageFacility.Id);
    public override Building? TargetBuilding => _storageFacility.Owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckStorageActivity"/> class.
    /// Create an activity to go to a facility and observe its storage.
    /// Handles cross-area navigation automatically.
    /// </summary>
    /// <param name="storageFacility">The facility to check.</param>
    /// <param name="priority">Action priority.</param>
    public CheckStorageActivity(Facility storageFacility, int priority = 0)
    {
        _storageFacility = storageFacility;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _navActivity = null;
        if (_currentPhase != Phase.Observing)
        {
            _currentPhase = Phase.Navigate;
        }
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if facility still exists
        if (!GodotObject.IsInstanceValid(_storageFacility))
        {
            DebugLog("CHECK_STORAGE", "Facility no longer valid, failing", 0);
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            Phase.Navigate => ProcessNavigate(position, perception),
            Phase.Observing => ProcessObserving(),
            _ => null
        };
    }

    private EntityAction? ProcessNavigate(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_navActivity == null)
        {
            _navActivity = new GoToFacilityActivity(_storageFacility, Priority);
            _navActivity.Initialize(_owner);
            DebugLog("CHECK_STORAGE", $"Starting navigation to {_storageFacility.Owner?.BuildingName ?? _storageFacility.Id}", 0);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("CHECK_STORAGE", "Navigation failed", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        _navActivity = null;
        _currentPhase = Phase.Observing;
        DebugLog("CHECK_STORAGE", $"Arrived at {_storageFacility.Owner?.BuildingName ?? _storageFacility.Id}", 0);
        return ProcessObserving();
    }

    private EntityAction? ProcessObserving()
    {
        if (_owner == null)
        {
            return null;
        }

        if (!_hasObserved)
        {
            var storage = _owner.AccessFacilityStorage(_storageFacility);
            _hasObserved = true;

            if (storage != null)
            {
                DebugLog("CHECK_STORAGE", $"Observed storage: {storage.GetContentsSummary()}", 0);
            }
            else
            {
                DebugLog("CHECK_STORAGE", "Facility has no storage", 0);
            }

            Complete();
            return null;
        }

        Complete();
        return null;
    }
}
