using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait for undead that hide in their home graveyard during daylight hours.
/// Handles the timing logic for hiding at dawn and emerging at dusk.
/// Undead can still eat and satisfy needs while hidden.
/// </summary>
public class DormantUndeadTrait : BeingTrait
{
    /// <summary>
    /// Gets or sets which day phases trigger hiding (default: Day, Dawn).
    /// </summary>
    public List<DayPhaseType> HidePhases { get; set; } = [DayPhaseType.Day, DayPhaseType.Dawn];

    /// <summary>
    /// Gets or sets which day phases allow emergence (default: Night, Dusk).
    /// </summary>
    public List<DayPhaseType> EmergePhases { get; set; } = [DayPhaseType.Night, DayPhaseType.Dusk];

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);
        IsInitialized = true;
    }

    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        // Optional: Configure phases from JSON
        // hidePhases: ["Day", "Dawn"]
        // emergePhases: ["Night", "Dusk"]
        var hidePhases = config.Get<List<string>>("hidePhases");
        if (hidePhases != null && hidePhases.Count > 0)
        {
            HidePhases.Clear();
            foreach (var phaseStr in hidePhases)
            {
                if (System.Enum.TryParse<DayPhaseType>(phaseStr, out var phase))
                {
                    HidePhases.Add(phase);
                }
            }
        }

        var emergePhases = config.Get<List<string>>("emergePhases");
        if (emergePhases != null && emergePhases.Count > 0)
        {
            EmergePhases.Clear();
            foreach (var phaseStr in emergePhases)
            {
                if (System.Enum.TryParse<DayPhaseType>(phaseStr, out var phase))
                {
                    EmergePhases.Add(phase);
                }
            }
        }
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        var currentPhase = gameTime.CurrentDayPhase;

        var home = _owner.SelfAsEntity().GetTrait<HomeTrait>()?.HomeBuilding;
        var currentActivity = _owner.GetCurrentActivity();

        // If hidden and should emerge
        if (_owner.IsHidden && EmergePhases.Contains(currentPhase))
        {
            // Already emerging?
            if (currentActivity is EmergeFromHidingActivity)
            {
                return null;
            }

            DebugLog("DORMANT", $"Time to emerge ({currentPhase})", 0);
            var emergeActivity = new EmergeFromHidingActivity(priority: -1);
            return new StartActivityAction(_owner, this, emergeActivity, priority: -1);
        }

        // If hidden during hide phases, stay dormant (block all other actions)
        if (_owner.IsHidden && HidePhases.Contains(currentPhase))
        {
            return new IdleAction(_owner, this, priority: -10);
        }

        // If not hidden and should hide
        if (!_owner.IsHidden && HidePhases.Contains(currentPhase))
        {
            // Already retreating or hiding?
            if (currentActivity is HideInBuildingActivity)
            {
                return null;
            }

            if (home == null || !GodotObject.IsInstanceValid(home))
            {
                DebugLog("DORMANT", $"Should hide but no valid home graveyard", 0);
                return null;
            }

            DebugLog("DORMANT", $"Time to hide ({currentPhase}), retreating to {home.BuildingName}", 0);
            var hideActivity = new HideInBuildingActivity(home, priority: -1);
            return new StartActivityAction(_owner, this, hideActivity, priority: -1);
        }

        return null;
    }
}
