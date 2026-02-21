using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for interacting with a facility. Two phases:
/// 1. Navigate to the facility via GoToFacilityActivity
/// 2. Return an InteractAction that calls the facility's interaction handler.
/// </summary>
public class InteractWithFacilityActivity : Activity
{
    private enum Phase
    {
        Navigating,
        Interacting
    }

    private readonly Facility _facility;
    private readonly IInteractable _interactable;
    private readonly Dialogue _dialogue;

    private Phase _currentPhase = Phase.Navigating;
    private GoToFacilityActivity? _navActivity;

    public override string DisplayName => L.TrFmt("activity.GOING_TO_FACILITY", _facility.Id);
    public override Room? TargetRoom => _facility.ContainingRoom;

    public InteractWithFacilityActivity(
        Facility facility, IInteractable interactable, Dialogue dialogue, int priority = 0)
    {
        _facility = facility;
        _interactable = interactable;
        _dialogue = dialogue;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _navActivity = null;
        _currentPhase = Phase.Navigating;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        return _currentPhase switch
        {
            Phase.Navigating => ProcessNavigating(position, perception),
            Phase.Interacting => ProcessInteracting(),
            _ => null
        };
    }

    private EntityAction? ProcessNavigating(Vector2I position, Perception perception)
    {
        if (_navActivity == null)
        {
            _navActivity = new GoToFacilityActivity(_facility, Priority);
            _navActivity.Initialize(_owner!);
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
                _currentPhase = Phase.Interacting;
                return ProcessInteracting();
            default:
                return null;
        }
    }

    private EntityAction? ProcessInteracting()
    {
        Complete();
        return new InteractAction(_owner!, this, _interactable, _dialogue, Priority);
    }
}
