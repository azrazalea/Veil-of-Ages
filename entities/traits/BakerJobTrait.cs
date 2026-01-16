using System.Linq;
using Godot;
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
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            return null; // Let VillagerTrait handle night behavior
        }

        // Get workplace storage and facilities
        var storage = _workplace.GetStorage();
        if (storage == null)
        {
            return null;
        }

        var facilities = _workplace.GetFacilities();

        // Find a reaction we can perform (check in priority order)
        ReactionDefinition? selectedReaction = null;

        foreach (var tag in _reactionTags)
        {
            var taggedReactions = ReactionResourceManager.Instance.GetReactionsByTag(tag);

            foreach (var reaction in taggedReactions)
            {
                // Check if we have the required facilities
                bool hasFacilities = reaction.CanPerformWith(facilities);
                bool hasInputs = HasRequiredInputs(reaction, storage);

                if (!hasFacilities)
                {
                    continue;
                }

                // Check if we have the required inputs
                if (hasInputs)
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
            return null;
        }

        // Start processing the reaction
        DebugLog("BAKER", $"Starting reaction: {selectedReaction.Id}");
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
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
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
