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
    private readonly Building _workplace; // The granary

    public DistributorJobTrait(Building workplace)
    {
        _workplace = workplace;
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_workplace))
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
        var orders = _workplace.GetStandingOrders();
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
