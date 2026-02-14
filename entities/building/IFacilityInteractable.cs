using System.Collections.Generic;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

/// <summary>
/// A single interaction option for a facility.
/// </summary>
public class FacilityDialogueOption
{
    /// <summary>
    /// Gets the display text for this option.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the command to execute when this option is selected, or null for info-only options.
    /// </summary>
    public EntityCommand? Command { get; }

    /// <summary>
    /// Gets a value indicating whether gets whether this option is currently available.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the reason this option is disabled, shown as tooltip. Null if enabled.
    /// </summary>
    public string? DisabledReason { get; }

    public FacilityDialogueOption(string label, EntityCommand? command = null, bool enabled = true, string? disabledReason = null)
    {
        Label = label;
        Command = command;
        Enabled = enabled;
        DisabledReason = disabledReason;
    }
}

/// <summary>
/// Interface for facilities that can be interacted with through dialogue.
/// Facilities implement this to provide context-sensitive options to the player.
/// </summary>
public interface IFacilityInteractable
{
    /// <summary>
    /// Get the interaction options for this facility based on who is interacting.
    /// Disabled options include a DisabledReason explaining WHY they're disabled.
    /// </summary>
    List<FacilityDialogueOption> GetInteractionOptions(Being interactor);

    /// <summary>
    /// Gets get a display name for this facility.
    /// </summary>
    string FacilityDisplayName { get; }
}
