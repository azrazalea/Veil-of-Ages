using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for bakers. Bakers work at their assigned workplace during daytime (Dawn/Day).
/// They find reactions with "baking" or "milling" tags and process them.
/// At night, BakerJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class BakerJobTrait : BeingTrait
{
    private readonly Building _workplace;

    // Reaction tags this baker can handle, in priority order (first = highest priority)
    private static readonly string[] _reactionTags = ["baking", "milling"];

    public BakerJobTrait(Building workplace)
    {
        _workplace = workplace;
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_workplace))
        {
            return null;
        }

        // Don't interrupt movement
        if (_owner.IsMoving())
        {
            return null;
        }

        // If already processing a reaction, let the activity handle things
        if (_owner.GetCurrentActivity() is ProcessReactionActivity)
        {
            return null;
        }

        // Only work during work hours (Dawn/Day)
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            return null; // Let VillagerTrait handle night behavior
        }

        // Get workplace storage and facilities
        var storage = _workplace.GetStorage();
        if (storage == null)
        {
            Log.Warn($"{_owner.Name}: Workplace {_workplace.BuildingName} has no storage");
            return null;
        }

        var facilities = _workplace.GetFacilities();

        // Find a reaction we can perform (check in priority order)
        ReactionDefinition? selectedReaction = null;

        foreach (var tag in _reactionTags)
        {
            foreach (var reaction in ReactionResourceManager.Instance.GetReactionsByTag(tag))
            {
                // Check if we have the required facilities
                if (!reaction.CanPerformWith(facilities))
                {
                    continue;
                }

                // Check if we have the required inputs
                if (HasRequiredInputs(reaction, storage))
                {
                    selectedReaction = reaction;
                    break;
                }
            }

            if (selectedReaction != null)
            {
                break;
            }
        }

        if (selectedReaction == null)
        {
            // No reaction available, just idle
            return new IdleAction(_owner, this, priority: 1);
        }

        // Start processing the reaction
        var processActivity = new ProcessReactionActivity(selectedReaction, _workplace, storage, priority: 0);
        return new StartActivityAction(_owner, this, processActivity, priority: 0);
    }

    /// <summary>
    /// Check if storage has all required inputs for a reaction.
    /// </summary>
    private static bool HasRequiredInputs(ReactionDefinition reaction, StorageTrait storage)
    {
        if (reaction.Inputs == null || reaction.Inputs.Count == 0)
        {
            return true;
        }

        foreach (var input in reaction.Inputs)
        {
            if (!storage.HasItem(input.ItemId, input.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Day)
        {
            return $"Morning, {speaker.Name}! The mill waits for no one.";
        }

        return $"Evening, {speaker.Name}. Bread's baked for tomorrow.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am a baker.";
    }
}
