using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that goes to a building and observes its storage contents.
/// Handles cross-area navigation when the building is in a different area.
/// Used when an entity needs to refresh their memory of what's in storage
/// (e.g., hungry but no memory of food locations).
///
/// Phases:
/// 1. CrossAreaNav (if needed) — cross-area navigation via NavigationHelper
/// 2. LocalNav — GoToBuildingActivity(targetStorage: true) to reach storage access position
/// 3. Observe — Call AccessStorage to observe and update memory, then complete
///
/// Interruption behavior: OnResume() nulls navigation, regresses to CrossAreaNav.
/// </summary>
public class CheckStorageActivity : Activity
{
    private enum Phase
    {
        CrossAreaNav,
        LocalNav,
        Observing
    }

    private readonly Building _targetBuilding;
    private Phase _currentPhase = Phase.CrossAreaNav;
    private Activity? _navActivity;
    private bool _hasObserved;

    public override string DisplayName => _hasObserved
        ? L.TrFmt("activity.CHECKED_STORAGE", _targetBuilding.BuildingName)
        : L.TrFmt("activity.GOING_TO_CHECK_STORAGE", _targetBuilding.BuildingName);
    public override Building? TargetBuilding => _targetBuilding;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckStorageActivity"/> class.
    /// Create an activity to go to a building and observe its storage.
    /// Handles cross-area navigation automatically.
    /// </summary>
    /// <param name="targetBuilding">The building to check.</param>
    /// <param name="priority">Action priority.</param>
    public CheckStorageActivity(Building targetBuilding, int priority = 0)
    {
        _targetBuilding = targetBuilding;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _navActivity = null;
        if (_currentPhase <= Phase.LocalNav)
        {
            _currentPhase = Phase.CrossAreaNav;
        }
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if building still exists
        if (!GodotObject.IsInstanceValid(_targetBuilding))
        {
            DebugLog("CHECK_STORAGE", "Building no longer valid, failing", 0);
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            Phase.CrossAreaNav => ProcessCrossAreaNav(position, perception),
            Phase.LocalNav => ProcessLocalNav(position, perception),
            Phase.Observing => ProcessObserving(),
            _ => null
        };
    }

    private EntityAction? ProcessCrossAreaNav(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Skip cross-area nav if already in same area
        if (_owner.GridArea == null || _targetBuilding.GridArea == null
            || _owner.GridArea == _targetBuilding.GridArea)
        {
            _currentPhase = Phase.LocalNav;
            return ProcessLocalNav(position, perception);
        }

        if (_navActivity == null)
        {
            _navActivity = NavigationHelper.CreateNavigationToBuilding(
                _owner, _targetBuilding, Priority, targetStorage: true);
            _navActivity.Initialize(_owner);
            DebugLog("CHECK_STORAGE", $"Starting cross-area navigation to {_targetBuilding.BuildingName}", 0);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("CHECK_STORAGE", "Cross-area navigation failed", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        _navActivity = null;
        _currentPhase = Phase.LocalNav;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessLocalNav(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_navActivity == null)
        {
            _navActivity = new GoToBuildingActivity(_targetBuilding, Priority, targetStorage: true);
            _navActivity.Initialize(_owner);
            DebugLog("CHECK_STORAGE", $"Starting local navigation to {_targetBuilding.BuildingName}", 0);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("CHECK_STORAGE", "Local navigation failed", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        _navActivity = null;
        _currentPhase = Phase.Observing;
        DebugLog("CHECK_STORAGE", $"Arrived at {_targetBuilding.BuildingName}", 0);
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
            var storage = _owner.AccessStorage(_targetBuilding);
            _hasObserved = true;

            if (storage != null)
            {
                DebugLog("CHECK_STORAGE", $"Observed storage: {storage.GetContentsSummary()}", 0);
            }
            else
            {
                DebugLog("CHECK_STORAGE", "Building has no storage", 0);
            }

            Complete();
            return null;
        }

        Complete();
        return null;
    }
}
