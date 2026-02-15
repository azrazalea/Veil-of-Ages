using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// General-purpose activity that follows a NavigationPlan across area boundaries.
/// Internally manages GoToLocationActivity for within-area movement
/// and ChangeAreaAction for area transitions.
/// </summary>
public class GoToWorldPositionActivity : Activity
{
    private readonly NavigationPlan _plan;
    private int _currentStepIndex;
    private GoToLocationActivity? _currentNavigation;

    public override string DisplayName => "Traveling";

    public GoToWorldPositionActivity(NavigationPlan plan, int priority = 0)
    {
        _plan = plan;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null || _plan.IsEmpty)
        {
            Fail();
            return null;
        }

        if (_currentStepIndex >= _plan.Steps.Count)
        {
            Complete();
            return null;
        }

        var currentStep = _plan.Steps[_currentStepIndex];

        switch (currentStep)
        {
            case GoToPositionStep goToStep:
                return HandleGoToPosition(goToStep, position, perception);

            case TransitionStep transitionStep:
                return HandleTransition(transitionStep);

            default:
                Fail();
                return null;
        }
    }

    private EntityAction? HandleGoToPosition(GoToPositionStep step, Vector2I position, Perception perception)
    {
        // Create navigation sub-activity if needed
        if (_currentNavigation == null)
        {
            _currentNavigation = new GoToLocationActivity(step.Position, Priority);
            _currentNavigation.Initialize(_owner!);
        }

        var (result, action) = RunSubActivity(_currentNavigation, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("WORLD_NAV", $"Failed to navigate to {step.Position}", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                _currentNavigation = null;
                _currentStepIndex++;

                // If we've reached the end, complete
                if (_currentStepIndex >= _plan.Steps.Count)
                {
                    Complete();
                    return null;
                }

                // Otherwise continue to next step (recursive call for immediate processing)
                return GetNextAction(position, perception);
        }

        return null;
    }

    private EntityAction? HandleTransition(TransitionStep step)
    {
        if (step.TransitionPoint.LinkedPoint == null)
        {
            DebugLog("WORLD_NAV", "Transition point has no linked destination", 0);
            Fail();
            return null;
        }

        _currentStepIndex++;
        DebugLog("WORLD_NAV", $"Transitioning via {step.TransitionPoint.Label}", 0);
        return new ChangeAreaAction(_owner!, this, step.TransitionPoint.LinkedPoint, Priority);
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Cross-area navigation plans are positional: each step assumes the entity
        // is in a specific area at a specific index. If the entity was interrupted
        // and moved (e.g., pushed to a different area), the step index may be invalid.
        // The safest approach is to fail so the parent activity re-creates the plan.
        _currentNavigation = null;
        Fail();
    }
}
