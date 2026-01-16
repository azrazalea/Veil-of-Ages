using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for farmers. Farmers work at their assigned farm during daytime (Dawn/Day).
/// At night, FarmerJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class FarmerJobTrait : BeingTrait
{
    private readonly Building _assignedFarm;
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec

    public FarmerJobTrait(Building farm)
    {
        _assignedFarm = farm;
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_assignedFarm))
        {
            return null;
        }

        // Don't interrupt movement
        if (_owner.IsMoving())
        {
            return null;
        }

        // If already working, let the activity handle things
        if (_owner.GetCurrentActivity() is WorkFieldActivity)
        {
            return null;
        }

        // Only work during work hours (Dawn/Day)
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            return null; // Let VillagerTrait handle night behavior
        }

        // Start work activity
        var workActivity = new WorkFieldActivity(_assignedFarm, WORKDURATION, priority: 0);
        return new StartActivityAction(_owner, this, workActivity, priority: 0);
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Day)
        {
            return $"Can't talk long, {speaker.Name}. The fields need tending!";
        }

        return $"Good evening, {speaker.Name}. Long day in the fields.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am a farmer.";
    }
}
