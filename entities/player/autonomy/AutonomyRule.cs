using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Autonomy;

/// <summary>
/// A single rule in the autonomy configuration system.
/// Each rule maps to a trait type that should be present on the player.
/// The autonomy system adds/removes/configures traits based on these rules.
/// </summary>
public class AutonomyRule
{
    /// <summary>
    /// Gets unique identifier for this rule (e.g., "study_research", "study_necromancy").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets human-readable name for UI display.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the trait class name this rule manages (e.g., "ScholarJobTrait").
    /// </summary>
    public string TraitType { get; }

    /// <summary>
    /// Gets or sets priority order for display and evaluation. Lower values shown first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether whether this rule is currently active. Toggle without deleting.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets day phases during which this rule's trait is active. Informational for UI display.
    /// The trait itself defines its own work phases - this field records the player's intent.
    /// Null means "any time".
    /// </summary>
    public DayPhaseType[] ? ActiveDuringPhases { get; set; }

    public AutonomyRule(string id, string displayName, string traitType, int priority, DayPhaseType[] ? activeDuringPhases = null)
    {
        Id = id;
        DisplayName = displayName;
        TraitType = traitType;
        Priority = priority;
        ActiveDuringPhases = activeDuringPhases;
    }
}
