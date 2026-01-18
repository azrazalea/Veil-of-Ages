using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for farmers. Farmers work at their assigned farm during daytime (Dawn/Day).
/// At night, FarmerJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class FarmerJobTrait : BeingTrait, IDesiredResources
{
    private readonly Building _assignedFarm;
    private const uint WORKDURATION = 1500; // ~3.1 real minutes, ~1.75 game hours per shift (2 shifts/day)

    // Desired resource stockpile for farmer's home
    // Farmers want to keep wheat harvested at home
    private static readonly Dictionary<string, int> _desiredResources = new ()
    {
        { "wheat", 10 } // Keep harvested wheat in stock
    };

    /// <summary>
    /// Gets the desired resource levels for the farmer's home storage.
    /// Farmers want to stockpile wheat from their harvest.
    /// </summary>
    public IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;

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

        var currentActivity = _owner.GetCurrentActivity();
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);

        // If already working, let the activity handle things
        if (currentActivity is WorkFieldActivity)
        {
            return null;
        }

        // Only work during work hours (Dawn/Day)
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            DebugLog("FARMER", $"Not work hours ({gameTime.CurrentDayPhase}), deferring to VillagerTrait");
            return null; // Let VillagerTrait handle night behavior
        }

        // Get farmer's home from VillagerTrait
        var villagerTrait = _owner.SelfAsEntity().GetTrait<VillagerTrait>();
        Building? home = villagerTrait?.Home;

        // Start work activity with home for depositing harvest
        var workActivity = new WorkFieldActivity(_assignedFarm, home, WORKDURATION, priority: 0);
        return new StartActivityAction(_owner, this, workActivity, priority: 0);
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
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
