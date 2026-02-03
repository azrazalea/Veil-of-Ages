using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for the village scholar/wiseperson (player's daytime occupation).
/// Scholars study at their home/study during daytime (Dawn/Day).
/// At night, ScholarJobTrait returns null and PlayerBehaviorTrait handles night behavior.
/// </summary>
public class ScholarJobTrait : BeingTrait
{
    private Building? _home;
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec

    /// <summary>
    /// Initializes a new instance of the <see cref="ScholarJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via Configure() method.
    /// </summary>
    public ScholarJobTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScholarJobTrait"/> class.
    /// Constructor for direct instantiation with a home/study building.
    /// </summary>
    public ScholarJobTrait(Building home)
    {
        _home = home;
    }

    /// <summary>
    /// Sets the home/study building where the scholar conducts their research.
    /// </summary>
    /// <param name="home">The building to use as the study location.</param>
    public void SetHome(Building home)
    {
        _home = home;
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "home" (Building): The home/study building to work at (recommended but optional).
    /// </summary>
    /// <remarks>
    /// If no home is provided, the trait will be non-functional but won't crash.
    /// The scholar will simply not suggest any study actions until a home is assigned.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // If already set via constructor, we're good
        if (_home != null)
        {
            return true;
        }

        // home is recommended but we handle null gracefully in SuggestAction()
        if (config.GetBuilding("home") == null)
        {
            Log.Warn("ScholarJobTrait: 'home' parameter recommended for proper function");
        }

        return true; // Don't fail - we handle missing home gracefully
    }

    /// <summary>
    /// Configures the trait from a TraitConfiguration.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        // If already set via constructor, skip configuration
        if (_home != null)
        {
            return;
        }

        _home = config.GetBuilding("home");
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || _home == null || !GodotObject.IsInstanceValid(_home))
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

        // If already studying, let the activity handle things
        if (currentActivity is StudyActivity)
        {
            return null;
        }

        // Only study during work hours (Dawn/Day)
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            DebugLog("SCHOLAR", $"Not work hours ({gameTime.CurrentDayPhase}), deferring to PlayerBehaviorTrait");
            return null; // Let PlayerBehaviorTrait handle night behavior
        }

        // Start study activity at home
        var studyActivity = new StudyActivity(_home, WORKDURATION, priority: 0);
        return new StartActivityAction(_owner, this, studyActivity, priority: 0);
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Day)
        {
            return "I cannot chat long, I have my studies to attend to.";
        }

        return "Good evening. The stars hold many secrets.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am the village scholar, keeper of knowledge both mundane and... arcane.";
    }
}
