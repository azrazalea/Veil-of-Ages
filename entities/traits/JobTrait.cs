using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Base class for job traits that work at a building during specific hours.
/// Job traits DECIDE when to work by starting activities - they never access storage
/// or manage navigation directly.
///
/// The SuggestAction() method is sealed to enforce the pattern.
/// Subclasses implement CreateWorkActivity() to define the actual work.
///
/// Key Design Principle: Traits DECIDE, Activities EXECUTE.
/// - Traits check if work should happen (time, not busy, etc.)
/// - Activities handle navigation, storage access, and multi-step work
/// - This prevents bugs like accessing storage before arriving at the building.
/// </summary>
public abstract class JobTrait : BeingTrait, IDesiredResources
{
    /// <summary>
    /// The workplace building. Set via configuration or constructor.
    /// For most jobs, this is the place of work (farm, bakery, etc.).
    /// ScholarJobTrait overrides GetWorkplace() to use home instead.
    /// </summary>
    protected Building? _workplace;

    /// <summary>
    /// Gets default work phases are Dawn and Day. Override to customize.
    /// </summary>
    protected virtual DayPhaseType[] WorkPhases => [DayPhaseType.Dawn, DayPhaseType.Day];

    /// <summary>
    /// Gets the activity type that represents "working" for this job.
    /// Used by the default IsWorkActivity() check.
    /// </summary>
    protected abstract Type WorkActivityType { get; }

    /// <summary>
    /// Check if the given activity counts as "working" for this job.
    /// Default checks WorkActivityType. Override for jobs that produce
    /// multiple activity types (e.g., NecromancyStudyJobTrait can produce
    /// both StudyNecromancyActivity and WorkOnOrderActivity).
    /// </summary>
    protected virtual bool IsWorkActivity(Activity activity)
    {
        return WorkActivityType.IsInstanceOfType(activity);
    }

    /// <summary>
    /// Create the work activity for this job. This is the ONLY way to define
    /// work behavior - subclasses cannot access storage or navigate directly.
    /// </summary>
    /// <returns>The activity to start, or null if work cannot start.</returns>
    protected abstract Activity? CreateWorkActivity();

    /// <summary>
    /// Gets the workplace for this job. Default returns _workplace field.
    /// Override for special cases like ScholarJobTrait which uses home.
    /// </summary>
    protected virtual Building? GetWorkplace() => _workplace;

    /// <summary>
    /// Gets the configuration key used to get the workplace from TraitConfiguration.
    /// Default is "workplace". FarmerJobTrait overrides to "farm".
    /// </summary>
    protected virtual string WorkplaceConfigKey => "workplace";

    /// <summary>
    /// Gets optional: Specify desired resource levels for home storage.
    /// Override in subclasses that need resources stockpiled at home.
    /// Jobs that don't need resources can leave this as the empty dictionary.
    /// </summary>
    public virtual IReadOnlyDictionary<string, int> DesiredResources => _emptyDesiredResources;

    private static readonly Dictionary<string, int> _emptyDesiredResources = new ();

    /// <summary>
    /// Sealed SuggestAction enforces the job pattern.
    /// Subclasses cannot override this - they must use CreateWorkActivity() instead.
    /// This prevents the baker bug where traits accessed storage directly.
    /// </summary>
    public sealed override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        // Get the workplace (may be overridden in subclasses)
        var workplace = GetWorkplace();

        // Validate owner and workplace
        if (_owner == null || workplace == null || !GodotObject.IsInstanceValid(workplace))
        {
            return null;
        }

        // Don't interrupt movement
        if (_owner.IsMoving())
        {
            return null;
        }

        // Check if already in work activity
        var currentActivity = _owner.GetCurrentActivity();
        if (currentActivity != null && IsWorkActivity(currentActivity))
        {
            return null;
        }

        // Check work hours
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (!IsWorkHours(gameTime.CurrentDayPhase))
        {
            DebugLog(GetJobName(), $"Not work hours ({gameTime.CurrentDayPhase}), deferring");
            return null;
        }

        // Create the work activity
        var workActivity = CreateWorkActivity();
        if (workActivity == null)
        {
            return null;
        }

        DebugLog(GetJobName(), $"Starting work activity: {workActivity.DisplayName}", 0);
        return new StartActivityAction(_owner, this, workActivity, priority: 0);
    }

    /// <summary>
    /// Check if the current day phase is a work phase.
    /// </summary>
    public bool IsWorkHours(DayPhaseType currentPhase)
    {
        foreach (var phase in WorkPhases)
        {
            if (phase == currentPhase)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get a human-readable job name for debug logging.
    /// Override for custom names, default is class name without "JobTrait".
    /// </summary>
    protected virtual string GetJobName()
    {
        var typeName = GetType().Name;
        if (typeName.EndsWith("JobTrait", StringComparison.Ordinal))
        {
            return typeName[..^8].ToUpperInvariant();
        }

        return typeName.ToUpperInvariant();
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Default implementation checks for workplace. Override for custom validation.
    /// </summary>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // If workplace already set via constructor, we're good
        if (_workplace != null)
        {
            return true;
        }

        // Check for workplace in config
        if (config.GetBuilding(WorkplaceConfigKey) == null && config.GetBuilding("workplace") == null)
        {
            Log.Warn($"{GetType().Name}: '{WorkplaceConfigKey}' or 'workplace' parameter recommended for proper function");
        }

        return true; // Don't fail - we handle missing workplace gracefully
    }

    /// <summary>
    /// Configures the trait from a TraitConfiguration.
    /// Default implementation sets workplace. Override for custom configuration.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        // If workplace already set via constructor, skip configuration
        if (_workplace != null)
        {
            return;
        }

        // Try custom key first, then "workplace" as fallback
        _workplace = config.GetBuilding(WorkplaceConfigKey) ?? config.GetBuilding("workplace");
    }
}
