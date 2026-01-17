using System.Collections.Generic;
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

    // Cooldown to prevent constant storage checking when no inputs are available
    private uint _lastStorageCheckTick;
    private const uint STORAGECHECKCOOLDOWN = 500; // ~1 minute game time at 8 ticks/sec

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

        // If already checking storage, let the activity handle things
        if (_owner.GetCurrentActivity() is CheckHomeStorageActivity)
        {
            DebugLog("BAKER", "Already checking storage, waiting for observation to complete");
            return null;
        }

        // Only work during work hours (Dawn/Day)
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            return null; // Let VillagerTrait handle night behavior
        }

        // Get workplace storage using wrapper method (auto-observes contents)
        var storage = _owner.AccessStorage(_workplace);
        if (storage == null)
        {
            return null;
        }

        var facilities = _workplace.GetFacilities();

        // Find a reaction we can perform (check in priority order)
        ReactionDefinition? selectedReaction = null;
        List<string> missingInputItemIds = [];

        foreach (var tag in _reactionTags)
        {
            var taggedReactions = ReactionResourceManager.Instance.GetReactionsByTag(tag);

            foreach (var reaction in taggedReactions)
            {
                // Check if we have the required facilities
                bool hasFacilities = reaction.CanPerformWith(facilities);
                bool hasInputs = HasRequiredInputs(reaction);

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
                else
                {
                    // Track which inputs are missing for potential storage check
                    foreach (var input in reaction.Inputs ?? [])
                    {
                        if (!string.IsNullOrEmpty(input.ItemId) && !missingInputItemIds.Contains(input.ItemId))
                        {
                            missingInputItemIds.Add(input.ItemId);
                        }
                    }
                }
            }

            if (selectedReaction != null)
            {
                break;
            }
        }

        if (selectedReaction != null)
        {
            // Start processing the reaction
            DebugLog("BAKER", $"Starting reaction: {selectedReaction.Id}");
            var processActivity = new ProcessReactionActivity(selectedReaction, _workplace, storage, priority: 0);
            return new StartActivityAction(_owner, this, processActivity, priority: 0);
        }

        // No reaction available - check if we should go observe the workplace storage
        // This happens when we don't remember any of the input items we need
        var currentTick = GameController.CurrentTick;
        if (missingInputItemIds.Count > 0 && ShouldCheckStorage(missingInputItemIds, currentTick))
        {
            DebugLog("BAKER", $"No memory of required inputs ({string.Join(", ", missingInputItemIds)}), going to check workplace storage", 0);
            _lastStorageCheckTick = currentTick; // Record when we started checking
            var checkActivity = new CheckHomeStorageActivity(_workplace, priority: 0);
            return new StartActivityAction(_owner, this, checkActivity, priority: 0);
        }

        // Nothing to do - idle (waiting for inputs to arrive or cooldown to expire)
        return null;
    }

    /// <summary>
    /// Check if we should go observe the workplace storage.
    /// Returns true if we don't remember any of the given items being available
    /// AND enough time has passed since our last check.
    /// </summary>
    private bool ShouldCheckStorage(List<string> itemIds, uint currentTick)
    {
        if (_owner?.Memory == null)
        {
            return true; // No memory system, should check
        }

        // If we remember any of the required items being available, no need to check
        foreach (var itemId in itemIds)
        {
            if (_owner.Memory.RemembersItemAvailableById(itemId))
            {
                DebugLog("BAKER", $"Remembers {itemId} being available somewhere");
                return false; // We remember at least one item, no need to check yet
            }
        }

        // We don't remember any of the required items - but check cooldown first
        // This prevents constantly checking storage when items aren't available
        if (currentTick - _lastStorageCheckTick < STORAGECHECKCOOLDOWN)
        {
            DebugLog("BAKER", $"Storage check on cooldown ({currentTick - _lastStorageCheckTick}/{STORAGECHECKCOOLDOWN} ticks)");
            return false; // Too soon to check again, idle instead
        }

        // Cooldown expired - go check storage
        return true;
    }

    /// <summary>
    /// Check if storage has all required inputs for a reaction.
    /// Uses Being wrapper methods so the baker remembers what's in storage.
    /// </summary>
    private bool HasRequiredInputs(ReactionDefinition reaction)
    {
        if (_owner == null || reaction.Inputs == null || reaction.Inputs.Count == 0)
        {
            return reaction.Inputs == null || reaction.Inputs.Count == 0;
        }

        foreach (var input in reaction.Inputs)
        {
            // Use wrapper method which auto-observes storage contents
            if (!_owner.StorageHasItem(_workplace, input.ItemId, input.Quantity))
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
