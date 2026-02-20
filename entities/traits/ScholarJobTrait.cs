using System;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Activities;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for the village scholar/wiseperson (player's daytime occupation).
/// Scholars study at their home/study during daytime (Dawn/Day).
/// At night, ScholarJobTrait returns null and ScheduleTrait/NecromancyStudyJobTrait handle night behavior.
///
/// Inherits from JobTrait which enforces the pattern:
/// - Traits DECIDE when to work (via sealed SuggestAction)
/// - Activities EXECUTE the work (via CreateWorkActivity)
///
/// Special case: Scholars use their home as the workplace, so GetWorkplace()
/// is overridden to return the home from HomeTrait instead of _workplace.
/// </summary>
public class ScholarJobTrait : JobTrait
{
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec

    // Scholar uses home as workplace - this is set via Configure or SetHome
    private Building? _home;

    /// <summary>
    /// Gets the activity type for studying work.
    /// </summary>
    protected override Type WorkActivityType => typeof(StudyActivity);

    /// <summary>
    /// Gets scholars use "home" as the configuration key.
    /// </summary>
    protected override string WorkplaceConfigKey => "home";

    /// <summary>
    /// Scholars use their home as the workplace.
    /// </summary>
    protected override Facility? GetWorkplace() => _home?.GetDefaultRoom()?.GetStorageFacility();

    /// <summary>
    /// Initializes a new instance of the <see cref="ScholarJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via Configure() method or SetHome().
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
    /// </summary>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // If already set via constructor, we're good
        if (_home != null)
        {
            return true;
        }

        // home is recommended but we handle null gracefully
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
        // If already set via constructor or SetHome, skip configuration
        if (_home != null)
        {
            return;
        }

        _home = config.GetBuilding("home");
    }

    /// <summary>
    /// Create the study activity.
    /// The activity handles navigation to home and studying.
    /// </summary>
    protected override Activity? CreateWorkActivity()
    {
        return new StudyActivity(_home!, WORKDURATION, priority: 0);
    }

    // ===== Dialogue Methods (not part of job pattern, kept in subclass) =====
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
