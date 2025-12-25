using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Needs.Strategies;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that handles need satisfaction by consuming at a source.
/// Uses the Activity system to execute the multi-step consumption behavior.
///
/// Trait responsibilities (DECIDE):
/// - Check if need is low
/// - Find food source using identifier strategy
/// - Handle critical state
/// - Start EatActivity when food source found
///
/// Activity responsibilities (EXECUTE):
/// - Navigate to food source
/// - Consume for duration
/// - Restore need.
/// </summary>
public class ConsumptionBehaviorTrait : BeingTrait
{
    private readonly string _needId;
    private Need? _need;

    private readonly IFoodSourceIdentifier _sourceIdentifier;
    private readonly ICriticalStateHandler _criticalStateHandler;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    public ConsumptionBehaviorTrait(
        string needId,
        IFoodSourceIdentifier sourceIdentifier,
        ICriticalStateHandler criticalStateHandler,
        float restoreAmount = 60f,
        uint consumptionDuration = 244)
    {
        _needId = needId;
        _sourceIdentifier = sourceIdentifier;
        _criticalStateHandler = criticalStateHandler;
        _restoreAmount = restoreAmount;
        _consumptionDuration = consumptionDuration;
    }

    // Legacy constructor for compatibility - extracts restore amount from effect
    public ConsumptionBehaviorTrait(
        string needId,
        IFoodSourceIdentifier sourceIdentifier,
        IFoodAcquisitionStrategy acquisitionStrategy,  // No longer used
        IConsumptionEffect consumptionEffect,          // No longer used - restore amount passed directly
        ICriticalStateHandler criticalStateHandler,
        uint consumptionDuration = 30)
    {
        _needId = needId;
        _sourceIdentifier = sourceIdentifier;
        _criticalStateHandler = criticalStateHandler;
        _restoreAmount = 60f;  // Default, was hardcoded in FarmConsumptionEffect
        _consumptionDuration = consumptionDuration;
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        _need = _owner?.NeedsSystem?.GetNeed(_needId);

        if (_need == null)
        {
            GD.PrintErr($"{_owner?.Name}: ConsumptionBehaviorTrait could not find need '{_needId}'");
        }
        else
        {
            GD.Print($"{_owner?.Name}: ConsumptionBehaviorTrait initialized for need '{_needId}'");
        }
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (!IsInitialized || _owner == null || _need == null)
        {
            return null;
        }

        // If already moving, don't interrupt
        if (_owner.IsMoving())
        {
            return null;
        }

        // If we already have an eating activity running, let it handle things
        if (_owner.GetCurrentActivity() is EatActivity)
        {
            return null;
        }

        // Check if need is low (hungry)
        if (_need.IsLow())
        {
            // Find food source
            var foodSource = _sourceIdentifier.IdentifyFoodSource(_owner, currentPerception);

            if (foodSource == null)
            {
                // Only log occasionally to avoid spam
                if (MyPathfinder.CurrentTick % 200 == 0)
                {
                    GD.Print($"{_owner.Name}: No food source found for '{_needId}'");
                }

                // No food source found
                if (_need.IsCritical())
                {
                    return _criticalStateHandler.HandleCriticalState(_owner, _need);
                }

                return null;
            }

            // Start eating activity
            var eatActivity = new EatActivity(
                foodSource,
                _need,
                _restoreAmount,
                _consumptionDuration,
                priority: Priority);

            return new StartActivityAction(_owner, this, eatActivity, priority: Priority);
        }

        return null;
    }
}
