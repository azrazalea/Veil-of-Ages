using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Memory;
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
    private string _needId = "hunger";
    private string _foodTag = "food";
    private float _restoreAmount = 60f;
    private uint _consumptionDuration = 244;

    private Need? _need;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemConsumptionBehaviorTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Configure via Configure() method with parameters from JSON.
    /// </summary>
    public ItemConsumptionBehaviorTrait()
    {
    }

    /// <summary>
    /// Gets the home room from the HomeTrait.
    /// Returns null if owner doesn't have HomeTrait or no home is set.
    /// </summary>
    private Room? GetHome()
    {
        return _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
    }

    /// <summary>
    /// Validates configuration parameters.
    /// </summary>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // foodTag is required
        if (string.IsNullOrEmpty(config.GetString("foodTag")))
        {
            Log.Warn("ItemConsumptionBehaviorTrait: 'foodTag' parameter required");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Configures the trait from JSON parameters.
    /// Expected parameters:
    /// - "needId" (string): The need to satisfy (default: "hunger")
    /// - "foodTag" (string): Tag to identify food items (required, e.g., "food", "zombie_food")
    /// - "restoreAmount" (float): Amount to restore when eating (default: 60)
    /// - "consumptionDuration" (int): Ticks to spend eating (default: 244).
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        _needId = config.GetString("needId") ?? "hunger";
        _foodTag = config.GetString("foodTag") ?? "food";
        _restoreAmount = config.GetFloat("restoreAmount") ?? 60f;
        _consumptionDuration = (uint)(config.GetInt("consumptionDuration") ?? 244);
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Need is found lazily in SuggestAction to handle initialization order
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (!IsInitialized || _owner == null)
        {
            return null;
        }

        // Lazily find the need (handles initialization order - LivingTrait may not have run yet)
        if (_need == null)
        {
            _need = _owner.NeedsSystem?.GetNeed(_needId);
            if (_need == null)
            {
                return null; // Need not registered yet, wait for next tick
            }

            Log.Print($"{_owner.Name}: ItemConsumptionBehaviorTrait found need '{_needId}' with food tag '{_foodTag}'");
        }

        // If we already have a consume or check storage activity running, let it handle things
        var currentActivity = _owner.GetCurrentActivity();
        if (currentActivity is ConsumeItemActivity)
        {
            DebugLog("EATING", $"ConsumeItemActivity already running, state: {currentActivity.State}");
            return null;
        }

        if (currentActivity is CheckStorageActivity)
        {
            DebugLog("EATING", $"CheckStorageActivity already running, state: {currentActivity.State}");
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
            // Determine priority - use same logic as eating
            int checkPriority = _need.IsCritical() ? -2 : -1;

            // Build list of potential food source rooms to check (home first, then known food rooms)
            var roomsToCheck = new List<Room>();

            // Add home if exists
            var homeRoom = GetHome();
            if (homeRoom != null && !homeRoom.IsDestroyed)
            {
                roomsToCheck.Add(homeRoom);
            }

            // Add rooms that SharedKnowledge says store food
            var foodRoomRefs = _owner.SharedKnowledge
                .SelectMany(k => k.GetRoomsByTag(_foodTag))
                .Where(r => r.IsValid && r.Room != null)
                .ToList();

            foreach (var roomRef in foodRoomRefs)
            {
                var room = roomRef.Room;
                if (room != null && !roomsToCheck.Contains(room))
                {
                    roomsToCheck.Add(room);
                }
            }

            // Find a room we haven't recently checked (no observation or observation expired)
            // This prevents infinite loops when home is empty - we'll try granary next
            foreach (var room in roomsToCheck)
            {
                // Storage observations are keyed by Facility — check via the room's storage facility
                var storageFacility = room.GetStorageFacility();
                var observation = storageFacility != null
                    ? _owner.Memory?.RecallStorageContents(storageFacility)
                    : null;
                if (observation == null)
                {
                    if (storageFacility == null)
                    {
                        continue; // No storage facility in this room, skip
                    }

                    // Never checked this room or observation expired - go check it
                    DebugLog("EATING", $"No food memory, going to check {room.Name} (priority {checkPriority})", 0);
                    var checkActivity = new CheckStorageActivity(storageFacility, priority: checkPriority);
                    return new StartActivityAction(_owner, this, checkActivity, priority: checkPriority);
                }

                // If observation exists, we already checked it and found no food (or we'd have food memory)
                // Skip to next room
            }

            // All known food sources were recently checked and found no food
            // Wait for standing orders to deliver food, or for memory to expire so we check again
            DebugLogFoodSearch();
            return null;
        }

        // Determine priority based on hunger severity
        // Critical hunger: priority -2 (interrupts everything)
        // Low hunger: priority -1 (interrupts work but not sleep)
        int actionPriority = _need.IsCritical() ? -2 : -1;

        // Find the facility that actually has food (may not be home!)
        // First check if food is in inventory (targetStorage can be null)
        Facility? targetStorage = null;
        string targetName = "inventory";
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory?.FindItemByTag(_foodTag) == null)
        {
            // Not in inventory - find which facility has the remembered food
            // RecallStorageWithItem returns (Facility, int); use facility directly.
            var facilitiesWithFood = _owner.Memory?.RecallStorageWithItem(_foodTag) ?? [];
            if (facilitiesWithFood.Count > 0)
            {
                // Use the first facility (could optimize to pick nearest)
                targetStorage = facilitiesWithFood[0].facility;
                targetName = targetStorage.ContainingRoom?.Name ?? targetStorage.Id;
                DebugLog("EATING", $"Found remembered food at {targetName}");
            }
            else
            {
                // No remembered food - this shouldn't happen since HasFoodAvailable() returned true
                // Fall back to home storage as last resort
                var homeRoom = GetHome();
                targetStorage = homeRoom?.GetStorageFacility();
                targetName = homeRoom?.Name ?? "null";
                DebugLog("EATING", $"No remembered food location, falling back to home: {targetName}");
            }
        }
        else
        {
            DebugLog("EATING", "Food is in inventory, no travel needed");
        }

        // Start consume activity
        var consumeActivity = new ConsumeItemActivity(
            _foodTag,
            _need,
            targetStorage,
            _restoreAmount,
            _consumptionDuration,
            priority: actionPriority);

        DebugLog("EATING", $"Starting ConsumeItemActivity (priority {actionPriority}), target: {targetName}", 0);
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
        var homeRoom = GetHome();

        // Check memory for remembered food
        var remembersFood = _owner.Memory?.RemembersItemAvailable(_foodTag) == true;
        var rememberedLocations = _owner.Memory?.RecallStorageWithItem(_foodTag) ?? [];

        var invInfo = inventoryFood != null ? $"{inventoryFood.Quantity} {inventoryFood.Definition.Name}" : "none";
        var homeInfo = homeRoom?.Name ?? "null";
        var memoryInfo = remembersFood ? $"yes ({rememberedLocations.Count} locations)" : "no";

        // Add real vs remembered storage comparison for home
        var storageComparison = string.Empty;
        if (homeRoom != null && !homeRoom.IsDestroyed)
        {
            var homeStorage = homeRoom.GetStorage();
            if (homeStorage != null)
            {
                var realFood = homeStorage.FindItemByTag(_foodTag);
                var realInfo = realFood != null ? $"{realFood.Quantity} {realFood.Definition.Name}" : "none";

                var memoryContents = "nothing (no memory)";
                var homeStorageFacility = homeRoom.GetStorageFacility();
                var storageMemory = homeStorageFacility != null
                    ? _owner.Memory?.RecallStorageContents(homeStorageFacility)
                    : null;
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

                storageComparison = $", [{homeRoom.Name}] Real: {realInfo} | Remembered: {memoryContents}";
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
