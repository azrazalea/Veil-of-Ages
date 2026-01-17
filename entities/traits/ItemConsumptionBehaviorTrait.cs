using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that handles need satisfaction by consuming items.
/// Checks inventory first, then personal memory for remembered food locations.
/// Entities only know what's in their inventory (immediate access) or what they
/// remember observing in storage (decays over time). They do NOT omnisciently
/// know what's in any storage container.
/// </summary>
public class ItemConsumptionBehaviorTrait : BeingTrait
{
    private readonly string _needId;
    private readonly string _foodTag;
    private readonly Func<Building?> _getHome;
    private readonly float _restoreAmount;
    private readonly uint _consumptionDuration;

    private Need? _need;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemConsumptionBehaviorTrait"/> class.
    /// Create a trait for item-based consumption.
    /// </summary>
    /// <param name="needId">The need to satisfy (e.g., "hunger").</param>
    /// <param name="foodTag">Tag to identify food items (e.g., "food", "zombie_food").</param>
    /// <param name="getHome">Function to get home building (may return null).</param>
    /// <param name="restoreAmount">Amount to restore when eating.</param>
    /// <param name="consumptionDuration">Ticks to spend eating.</param>
    public ItemConsumptionBehaviorTrait(
        string needId,
        string foodTag,
        Func<Building?> getHome,
        float restoreAmount = 60f,
        uint consumptionDuration = 244)
    {
        _needId = needId;
        _foodTag = foodTag;
        _getHome = getHome;
        _restoreAmount = restoreAmount;
        _consumptionDuration = consumptionDuration;
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        _need = _owner?.NeedsSystem?.GetNeed(_needId);

        if (_need == null)
        {
            Log.Error($"{_owner?.Name}: ItemConsumptionBehaviorTrait could not find need '{_needId}'");
        }
        else
        {
            Log.Print($"{_owner?.Name}: ItemConsumptionBehaviorTrait initialized for need '{_needId}' with food tag '{_foodTag}'");
        }
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (!IsInitialized || _owner == null || _need == null)
        {
            return null;
        }

        // If we already have a consume or check storage activity running, let it handle things
        var currentActivity = _owner.GetCurrentActivity();
        if (currentActivity is ConsumeItemActivity)
        {
            DebugLog("EATING", $"ConsumeItemActivity already running, state: {currentActivity.State}");
            return null;
        }

        if (currentActivity is CheckHomeStorageActivity)
        {
            DebugLog("EATING", $"CheckHomeStorageActivity already running, state: {currentActivity.State}");
            return null;
        }

        // Check if need is low (hungry)
        if (!_need.IsLow())
        {
            return null;
        }

        // Log hunger status when low/critical
        DebugLog("EATING", $"Hunger is {(_need.IsCritical() ? "CRITICAL" : "low")}: {_need.Value:F1}, current activity: {currentActivity?.GetType().Name ?? "none"}");

        // Note: This check is technically redundant because Being.Think() returns early
        // if IsMoving() is true - traits are never consulted while moving.
        // Kept for defensive programming in case the architecture changes.
        if (_owner.IsMoving())
        {
            return null;
        }

        // Check if we have food available
        if (!HasFoodAvailable())
        {
            // No food in inventory and no memory of food locations
            // If we have a home, go check its storage to refresh memory
            var home = _getHome();
            if (home != null && GodotObject.IsInstanceValid(home))
            {
                // Determine priority - use same logic as eating
                int checkPriority = _need.IsCritical() ? -2 : -1;

                DebugLog("EATING", $"No food memory, going to check home storage (priority {checkPriority})", 0);
                var checkActivity = new CheckHomeStorageActivity(home, priority: checkPriority);
                return new StartActivityAction(_owner, this, checkActivity, priority: checkPriority);
            }

            // No home to check - log debug info about why food wasn't found
            DebugLogFoodSearch();
            return null;
        }

        // Determine priority based on hunger severity
        // Critical hunger: priority -2 (interrupts everything)
        // Low hunger: priority -1 (interrupts work but not sleep)
        int actionPriority = _need.IsCritical() ? -2 : -1;

        // Start consume activity
        var homeBuilding = _getHome();
        var consumeActivity = new ConsumeItemActivity(
            _foodTag,
            _need,
            homeBuilding,
            _restoreAmount,
            _consumptionDuration,
            priority: actionPriority);

        DebugLog("EATING", $"Starting ConsumeItemActivity (priority {actionPriority}), home: {homeBuilding?.BuildingName ?? "null"}", 0);
        return new StartActivityAction(_owner, this, consumeActivity, priority: actionPriority);
    }

    /// <summary>
    /// Log detailed debug info about food search results.
    /// Only logs when owner has debug enabled.
    /// Shows both real storage contents and what the entity remembers.
    /// </summary>
    private void DebugLogFoodSearch()
    {
        if (_owner?.DebugEnabled != true)
        {
            return;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        var inventoryFood = inventory?.FindItemByTag(_foodTag);
        var home = _getHome();

        // Check memory for remembered food
        var remembersFood = _owner.Memory?.RemembersItemAvailable(_foodTag) == true;
        var rememberedLocations = _owner.Memory?.RecallStorageWithItem(_foodTag) ?? [];

        var invInfo = inventoryFood != null ? $"{inventoryFood.Quantity} {inventoryFood.Definition.Name}" : "none";
        var homeInfo = home?.BuildingName ?? "null";
        var memoryInfo = remembersFood ? $"yes ({rememberedLocations.Count} locations)" : "no";

        // Add real vs remembered storage comparison for home
        var storageComparison = string.Empty;
        if (home != null && GodotObject.IsInstanceValid(home))
        {
            var homeStorage = home.GetStorage();
            if (homeStorage != null)
            {
                var realFood = homeStorage.FindItemByTag(_foodTag);
                var realInfo = realFood != null ? $"{realFood.Quantity} {realFood.Definition.Name}" : "none";

                var memoryContents = "nothing (no memory)";
                var storageMemory = _owner.Memory?.RecallStorageContents(home);
                if (storageMemory != null)
                {
                    var rememberedFood = storageMemory.Items
                        .Where(i => i.Tags.Contains(_foodTag, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    if (rememberedFood.Count > 0)
                    {
                        memoryContents = string.Join(", ", rememberedFood.Select(i => $"{i.Quantity} {i.Name}"));
                    }
                    else
                    {
                        memoryContents = "no food";
                    }
                }

                storageComparison = $", [{home.BuildingName}] Real: {realInfo} | Remembered: {memoryContents}";
            }
        }

        Log.EntityDebug(_owner.Name, "EATING", $"No {_foodTag} available - Inventory: {invInfo}, Home: {homeInfo}, RemembersFood: {memoryInfo}{storageComparison}", 0);
    }

    /// <summary>
    /// Check if food is available in inventory or remembered in storage.
    /// Uses memory-based checking - entity only knows what they've observed.
    /// </summary>
    private bool HasFoodAvailable()
    {
        if (_owner == null)
        {
            return false;
        }

        // Check inventory first - entity knows what they're carrying
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory?.FindItemByTag(_foodTag) != null)
        {
            return true;
        }

        // Check personal memory - do I remember seeing food somewhere?
        if (_owner.Memory?.RemembersItemAvailable(_foodTag) == true)
        {
            return true;
        }

        // Don't omnisciently check storage - we don't know what's there
        // If memory is empty/stale, entity needs to go check
        return false;
    }
}
