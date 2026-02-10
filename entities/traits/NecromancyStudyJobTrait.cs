using System;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Needs;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for the necromancer's nighttime study of dark arts.
/// Studies necromancy at home during Dusk/Night phases.
/// During Dawn/Day, this trait returns null and ScholarJobTrait handles daytime work.
///
/// Inherits from JobTrait which enforces the pattern:
/// - Traits DECIDE when to work (via sealed SuggestAction)
/// - Activities EXECUTE the work (via CreateWorkActivity)
///
/// Special behaviors:
/// - Will not start studying if energy is low (allows PlayerBehaviorTrait's sleep to take over)
/// - Uses home as workplace (same pattern as ScholarJobTrait).
/// </summary>
public class NecromancyStudyJobTrait : JobTrait
{
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec

    // Uses home as workplace - set via Configure or SetHome
    private Building? _home;

    /// <summary>
    /// Gets the activity type for necromancy study work.
    /// </summary>
    protected override Type WorkActivityType => typeof(StudyNecromancyActivity);

    /// <summary>
    /// Gets necromancy study happens during Dusk and Night (dark arts thrive in darkness).
    /// </summary>
    protected override DayPhaseType[] WorkPhases => [DayPhaseType.Dusk, DayPhaseType.Night];

    /// <summary>
    /// Gets necromancy study uses "home" as the configuration key.
    /// </summary>
    protected override string WorkplaceConfigKey => "home";

    /// <summary>
    /// Necromancer studies at their home.
    /// </summary>
    protected override Building? GetWorkplace() => _home;

    /// <summary>
    /// Initializes a new instance of the <see cref="NecromancyStudyJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via Configure() method or SetHome().
    /// </summary>
    public NecromancyStudyJobTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NecromancyStudyJobTrait"/> class.
    /// Constructor for direct instantiation with a home/study building.
    /// </summary>
    public NecromancyStudyJobTrait(Building home)
    {
        _home = home;
    }

    /// <summary>
    /// Sets the home building where the necromancer studies dark arts.
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
            Log.Warn("NecromancyStudyJobTrait: 'home' parameter recommended for proper function");
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
    /// Create the necromancy study activity.
    /// Returns null if energy is low to allow sleep to take over.
    /// The activity handles navigation to home and studying dark arts.
    /// </summary>
    protected override Activity? CreateWorkActivity()
    {
        // Don't start studying if too tired - let PlayerBehaviorTrait's sleep take over
        var energyNeed = _owner?.NeedsSystem?.GetNeed("energy");
        if (energyNeed != null && energyNeed.IsLow())
        {
            DebugLog("NECROMANCYSTUDY", "Energy is low, skipping study to allow sleep");
            return null;
        }

        return new StudyNecromancyActivity(_home!, WORKDURATION, priority: 0);
    }

    // ===== Dialogue Methods (not part of job pattern, kept in subclass) =====
    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dusk or DayPhaseType.Night)
        {
            return "The veil between worlds grows thin at this hour... I must not be disturbed.";
        }

        return "The dark arts demand patience. Night will come soon enough.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I delve into the forbidden art of necromancy, communing with the dead under cover of darkness.";
    }
}
