using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Two-phase activity for area transitions:
/// 1. Navigate to the transition point position using GoToLocationActivity
/// 2. Execute ChangeAreaAction to transition to the linked area.
/// </summary>
public class GoToTransitionActivity : Activity
{
    private readonly TransitionPoint _transitionPoint;
    private GoToLocationActivity? _navigationPhase;
    private bool _navigationComplete;

    public override string DisplayName => $"Going to {_transitionPoint.Label}";

    public GoToTransitionActivity(TransitionPoint transitionPoint, int priority = 0)
    {
        _transitionPoint = transitionPoint;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Set up navigation to the transition point's position
        _navigationPhase = new GoToLocationActivity(_transitionPoint.SourcePosition, Priority);
        _navigationPhase.Initialize(owner);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Verify the transition point still has a valid link
        if (_transitionPoint.LinkedPoint == null)
        {
            DebugLog("TRANSITION", "Transition point has no linked destination", 0);
            Fail();
            return null;
        }

        // Phase 1: Navigate to the transition point
        if (!_navigationComplete && _navigationPhase != null)
        {
            var (result, action) = RunSubActivity(_navigationPhase, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    DebugLog("TRANSITION", "Failed to navigate to transition point", 0);
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    _navigationComplete = true;
                    break;
            }
        }

        // Phase 2: Execute the area transition
        DebugLog("TRANSITION", $"Arrived at {_transitionPoint.Label}, transitioning...", 0);
        Complete();
        return new ChangeAreaAction(_owner, this, _transitionPoint.LinkedPoint, Priority);
    }
}
