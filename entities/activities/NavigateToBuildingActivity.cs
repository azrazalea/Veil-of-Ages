using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Cross-area capable navigation to a building. Drop-in replacement for GoToBuildingActivity.
///
/// Phases:
/// 1. CrossAreaNav (if needed) — GoToWorldPositionActivity via NavigationHelper
/// 2. LocalNav — GoToBuildingActivity to reach the building within the target area
///
/// OnResume() nulls navigation, regresses to CrossAreaNav.
/// If already in the same area as the target, CrossAreaNav is skipped immediately.
/// </summary>
public class NavigateToBuildingActivity : Activity
{
    private enum Phase
    {
        CrossAreaNav,
        LocalNav
    }

    private readonly Building _target;
    private readonly bool _targetStorage;
    private readonly bool _requireInterior;

    private Phase _currentPhase = Phase.CrossAreaNav;
    private Activity? _navActivity;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_BUILDING", _target.BuildingType);
    public override Building? TargetBuilding => _target;

    public NavigateToBuildingActivity(
        Building target,
        int priority,
        bool targetStorage = false,
        bool requireInterior = true)
    {
        _target = target;
        _targetStorage = targetStorage;
        _requireInterior = requireInterior;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _navActivity = null;
        _currentPhase = Phase.CrossAreaNav;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        if (!GodotObject.IsInstanceValid(_target))
        {
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            Phase.CrossAreaNav => ProcessCrossAreaNav(position, perception),
            Phase.LocalNav => ProcessLocalNav(position, perception),
            _ => null
        };
    }

    private EntityAction? ProcessCrossAreaNav(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Skip cross-area nav if already in the same area
        if (_owner.GridArea == null || _target.GridArea == null
            || _owner.GridArea == _target.GridArea)
        {
            _currentPhase = Phase.LocalNav;
            return ProcessLocalNav(position, perception);
        }

        if (_navActivity == null)
        {
            _navActivity = NavigationHelper.CreateNavigationToBuilding(
                _owner, _target, Priority, _targetStorage);
            _navActivity.Initialize(_owner);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        // Cross-area nav complete — now do local nav
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
            _navActivity = new GoToBuildingActivity(
                _target, Priority,
                targetStorage: _targetStorage,
                requireInterior: _requireInterior);
            _navActivity.Initialize(_owner);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        Complete();
        return null;
    }
}
