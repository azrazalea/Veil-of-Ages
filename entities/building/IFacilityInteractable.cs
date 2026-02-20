using System;
using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

/// <summary>
/// Generic interface for anything an entity can interact with.
/// The interactable controls what happens on interaction via Interact().
/// Adjacency is checked via TryInteract before calling Interact.
/// The Dialogue handle is passed through so interactables can show UI as needed.
/// </summary>
public interface IInteractable
{
    bool RequiresAdjacency { get; }
    bool IsInteractorAdjacent(Being interactor);
    bool Interact(Being interactor, Dialogue dialogue);

    bool TryInteract(Being interactor, Dialogue dialogue)
    {
        if (RequiresAdjacency && !IsInteractorAdjacent(interactor))
        {
            return false;
        }

        return Interact(interactor, dialogue);
    }
}

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

    /// <summary>
    /// Gets the callback action for facility options that don't use the standard command system.
    /// Receives the interacting Being as parameter.
    /// </summary>
    public Action<Being>? FacilityAction { get; }

    public FacilityDialogueOption(string label, EntityCommand? command = null, bool enabled = true,
        string? disabledReason = null, Action<Being>? facilityAction = null)
    {
        Label = label;
        Command = command;
        Enabled = enabled;
        DisabledReason = disabledReason;
        FacilityAction = facilityAction;
    }
}

/// <summary>
/// Interface for facilities that can be interacted with through dialogue.
/// Facilities implement this to provide context-sensitive options to the player.
/// Default implementations delegate adjacency to Building.IsAdjacentToFacility
/// and interaction to Dialogue.ShowFacilityDialogue.
/// </summary>
public interface IFacilityInteractable : IInteractable
{
    /// <summary>
    /// Get the interaction options for this facility based on who is interacting.
    /// Disabled options include a DisabledReason explaining WHY they're disabled.
    /// </summary>
    List<FacilityDialogueOption> GetInteractionOptions(Being interactor);

    /// <summary>
    /// Gets a display name for this facility.
    /// </summary>
    string FacilityDisplayName { get; }

    /// <summary>
    /// Gets the facility this interactable is attached to.
    /// </summary>
    Facility Facility { get; }

    bool IInteractable.RequiresAdjacency => Facility.RequireAdjacent;

    bool IInteractable.IsInteractorAdjacent(Being interactor)
    {
        var interactorPos = interactor.GetCurrentGridPosition();
        var absolutePositions = Facility.GetAbsolutePositions();

        foreach (var facilityPos in absolutePositions)
        {
            // Check if interactor is at or adjacent to (including diagonals) the facility position
            if (interactorPos == facilityPos || DirectionUtils.IsAdjacent(interactorPos, facilityPos))
            {
                return true;
            }
        }

        return false;
    }

    bool IInteractable.Interact(Being interactor, Dialogue dialogue)
    {
        dialogue.ShowFacilityDialogue(interactor, this);
        return true;
    }
}
