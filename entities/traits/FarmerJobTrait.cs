using System;
using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Activities;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for farmers who work at assigned farms during daytime (Dawn/Day).
/// At night, FarmerJobTrait returns null and VillagerTrait handles sleep behavior.
///
/// Inherits from JobTrait which enforces the pattern:
/// - Traits DECIDE when to work (via sealed SuggestAction)
/// - Activities EXECUTE the work (via CreateWorkActivity).
/// </summary>
public class FarmerJobTrait : JobTrait
{
    private const uint WORKDURATION = 1500; // ~3.1 real minutes, ~1.75 game hours per shift (2 shifts/day)

    /// <summary>
    /// Gets the activity type for farming work.
    /// </summary>
    protected override Type WorkActivityType => typeof(WorkFieldActivity);

    /// <summary>
    /// Gets use "farm" as the configuration key for the workplace.
    /// </summary>
    protected override string WorkplaceConfigKey => "farm";

    /// <summary>
    /// Gets desired resource stockpile for farmer's home.
    /// Farmers want to keep wheat harvested at home.
    /// </summary>
    public override IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;

    private static readonly Dictionary<string, int> _desiredResources = new ()
    {
        { "wheat", 10 } // Keep harvested wheat in stock
    };

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
    /// Resolves the building to its storage facility for the base _workplace field.
    /// </summary>
    public FarmerJobTrait(Building farm)
    {
        _workplace = farm.GetDefaultRoom()?.GetStorageFacility();
    }

    /// <summary>
    /// Create the farming work activity.
    /// The activity handles navigation, working, harvesting, and bringing wheat home.
    /// </summary>
    protected override Activity? CreateWorkActivity()
    {
        // _workplace is Facility? (from JobTrait base); we need its owner Building
        var workplaceBuilding = _workplace?.Owner;
        if (workplaceBuilding == null)
        {
            return null;
        }

        // Get farmer's home storage facility from HomeTrait
        var homeTrait = _owner?.SelfAsEntity().GetTrait<HomeTrait>();
        Facility? homeStorage = homeTrait?.Home?.GetStorageFacility();

        return new WorkFieldActivity(workplaceBuilding, homeStorage, WORKDURATION, priority: 0);
    }

    // ===== Dialogue Methods (not part of job pattern, kept in subclass) =====
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
