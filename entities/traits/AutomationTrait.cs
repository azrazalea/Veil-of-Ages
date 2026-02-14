using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that allows toggling between automated and manual behavior.
/// When manual mode is active (IsAutomated = false), autonomous trait
/// SuggestAction() calls are suppressed in Being.Think().
/// Exception: critical needs (.<=20) force automated behavior.
///
/// NPC-compatible: any entity can have this trait, not just the player.
/// An NPC with this trait could be set to manual mode by their master.
/// </summary>
public class AutomationTrait : BeingTrait
{
    /// <summary>
    /// Gets or sets a value indicating whether this entity runs autonomously.
    /// When true (default): traits suggest actions normally.
    /// When false (manual): trait SuggestAction() is suppressed, entity only executes commands.
    /// Exception: critical needs override manual mode.
    /// </summary>
    public bool IsAutomated { get; set; } = true;

    /// <summary>
    /// Toggle between automated and manual mode.
    /// Returns the new state.
    /// </summary>
    public bool Toggle()
    {
        IsAutomated = !IsAutomated;
        Log.Print($"{_owner?.Name}: Automation {(IsAutomated ? "ON" : "OFF")}");
        return IsAutomated;
    }

    /// <summary>
    /// Check if any need is critical (at or below critical threshold),
    /// which would force automation even in manual mode.
    /// </summary>
    public bool HasCriticalNeed()
    {
        if (_owner?.NeedsSystem == null)
        {
            return false;
        }

        foreach (var need in _owner.NeedsSystem.GetAllNeeds())
        {
            if (need.IsCritical())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether trait actions should be suppressed this tick.
    /// Returns true when in manual mode AND no critical needs.
    /// Returns false when in auto mode OR when critical needs override manual mode.
    /// </summary>
    public bool ShouldSuppressTraits()
    {
        if (IsAutomated)
        {
            return false; // Auto mode - don't suppress
        }

        return !HasCriticalNeed(); // Manual mode - suppress unless critical need
    }

    /// <summary>
    /// This trait doesn't suggest actions itself - it modifies how Being.Think()
    /// processes other traits via ShouldSuppressTraits().
    /// </summary>
    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        return null;
    }
}
