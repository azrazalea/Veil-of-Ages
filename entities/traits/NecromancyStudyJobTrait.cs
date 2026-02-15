using System;
using Godot;
using VeilOfAges.Core;
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
/// - Finds the nearest necromancy_altar via Being.FindFacilityOfType (cached with TTL).
/// </summary>
public class NecromancyStudyJobTrait : JobTrait
{
    private const uint WORKDURATION = 400; // ~50 seconds real time at 8 ticks/sec
    private const uint FACILITYCACHETTL = 200; // Re-lookup facility every ~25 seconds

    // Cached facility reference to avoid FindFacilityOfType every tick
    private FacilityReference? _cachedFacilityRef;
    private uint _facilityCacheTick;

    /// <summary>
    /// Gets the activity type for necromancy study work.
    /// </summary>
    protected override Type WorkActivityType => typeof(StudyNecromancyActivity);

    /// <summary>
    /// Check if the given activity counts as "working" for this job.
    /// Necromancy study can produce either StudyNecromancyActivity or WorkOnOrderActivity.
    /// </summary>
    protected override bool IsWorkActivity(Activity activity)
    {
        return activity is StudyNecromancyActivity or WorkOnOrderActivity;
    }

    /// <summary>
    /// Gets necromancy study happens during Night only (dark arts thrive in deepest darkness).
    /// </summary>
    protected override DayPhaseType[] WorkPhases => [DayPhaseType.Night];

    /// <summary>
    /// The workplace is dynamically resolved via FindFacilityOfType (cached).
    /// Returns a non-null value so JobTrait's sealed SuggestAction doesn't bail out
    /// on the workplace null check. The actual facility is resolved in CreateWorkActivity.
    /// </summary>
    protected override Building? GetWorkplace()
    {
        var facilityRef = GetCachedFacilityRef();
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

        // Use cached facility reference (avoids FindFacilityOfType every tick)
        var facilityRef = GetCachedFacilityRef();
        if (facilityRef == null)
        {
            DebugLog("NECROMANCYSTUDY", "No necromancy_altar facility found");
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
                return new WorkOnOrderActivity(facilityRef, facility, priority: 0, allowedPhases: [DayPhaseType.Night]);
            }
        }

        // No work order — default to necromancy study
        return new StudyNecromancyActivity(facilityRef, WORKDURATION, priority: 0);
    }

    /// <summary>
    /// Get the cached facility reference, refreshing if the TTL has expired.
    /// Avoids calling FindFacilityOfType every tick (GC pressure reduction).
    /// </summary>
    private FacilityReference? GetCachedFacilityRef()
    {
        uint currentTick = GameController.CurrentTick;
        if (_cachedFacilityRef == null || (currentTick - _facilityCacheTick) >= FACILITYCACHETTL)
        {
            _cachedFacilityRef = _owner?.FindFacilityOfType("necromancy_altar");
            _facilityCacheTick = currentTick;
        }

        return _cachedFacilityRef;
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
