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
    private Building? _assignedFarm;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="FarmerJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Farm must be configured via Configure() method.
    /// </summary>
    public FarmerJobTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FarmerJobTrait"/> class.
    /// Constructor for direct instantiation with a farm building.
    /// </summary>
    public FarmerJobTrait(Building farm)
    {
        _assignedFarm = farm;
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "farm" or "workplace" (Building): The farm building to work at (recommended but optional).
    /// </summary>
    /// <remarks>
    /// If no farm/workplace is provided, the trait will be non-functional but won't crash.
    /// The farmer will simply not suggest any work actions until a farm is assigned.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // If already set via constructor, we're good
        if (_assignedFarm != null)
        {
            return true;
        }

        // workplace is recommended but we handle null gracefully in SuggestAction()
        if (config.GetBuilding("farm") == null && config.GetBuilding("workplace") == null)
        {
            Log.Warn("FarmerJobTrait: 'farm' or 'workplace' parameter recommended for proper function");
        }

        return true; // Don't fail - we handle missing workplace gracefully
    }

    /// <summary>
    /// Configures the trait from a TraitConfiguration.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        // If already set via constructor, skip configuration
        if (_assignedFarm != null)
        {
            return;
        }

        // Try "farm" first, then "workplace" as fallback
        _assignedFarm = config.GetBuilding("farm") ?? config.GetBuilding("workplace");
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || _assignedFarm == null || !GodotObject.IsInstanceValid(_assignedFarm))
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

        // Get farmer's home from HomeTrait
        var homeTrait = _owner.SelfAsEntity().GetTrait<HomeTrait>();
        Building? home = homeTrait?.Home;

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
