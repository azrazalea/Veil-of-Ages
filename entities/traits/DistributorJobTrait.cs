using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for distributors. Distributors work at the granary during daytime (Dawn/Day).
/// They read standing orders and deliver food to households.
/// At night, DistributorJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class DistributorJobTrait : BeingTrait
{
    private Building? _workplace; // The granary

    public DistributorJobTrait()
    {
    }

    public DistributorJobTrait(Building workplace)
    {
        _workplace = workplace;
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "workplace" (Building): The granary building to work at (recommended but optional).
    /// </summary>
    /// <remarks>
    /// If no workplace is provided, the trait will be non-functional but won't crash.
    /// The distributor will simply not suggest any work actions until a workplace is assigned.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        if (_workplace != null)
        {
            return true;
        }

        // workplace is recommended but we handle null gracefully in SuggestAction()
        if (config.GetBuilding("workplace") == null)
        {
            Log.Warn("DistributorJobTrait: 'workplace' parameter recommended for proper function");
        }

        return true; // Don't fail - we handle missing workplace gracefully
    }

    public override void Configure(TraitConfiguration config)
    {
        if (_workplace != null)
        {
            return;
        }

        _workplace = config.GetBuilding("workplace");
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || _workplace == null || !GodotObject.IsInstanceValid(_workplace))
        {
            return null;
        }

        // Don't interrupt movement
        if (_owner.IsMoving())
        {
            return null;
        }

        // If already doing a distribution round, let the activity handle things
        if (_owner.GetCurrentActivity() is DistributorRoundActivity)
        {
            return null;
        }

        // Only work during work hours (Dawn/Day)
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            return null; // Let VillagerTrait handle night behavior
        }

        // Check if granary has standing orders
        var orders = _workplace.Traits.OfType<GranaryTrait>().FirstOrDefault()?.Orders;
        if (orders == null || orders.Count == 0)
        {
            DebugLog("DISTRIBUTOR", "No standing orders at granary");
            return null;
        }

        // Start a distribution round (activity logs when it actually starts)
        var roundActivity = new DistributorRoundActivity(_workplace, priority: 0);
        return new StartActivityAction(_owner, this, roundActivity, priority: 0);
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Day)
        {
            return $"Morning, {speaker.Name}! I'm off to make my rounds.";
        }

        return $"Evening, {speaker.Name}. Deliveries done for today.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am a distributor. I deliver food from the granary to households.";
    }
}
