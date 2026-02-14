using System;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Needs;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for the necromancer's nighttime study of dark arts.
/// Studies necromancy at the nearest necromancy_altar during Night phase only.
/// During Dawn/Day/Dusk, this trait returns null and other traits handle behavior.
///
/// Inherits from JobTrait which enforces the pattern:
/// - Traits DECIDE when to work (via sealed SuggestAction)
/// - Activities EXECUTE the work (via CreateWorkActivity)
///
/// Work order priority: If the necromancy_altar facility has an active work order,
/// a WorkOnOrderActivity is created instead of StudyNecromancyActivity.
///
/// Special behaviors:
/// - Will not start if energy is critical (allows sleep to take over)
/// - Finds the nearest necromancy_altar via Being.FindFacilityOfType.
/// </summary>
public class NecromancyStudyJobTrait : JobTrait
{
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec

    /// <summary>
    /// Gets the activity type for necromancy study work.
    /// Also checks for WorkOnOrderActivity since that can replace study.
    /// </summary>
    protected override Type WorkActivityType => typeof(StudyNecromancyActivity);

    /// <summary>
    /// Gets necromancy study happens during Night only (dark arts thrive in deepest darkness).
    /// </summary>
    protected override DayPhaseType[] WorkPhases => [DayPhaseType.Night];

    /// <summary>
    /// Gets necromancy study uses facility lookup, not a configured workplace.
    /// Config key is kept for compatibility but not required.
    /// </summary>
    protected override string WorkplaceConfigKey => "home";

    /// <summary>
    /// The workplace is dynamically resolved via FindFacilityOfType.
    /// Returns a dummy non-null value so JobTrait's sealed SuggestAction doesn't bail out
    /// on the workplace null check. The actual facility is resolved in CreateWorkActivity.
    /// </summary>
    protected override Building? GetWorkplace()
    {
        // We need to return non-null so JobTrait doesn't short-circuit.
        // Try to find the altar's building; fall back to null (which skips work).
        var facilityRef = _owner?.FindFacilityOfType("necromancy_altar");
        return facilityRef?.Building.Building;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NecromancyStudyJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// The altar is found dynamically via FindFacilityOfType.
    /// </summary>
    public NecromancyStudyJobTrait()
    {
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// The necromancy altar is found dynamically, so no configuration is strictly required.
    /// </summary>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        return true;
    }

    /// <summary>
    /// Configures the trait from a TraitConfiguration.
    /// No configuration needed — altar is found dynamically.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        // No-op: necromancy altar is resolved dynamically via FindFacilityOfType
    }

    /// <summary>
    /// Create the necromancy work activity.
    /// Priority: work orders on the altar first, then default to studying.
    /// Returns null if energy is critical or no altar is found.
    /// </summary>
    protected override Activity? CreateWorkActivity()
    {
        if (_owner == null)
        {
            return null;
        }

        // Don't start if energy is critical - let sleep take over
        var energyNeed = _owner.NeedsSystem?.GetNeed("energy");
        if (energyNeed != null && energyNeed.IsCritical())
        {
            DebugLog("NECROMANCYSTUDY", "Energy is critical, skipping to allow sleep");
            return null;
        }

        // Find nearest necromancy_altar facility
        var facilityRef = _owner.FindFacilityOfType("necromancy_altar");
        if (facilityRef == null)
        {
            DebugLog("NECROMANCYSTUDY", "No necromancy_altar facility found");
            return null;
        }

        // Check if already in a WorkOnOrderActivity (in addition to the WorkActivityType check)
        var currentActivity = _owner.GetCurrentActivity();
        if (currentActivity is WorkOnOrderActivity)
        {
            return null;
        }

        // Check for active work order on the altar — work orders take priority
        var building = facilityRef.Building.Building;
        if (building != null && GodotObject.IsInstanceValid(building))
        {
            var facility = building.GetFacility("necromancy_altar");
            if (facility?.ActiveWorkOrder != null)
            {
                DebugLog("NECROMANCYSTUDY", $"Active work order found on altar, starting WorkOnOrderActivity", 0);
                return new WorkOnOrderActivity(facilityRef, facility, priority: 0);
            }
        }

        // No work order — default to necromancy study
        return new StudyNecromancyActivity(facilityRef, WORKDURATION, priority: 0);
    }

    // ===== Dialogue Methods (not part of job pattern, kept in subclass) =====
    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is DayPhaseType.Night)
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
