using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Reactions;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for bakers who work at their assigned workplace during daytime (Dawn/Day).
/// Bakers use the reaction system to mill wheat into flour and bake bread.
/// When missing water for baking, they proactively fetch water from the village well.
/// At night, returns null to let VillagerTrait handle sleep behavior.
///
/// Inherits from JobTrait which enforces the pattern:
/// - Traits DECIDE when to work (via sealed SuggestAction)
/// - Activities EXECUTE the work (via CreateWorkActivity)
///
/// Uses the reaction system (resources/reactions/*.json) rather than hardcoded recipes.
/// </summary>
public class BakerJobTrait : JobTrait
{
    // Reaction tags this baker can handle, in priority order (first = highest priority)
    // "baking" = bake_bread reaction (flour + water -> bread)
    // "milling" = mill_wheat reaction (wheat -> flour)
    private static readonly string[] _reactionTags = ["baking", "milling"];

    // Cooldown to prevent constant storage checking when no inputs are available
    private uint _lastStorageCheckTick;
    private const uint STORAGECHECKCOOLDOWN = 500; // ~1 minute game time at 8 ticks/sec

    // Water management - how much water to maintain at the workplace for baking
    private const int WATERFETCHTHRESHOLD = 4; // Only fetch when below this amount
    private const int WATERFETCHAMOUNT = 5;
    private const int DESIREDWATERATWORKPLACE = 10;

    // Wheat management - how much wheat to maintain at the workplace for milling
    private const int WHEATFETCHTHRESHOLD = 2; // Only fetch when below this amount
    private const int WHEATFETCHAMOUNT = 5;
    private const int DESIREDWHEATATWORKPLACE = 10;

    /// <summary>
    /// Gets the activity type for baker work - we use ProcessReactionActivity.
    /// </summary>
    protected override Type WorkActivityType => typeof(ProcessReactionActivity);

    /// <summary>
    /// Gets desired resource stockpile for baker's home.
    /// Bakers want flour for baking, water for dough, and bread as finished product.
    /// </summary>
    public override IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;

    private static readonly Dictionary<string, int> _desiredResources = new ()
    {
        { "flour", 5 },   // Need flour for baking bread
        { "water", 10 },  // Need water for dough
        { "bread", 5 } // Keep some bread in stock
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="BakerJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// </summary>
    public BakerJobTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BakerJobTrait"/> class.
    /// Constructor for direct instantiation with a workplace.
    /// </summary>
    public BakerJobTrait(Building workplace)
    {
        _workplace = workplace.GetDefaultRoom()?.GetStorageFacility();
    }

    /// <summary>
    /// Create the work activity for baking.
    /// Returns (in priority order):
    /// - ProcessReactionActivity if a reaction can be performed (milling or baking)
    /// - FetchResourceActivity for water if water is low and a well is available
    /// - FetchResourceActivity for wheat if wheat is low and a source is known
    /// - CheckStorageActivity if we have no memory of required inputs
    /// - null if nothing can be done (lets VillagerTrait handle idle behavior).
    /// </summary>
    protected override Activity? CreateWorkActivity()
    {
        if (_owner == null || _workplace == null)
        {
            return null;
        }

        // If already in a fetch activity, let it complete
        if (_owner.GetCurrentActivity() is FetchResourceActivity)
        {
            DebugLog("BAKER", "Already fetching resources, waiting for completion");
            return null;
        }

        // If already checking storage, let it complete
        if (_owner.GetCurrentActivity() is CheckStorageActivity)
        {
            DebugLog("BAKER", "Already checking storage, waiting for completion");
            return null;
        }

        // Get workplace storage using wrapper method (auto-observes contents when adjacent)
        // Note: This uses MEMORY - we can only see what we remember from past observations
        var storage = _workplace.SelfAsEntity().GetTrait<StorageTrait>();
        if (storage == null)
        {
            DebugLog("BAKER", "Workplace has no storage");
            return null;
        }

        var facilities = _workplace.Owner?.GetDefaultRoom()?.Facilities
            .Select(f => f.Id).Distinct() ?? Enumerable.Empty<string>();

        // Track missing inputs for potential water fetch or storage check
        List<string> missingInputItemIds = [];

        // Find a reaction we can perform (check in priority order)
        ReactionDefinition? selectedReaction = null;

        foreach (var tag in _reactionTags)
        {
            var taggedReactions = ReactionResourceManager.Instance.GetReactionsByTag(tag);

            foreach (var reaction in taggedReactions)
            {
                // Check if we have the required facilities
                bool hasFacilities = reaction.CanPerformWith(facilities);
                if (!hasFacilities)
                {
                    continue;
                }

                // Check if we have the required inputs (using memory)
                bool hasInputs = HasRequiredInputs(reaction);

                if (hasInputs)
                {
                    selectedReaction = reaction;
                    break;
                }
                else
                {
                    // Track which inputs are missing for potential fetch/check
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
            // We can perform a reaction - start processing
            DebugLog("BAKER", $"Starting reaction: {selectedReaction.Id} ({selectedReaction.Name})", 0);
            return new ProcessReactionActivity(selectedReaction, _workplace, priority: 0);
        }

        // No reaction available - check if water is needed and we can fetch it
        // This takes priority because it's proactive resource gathering
        var waterFetchActivity = TryCreateWaterFetchActivity(missingInputItemIds);
        if (waterFetchActivity != null)
        {
            return waterFetchActivity;
        }

        // No water fetch needed - check if wheat is needed and we can fetch it
        // Wheat is needed for milling into flour
        var wheatFetchActivity = TryCreateWheatFetchActivity(missingInputItemIds);
        if (wheatFetchActivity != null)
        {
            return wheatFetchActivity;
        }

        // No reaction, no water fetch, no wheat fetch - check if we should observe the workplace storage
        // This happens when we don't remember any of the input items we need
        var currentTick = GameController.CurrentTick;
        if (missingInputItemIds.Count > 0 && ShouldCheckStorage(missingInputItemIds, currentTick))
        {
            DebugLog("BAKER", $"No memory of required inputs ({string.Join(", ", missingInputItemIds)}), going to check workplace storage", 0);
            _lastStorageCheckTick = currentTick;
            return new CheckStorageActivity(_workplace, priority: 0);
        }

        // Nothing to do - return null to let VillagerTrait handle idle behavior
        // This is intentional: bakers should do villager things (wander, socialize) when there's nothing to bake
        DebugLog("BAKER", "Nothing to do (no inputs available or cooldown active), deferring to VillagerTrait");
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
                return false;
            }
        }

        // We don't remember any of the required items - but check cooldown first
        if (currentTick - _lastStorageCheckTick < STORAGECHECKCOOLDOWN)
        {
            DebugLog("BAKER", $"Storage check on cooldown ({currentTick - _lastStorageCheckTick}/{STORAGECHECKCOOLDOWN} ticks)");
            return false;
        }

        // Cooldown expired - go check storage
        return true;
    }

    /// <summary>
    /// Check if storage has all required inputs for a reaction.
    /// Uses Being wrapper methods so the baker queries their MEMORY of storage contents.
    /// </summary>
    private bool HasRequiredInputs(ReactionDefinition reaction)
    {
        if (_owner == null || _workplace == null || reaction.Inputs == null || reaction.Inputs.Count == 0)
        {
            return reaction.Inputs == null || reaction.Inputs.Count == 0;
        }

        foreach (var input in reaction.Inputs)
        {
            // Use wrapper method which queries MEMORY (not real storage)
            if (!_owner.StorageHasItem(_workplace, input.ItemId, input.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Try to create a water fetch activity if water is low at the workplace.
    /// Returns a FetchResourceActivity if water should be fetched, null otherwise.
    /// </summary>
    /// <param name="missingInputs">List of missing input item IDs from reaction checks.</param>
    private Activity? TryCreateWaterFetchActivity(List<string> missingInputs)
    {
        if (_owner == null || _workplace == null)
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
        if (currentWater >= WATERFETCHTHRESHOLD)
        {
            // We have enough water, don't fetch yet
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
            // We don't remember seeing water at the well
            // Storage observations are keyed by Facility — look up the storage facility first
            var wellStorageFacility = well.GetDefaultRoom()?.GetStorageFacility();
            var wellMemory = wellStorageFacility != null
                ? _owner.Memory?.RecallStorageContents(wellStorageFacility)
                : null;
            if (wellMemory == null)
            {
                if (wellStorageFacility == null)
                {
                    DebugLog("BAKER", "Well has no storage facility");
                    return null;
                }

                // No memory of well - go check it
                DebugLog("BAKER", "Need water, no memory of well storage, going to check well", 0);
                return new CheckStorageActivity(wellStorageFacility, priority: 0);
            }

            // We have memory of the well being empty - wait for water to regenerate
            DebugLog("BAKER", "Need water but remember well being empty, waiting for regeneration");
            return null;
        }

        // Calculate how much water to fetch
        var wellStorage = well.GetDefaultRoom()?.GetStorageFacility();
        if (wellStorage == null)
        {
            DebugLog("BAKER", "Well has no storage facility for water fetch");
            return null;
        }

        var workplace = _workplace;
        if (workplace == null)
        {
            return null;
        }

        int amountToFetch = System.Math.Min(WATERFETCHAMOUNT, DESIREDWATERATWORKPLACE - currentWater);
        amountToFetch = System.Math.Max(1, amountToFetch); // At least try to get 1

        DebugLog("BAKER", $"Workplace water low ({currentWater}/{DESIREDWATERATWORKPLACE}), fetching {amountToFetch} from well", 0);

        return new FetchResourceActivity(wellStorage, workplace, "water", amountToFetch, priority: 0);
    }

    /// <summary>
    /// Try to create a wheat fetch activity if wheat is low at the workplace.
    /// Uses personal memory and SharedKnowledge to find buildings with wheat.
    /// Pattern from ItemConsumptionBehaviorTrait for finding items.
    /// Returns a FetchResourceActivity if wheat should be fetched, null otherwise.
    /// </summary>
    /// <param name="missingInputs">List of missing input item IDs from reaction checks.</param>
    private Activity? TryCreateWheatFetchActivity(List<string> missingInputs)
    {
        if (_owner == null || _workplace == null)
        {
            return null;
        }

        // Check if wheat is one of the missing inputs
        if (!missingInputs.Contains("wheat"))
        {
            return null;
        }

        // Check current wheat level at workplace (from memory)
        int currentWheat = _owner.GetStorageItemCount(_workplace, "wheat");
        if (currentWheat >= WHEATFETCHTHRESHOLD)
        {
            // We have enough wheat, don't fetch yet
            return null;
        }

        // We need wheat - find a building that has it
        // Strategy 1: Check personal memory for buildings where we saw wheat
        var rememberedWheatLocations = _owner.Memory?.RecallStorageWithItemById("wheat") ?? [];

        // Strategy 2: Check SharedKnowledge for buildings tagged as storing grain
        // (Farmer homes with IDesiredResources wheat would be tagged as "grain" storage)
        var grainBuildings = _owner.SharedKnowledge
            .SelectMany(k => k.GetBuildingsByTag("grain"))
            .Where(b => b.IsValid && b.Building != null && b.Building != _workplace?.Owner)
            .Select(b => b.Building)
            .OfType<Building>()
            .ToList();

        // Build list of candidate buildings to fetch from
        // Priority: buildings we remember having wheat > buildings known to store grain
        Building? sourceBuilding = null;
        int rememberedQuantity = 0;

        // First, check remembered locations (highest confidence)
        // RecallStorageWithItemById returns (Facility, int); use facility.Owner for the Building.
        foreach (var (facility, quantity) in rememberedWheatLocations)
        {
            var ownerBuilding = facility.Owner;

            // Skip if no owner building (shouldn't happen for building-owned facilities)
            if (ownerBuilding == null)
            {
                continue;
            }

            // Skip our own workplace
            if (ownerBuilding == _workplace?.Owner)
            {
                continue;
            }

            // Skip invalid buildings
            if (!GodotObject.IsInstanceValid(ownerBuilding))
            {
                continue;
            }

            // Use the building with the most remembered wheat
            if (quantity > rememberedQuantity)
            {
                sourceBuilding = ownerBuilding;
                rememberedQuantity = quantity;
            }
        }

        // If we found a building with remembered wheat, use it
        if (sourceBuilding != null && rememberedQuantity > 0)
        {
            var sourceStorage = sourceBuilding.GetDefaultRoom()?.GetStorageFacility();
            var workplace = _workplace;
            if (sourceStorage == null || workplace == null)
            {
                DebugLog("BAKER", "Source or workplace has no storage facility for wheat fetch");
                return null;
            }

            int amountToFetch = System.Math.Min(WHEATFETCHAMOUNT, DESIREDWHEATATWORKPLACE - currentWheat);
            amountToFetch = System.Math.Min(amountToFetch, rememberedQuantity); // Don't try to take more than we remember
            amountToFetch = System.Math.Max(1, amountToFetch);

            DebugLog("BAKER", $"Workplace wheat low ({currentWheat}/{DESIREDWHEATATWORKPLACE}), fetching {amountToFetch} from {sourceBuilding.BuildingName} (remembered {rememberedQuantity})", 0);

            return new FetchResourceActivity(sourceStorage, workplace, "wheat", amountToFetch, priority: 0);
        }

        // No remembered wheat - check if there are grain buildings we haven't observed
        foreach (var building in grainBuildings)
        {
            // Check if we have any memory of this building's storage
            // Storage observations are keyed by Facility — look up the storage facility first
            var storageFacility = building.GetDefaultRoom()?.GetStorageFacility();
            var observation = storageFacility != null
                ? _owner.Memory?.RecallStorageContents(storageFacility)
                : null;
            if (observation == null)
            {
                if (storageFacility == null)
                {
                    continue; // No storage facility, skip
                }

                // No memory of this building - go check it
                DebugLog("BAKER", $"Need wheat, no memory of {building.BuildingName} storage, going to check", 0);
                return new CheckStorageActivity(storageFacility, priority: 0);
            }

            // We have memory but it showed no wheat (or we already checked above)
            // Continue to next building
        }

        // No wheat sources found or all checked and empty
        DebugLog("BAKER", "Need wheat but no sources found via memory or SharedKnowledge");
        return null;
    }

    // ===== Dialogue Methods =====
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
