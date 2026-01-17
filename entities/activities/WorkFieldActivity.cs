using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for working at a field/farm during daytime.
/// Phases:
/// 1. Navigate to workplace building
/// 2. Navigate to crop facility (if available)
/// 3. Work (idle, produce wheat)
/// 4. Take wheat from farm storage into inventory
/// 5. Navigate home
/// 6. Deposit wheat to home storage
/// Completes after depositing or if day phase ends.
/// Drains energy while working (restored by sleeping).
/// </summary>
public class WorkFieldActivity : Activity
{
    private enum WorkPhase
    {
        GoingToWork,
        GoingToCrop,
        Working,
        TakingWheat,
        GoingHome,
        DepositingWheat,
        Done
    }

    // Energy cost per tick while actively working
    // At 0.01/tick over a 1500-tick shift = 15 energy per shift
    // Two shifts = 30 energy from work + ~38 from decay = ~68 total
    // Leaves farmer at ~32 energy (above critical 20) by end of day
    private const float ENERGYCOSTPERTICK = 0.01f;

    // Amount of wheat produced per work shift
    private const int WHEATPRODUCEDPERSHIFT = 3;

    // Amount of wheat to bring home per trip (farmer does 2 shifts/day, produces 3/shift = +1 surplus per shift)
    private const int WHEATTOBRINGHOME = 2;

    private readonly Building _workplace;
    private readonly Building? _home;
    private readonly uint _workDuration;

    private GoToBuildingActivity? _goToWorkPhase;
    private GoToFacilityActivity? _goToCropPhase;
    private GoToBuildingActivity? _goToHomePhase;
    private uint _workTimer;
    private uint _ticksSinceLastWheat;
    private int _wheatProducedThisShift;
    private WorkPhase _currentPhase = WorkPhase.GoingToWork;
    private Need? _energyNeed;

    public override string DisplayName => _currentPhase switch
    {
        WorkPhase.GoingToWork => "Going to work",
        WorkPhase.GoingToCrop => "Going to crops",
        WorkPhase.Working => $"Working at {_workplace.BuildingType}",
        WorkPhase.TakingWheat => "Gathering harvest",
        WorkPhase.GoingHome => "Bringing harvest home",
        WorkPhase.DepositingWheat => "Storing harvest",
        _ => "Working"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkFieldActivity"/> class.
    /// Create an activity to work at a building and bring harvest home.
    /// </summary>
    /// <param name="workplace">The building to work at (farm, etc.)</param>
    /// <param name="home">The home building to deposit harvest (can be null).</param>
    /// <param name="workDuration">How many ticks to work before taking a break.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public WorkFieldActivity(Building workplace, Building? home, uint workDuration, int priority = 0)
    {
        _workplace = workplace;
        _home = home;
        _workDuration = workDuration;
        Priority = priority;

        // Working makes you hungry faster
        NeedDecayMultipliers["hunger"] = 1.2f;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get energy need - work directly costs energy (not via decay multiplier)
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");

        DebugLog("ACTIVITY", $"Started WorkFieldActivity at {_workplace.BuildingName}, home: {_home?.BuildingName ?? "none"}, priority: {Priority}", 0);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if workplace still exists
        if (!GodotObject.IsInstanceValid(_workplace))
        {
            Fail();
            return null;
        }

        // Check if work time has ended (day phase changed to dusk/night)
        // If we're still working or navigating to crops, transition to taking wheat and going home
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            if (_currentPhase is WorkPhase.Working or WorkPhase.GoingToCrop)
            {
                Log.Print($"{_owner.Name}: Work time ended, gathering harvest to bring home");
                _currentPhase = WorkPhase.TakingWheat;
            }
        }

        return _currentPhase switch
        {
            WorkPhase.GoingToWork => ProcessGoingToWork(position, perception),
            WorkPhase.GoingToCrop => ProcessGoingToCrop(position, perception),
            WorkPhase.Working => ProcessWorking(),
            WorkPhase.TakingWheat => ProcessTakingWheat(),
            WorkPhase.GoingHome => ProcessGoingHome(position, perception),
            WorkPhase.DepositingWheat => ProcessDepositingWheat(),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToWork(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to phase if needed
        if (_goToWorkPhase == null)
        {
            _goToWorkPhase = new GoToBuildingActivity(_workplace, Priority);
            _goToWorkPhase.Initialize(_owner);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToWorkPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at building
        Log.Print($"{_owner.Name}: Arrived at {_workplace.BuildingType}");
        DebugLog("ACTIVITY", $"Arrived at workplace, now navigating to crop facility", 0);

        // If the workplace has a crop facility, navigate to it; otherwise start working directly
        if (_workplace.HasFacility("crop"))
        {
            _currentPhase = WorkPhase.GoingToCrop;
        }
        else
        {
            // No crop facility defined, proceed to work directly
            _currentPhase = WorkPhase.Working;
            Log.Print($"{_owner.Name}: Started working at {_workplace.BuildingType}");
            DebugLog("ACTIVITY", $"No crop facility, starting work phase directly (duration: {_workDuration} ticks)", 0);
            LogStorageInfo();
        }

        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessGoingToCrop(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to-crop phase if needed
        if (_goToCropPhase == null)
        {
            _goToCropPhase = new GoToFacilityActivity(_workplace, "crop", Priority);
            _goToCropPhase.Initialize(_owner);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToCropPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Fall back to working at current position
                Log.Warn($"{_owner.Name}: Could not reach crop facility, working at current position");
                DebugLog("ACTIVITY", "Failed to reach crop facility, starting work phase anyway", 0);
                _currentPhase = WorkPhase.Working;
                LogStorageInfo();
                return new IdleAction(_owner, this, Priority);
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at crop
        _currentPhase = WorkPhase.Working;
        Log.Print($"{_owner.Name}: Started working at crops in {_workplace.BuildingType}");
        DebugLog("ACTIVITY", $"Arrived at crop facility, starting work phase (duration: {_workDuration} ticks)", 0);
        LogStorageInfo();
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessWorking()
    {
        if (_owner == null)
        {
            return null;
        }

        _workTimer++;
        _ticksSinceLastWheat++;

        // Directly spend energy while working
        _energyNeed?.Restore(-ENERGYCOSTPERTICK);

        // Calculate production interval: produce wheat gradually across the shift
        uint productionInterval = _workDuration / WHEATPRODUCEDPERSHIFT;

        // Check if it's time to produce wheat (and we haven't produced all wheat yet)
        if (_ticksSinceLastWheat >= productionInterval && _wheatProducedThisShift < WHEATPRODUCEDPERSHIFT)
        {
            ProduceSingleWheat();
            _ticksSinceLastWheat = 0;
            _wheatProducedThisShift++;
            DebugLog("ACTIVITY", $"Produced 1 wheat ({_wheatProducedThisShift}/{WHEATPRODUCEDPERSHIFT} this shift)", 0);
        }

        if (_workTimer >= _workDuration)
        {
            Log.Print($"{_owner.Name}: Completed work shift, gathering harvest");
            DebugLog("ACTIVITY", $"Work phase complete, produced {_wheatProducedThisShift} wheat total, transitioning to TakingWheat", 0);
            LogStorageInfo();
            _currentPhase = WorkPhase.TakingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        // Periodic progress log while working
        DebugLog("ACTIVITY", $"Working... progress: {_workTimer}/{_workDuration} ticks, wheat: {_wheatProducedThisShift}/{WHEATPRODUCEDPERSHIFT}, energy: {_energyNeed?.Value:F1}");

        // Still working, idle
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingWheat()
    {
        if (_owner == null)
        {
            return null;
        }

        // Take wheat from farm storage into inventory
        TakeWheatFromFarm();

        // If no home, just complete here
        if (_home == null || !GodotObject.IsInstanceValid(_home))
        {
            Log.Print($"{_owner.Name}: No home to bring harvest to, shift complete");
            DebugLog("ACTIVITY", "No home to bring harvest to, completing activity", 0);
            Complete();
            return null;
        }

        DebugLog("ACTIVITY", "Wheat taken from farm, transitioning to GoingHome", 0);
        _currentPhase = WorkPhase.GoingHome;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessGoingHome(Vector2I position, Perception perception)
    {
        if (_owner == null || _home == null)
        {
            Complete();
            return null;
        }

        // Initialize go-to-home phase if needed (targeting storage position)
        if (_goToHomePhase == null)
        {
            _goToHomePhase = new GoToBuildingActivity(_home, Priority, targetStorage: true);
            _goToHomePhase.Initialize(_owner);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToHomePhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Just complete, we tried our best
                Log.Warn($"{_owner.Name}: Couldn't reach home, wheat stays in inventory");
                Complete();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at home
        DebugLog("ACTIVITY", "Arrived at home, transitioning to DepositingWheat", 0);
        _currentPhase = WorkPhase.DepositingWheat;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingWheat()
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // Deposit wheat from inventory to home storage
        DepositWheatToHome();

        Log.Print($"{_owner.Name}: Work day complete, harvest stored at home");
        DebugLog("ACTIVITY", "Wheat deposited at home, activity complete", 0);
        LogStorageInfo();
        Complete();
        return null;
    }

    /// <summary>
    /// Produce a single unit of wheat and deposit to the farm's storage.
    /// Called periodically during the Working phase to spread production across the shift.
    ///
    /// The FARM produces wheat into its own storage directly.
    /// This is NOT a remote storage access - the building is accessing its own storage.
    /// The farmer triggers production, but the farm handles storage.
    /// </summary>
    private void ProduceSingleWheat()
    {
        if (_owner == null)
        {
            return;
        }

        // The BUILDING produces wheat into its own storage.
        // The farmer triggers production, the building handles the storage.
        if (_workplace.ProduceItem("wheat", 1))
        {
            var storage = _workplace.GetStorage();
            Log.Print($"{_owner.Name}: Harvested 1 wheat at {_workplace.BuildingName} (Farm: {storage?.GetContentsSummary() ?? "unknown"})");

            // Farmer observes the storage since they're physically here working
            if (storage != null)
            {
                _owner.Memory?.ObserveStorage(_workplace, storage);
            }
        }
        else
        {
            Log.Warn($"{_owner.Name}: Farm storage full or unavailable, wheat lost!");
        }
    }

    /// <summary>
    /// Take wheat from farm storage into inventory.
    /// Takes exactly WHEATTOBRINGHOME (2) wheat, or all available if less than that.
    /// Uses Being's storage wrapper to auto-observe storage contents.
    /// </summary>
    private void TakeWheatFromFarm()
    {
        if (_owner == null)
        {
            return;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Log.Warn($"{_owner.Name}: No inventory to carry wheat");
            return;
        }

        // Check how much is available using wrapper (auto-observes)
        int available = _owner.GetStorageItemCount(_workplace, "wheat");
        if (available == 0)
        {
            Log.Print($"{_owner.Name}: No wheat at farm to bring home");
            return;
        }

        // Take exactly WHEATTOBRINGHOME, or all available if less
        int actualAmount = System.Math.Min(WHEATTOBRINGHOME, available);

        // Use wrapper method - auto-observes storage contents after taking
        var wheat = _owner.TakeFromStorage(_workplace, "wheat", actualAmount);
        if (wheat != null)
        {
            if (inventory.AddItem(wheat))
            {
                // Get storage for logging (already observed by TakeFromStorage)
                var farmStorage = _workplace.GetStorage();
                Log.Print($"{_owner.Name}: Took {wheat.Quantity} wheat to bring home (Farm: {farmStorage?.GetContentsSummary() ?? "unknown"}, Inventory: {inventory.GetContentsSummary()})");
            }
            else
            {
                // Inventory full, put it back using wrapper
                _owner.PutInStorage(_workplace, wheat);
                Log.Warn($"{_owner.Name}: Inventory full, leaving wheat at farm");
            }
        }
    }

    /// <summary>
    /// Deposit wheat from inventory to home storage.
    /// Uses Being's storage wrapper to auto-observe storage contents.
    /// </summary>
    private void DepositWheatToHome()
    {
        if (_owner == null || _home == null)
        {
            return;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            return;
        }

        // Transfer all wheat from inventory to home storage
        int wheatCount = inventory.GetItemCount("wheat");
        if (wheatCount == 0)
        {
            return;
        }

        var wheat = inventory.RemoveItem("wheat", wheatCount);
        if (wheat != null)
        {
            // Use wrapper method - auto-observes storage contents
            if (_owner.PutInStorage(_home, wheat))
            {
                // Get storage for logging (already observed by PutInStorage)
                var homeStorage = _home.GetStorage();
                Log.Print($"{_owner.Name}: Stored {wheat.Quantity} wheat at home (Home: {homeStorage?.GetContentsSummary() ?? "unknown"})");
            }
            else
            {
                // Home storage full, keep in inventory
                inventory.AddItem(wheat);
                Log.Warn($"{_owner.Name}: Home storage full, keeping wheat in inventory");
            }
        }
    }

    /// <summary>
    /// Log storage information for debugging.
    /// Shows both real storage contents and what the entity remembers.
    /// Uses Being's storage wrapper to auto-observe storage contents.
    /// </summary>
    private void LogStorageInfo()
    {
        if (_owner?.DebugEnabled != true)
        {
            return;
        }

        // Farm storage info - use wrapper to auto-observe
        var farmStorage = _owner.AccessStorage(_workplace);
        if (farmStorage != null)
        {
            var realContents = farmStorage.GetContentsSummary();

            // Get remembered contents for farm
            var memoryContents = "nothing (no memory)";
            var storageMemory = _owner.Memory?.RecallStorageContents(_workplace);
            if (storageMemory != null)
            {
                var rememberedItems = storageMemory.Items
                    .Select(i => $"{i.Quantity} {i.Name}")
                    .ToList();
                memoryContents = rememberedItems.Count > 0 ? string.Join(", ", rememberedItems) : "empty";
            }

            DebugLog("STORAGE", $"[{_workplace.BuildingName}] Real: {realContents} | Remembered: {memoryContents}", 0);
        }

        // Home storage info - use wrapper to auto-observe
        if (_home != null && GodotObject.IsInstanceValid(_home))
        {
            var homeStorage = _owner.AccessStorage(_home);
            if (homeStorage != null)
            {
                var realContents = homeStorage.GetContentsSummary();

                // Get remembered contents for home
                var memoryContents = "nothing (no memory)";
                var storageMemory = _owner.Memory?.RecallStorageContents(_home);
                if (storageMemory != null)
                {
                    var rememberedItems = storageMemory.Items
                        .Select(i => $"{i.Quantity} {i.Name}")
                        .ToList();
                    memoryContents = rememberedItems.Count > 0 ? string.Join(", ", rememberedItems) : "empty";
                }

                DebugLog("STORAGE", $"[{_home.BuildingName}] Real: {realContents} | Remembered: {memoryContents}", 0);
            }
        }

        // Inventory info
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            DebugLog("STORAGE", $"Inventory: {inventory.GetContentsSummary()}", 0);
        }
    }
}
