using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for bakers. Bakers work at their assigned workplace during daytime (Dawn/Day).
/// They find reactions with "baking" or "milling" tags and process them.
/// When missing water for baking, they will fetch water from the village well.
/// At night, BakerJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class BakerJobTrait : BeingTrait, IDesiredResources
{
    private readonly Building _workplace;

    // Desired resource stockpile for baker's home
    // Bakers want flour for baking, water for dough, and bread as finished product
    private static readonly Dictionary<string, int> _desiredResources = new ()
    {
        { "flour", 5 },   // Need flour for baking bread
        { "water", 10 },  // Need water for dough
        { "bread", 5 } // Keep some bread in stock
    };

    /// <summary>
    /// Gets the desired resource levels for the baker's home storage.
    /// Bakers want to stockpile flour, water, and bread.
    /// </summary>
    public IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;

    // Reaction tags this baker can handle, in priority order (first = highest priority)
    private static readonly string[] _reactionTags = ["baking", "milling"];

    // Cooldown to prevent constant storage checking when no inputs are available
    private uint _lastStorageCheckTick;
    private const uint STORAGECHECKCOOLDOWN = 500; // ~1 minute game time at 8 ticks/sec

    // Water management - how much water to maintain at the workplace for baking
    private const int DESIREDWATERATWORKPLACE = 10;
    private const int WATERFETCHAMOUNT = 5;

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

        // If already fetching resources, let the activity handle things
        if (_owner.GetCurrentActivity() is FetchResourceActivity)
        {
            DebugLog("BAKER", "Already fetching resources, waiting for fetch to complete");
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

        // No reaction available - check if water is needed and we can fetch it
        // This takes priority over just waiting because it's proactive resource gathering
        var waterFetchAction = TryFetchWaterIfNeeded(missingInputItemIds);
        if (waterFetchAction != null)
        {
            return waterFetchAction;
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

    /// <summary>
    /// Check if water is needed at the workplace and fetch it from the well if available.
    /// Returns a StartActivityAction if water fetching should start, null otherwise.
    /// </summary>
    /// <param name="missingInputs">List of missing input item IDs from reaction checks.</param>
    private EntityAction? TryFetchWaterIfNeeded(List<string> missingInputs)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if water is one of the missing inputs
        if (!missingInputs.Contains("water"))
        {
            return null;
        }

        // Check current water level at workplace (from memory)
        int currentWater = _owner.GetStorageItemCount(_workplace, "water");
        if (currentWater >= DESIREDWATERATWORKPLACE)
        {
            // We have enough water
            return null;
        }

        // We need water - try to find a well via SharedKnowledge
        if (!_owner.TryFindBuildingOfType("Well", out BuildingReference? wellRef) || wellRef?.Building == null)
        {
            DebugLog("BAKER", "Need water but no well found via SharedKnowledge");
            return null;
        }

        var well = wellRef.Building;

        // Check if well has water available (from memory or go check it)
        int wellWater = _owner.GetStorageItemCount(well, "water");
        if (wellWater <= 0)
        {
            // We don't remember seeing water at the well, but the well regenerates water
            // Let's go check it anyway - if we have no memory or stale memory, assume there might be water
            var wellMemory = _owner.Memory?.RecallStorageContents(well);
            if (wellMemory == null)
            {
                // No memory of well - let's go check it
                DebugLog("BAKER", "Need water, no memory of well storage, going to check well", 0);
                var checkWellActivity = new CheckHomeStorageActivity(well, priority: 0);
                return new StartActivityAction(_owner, this, checkWellActivity, priority: 0);
            }

            // We have memory of the well being empty - wait for water to regenerate
            DebugLog("BAKER", "Need water but remember well being empty, waiting for regeneration");
            return null;
        }

        // Calculate how much water to fetch
        int amountToFetch = System.Math.Min(WATERFETCHAMOUNT, DESIREDWATERATWORKPLACE - currentWater);
        amountToFetch = System.Math.Max(1, amountToFetch); // At least try to get 1

        DebugLog("BAKER", $"Workplace water low ({currentWater}/{DESIREDWATERATWORKPLACE}), fetching {amountToFetch} from well", 0);

        // Start fetch activity
        var fetchActivity = new FetchResourceActivity(well, _workplace, "water", amountToFetch, priority: 0);
        return new StartActivityAction(_owner, this, fetchActivity, priority: 0);
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
