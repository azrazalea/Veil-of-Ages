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
        TakingBreak,
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

    // Break configuration
    private const uint MINBREAKDURATION = 30;  // ~4 seconds
    private const uint MAXBREAKDURATION = 60;  // ~8 seconds
    private const float BREAKPROBABILITY = 0.18f;  // 18% chance after work segment

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
    private uint _variedWorkDuration;
    private uint _breakTimer;
    private TakeFromStorageAction? _takeWheatAction;
    private DepositToStorageAction? _depositWheatAction;
    private int _actualWheatTaken;
    private ProduceToStorageAction? _pendingProduceAction;

    public override string DisplayName => _currentPhase switch
    {
        WorkPhase.GoingToWork => "Going to work",
        WorkPhase.GoingToCrop => "Going to crops",
        WorkPhase.Working => $"Working at {_workplace.BuildingType}",
        WorkPhase.TakingBreak => "Taking a break",
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

        // Apply variance to work duration for more natural behavior
        _variedWorkDuration = ActivityTiming.GetVariedDuration(_workDuration, 0.15f);

        DebugLog("ACTIVITY", $"Started WorkFieldActivity at {_workplace.BuildingName}, home: {_home?.BuildingName ?? "none"}, priority: {Priority}, work duration: {_variedWorkDuration} ticks (base: {_workDuration})", 0);
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
            WorkPhase.TakingBreak => ProcessTakingBreak(),
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

        // Check if we have a pending produce action that was executed last tick
        if (_pendingProduceAction != null)
        {
            // Action was executed on main thread - check result
            if (_pendingProduceAction.ActualProduced > 0)
            {
                var storage = _workplace.GetStorage();
                Log.Print($"{_owner.Name}: Harvested 1 wheat at {_workplace.BuildingName} (Farm: {storage?.GetContentsSummary() ?? "unknown"})");
            }
            else
            {
                Log.Warn($"{_owner.Name}: Farm storage full or unavailable, wheat lost!");
            }

            _pendingProduceAction = null;
        }

        _workTimer++;
        _ticksSinceLastWheat++;

        // Directly spend energy while working
        _energyNeed?.Restore(-ENERGYCOSTPERTICK);

        // Calculate production interval: produce wheat gradually across the shift
        uint productionInterval = _variedWorkDuration / WHEATPRODUCEDPERSHIFT;

        // Check if it's time to produce wheat (and we haven't produced all wheat yet)
        if (_ticksSinceLastWheat >= productionInterval && _wheatProducedThisShift < WHEATPRODUCEDPERSHIFT)
        {
            _ticksSinceLastWheat = 0;
            _wheatProducedThisShift++;
            DebugLog("ACTIVITY", $"Producing 1 wheat ({_wheatProducedThisShift}/{WHEATPRODUCEDPERSHIFT} this shift)", 0);

            // Return a ProduceToStorageAction to add wheat on the main thread
            // This is thread-safe because actions execute on main thread
            _pendingProduceAction = new ProduceToStorageAction(
                _owner,
                this,
                _workplace,
                "wheat",
                1,
                Priority);

            // Farmer observes the storage since they're physically here working
            // (observation will happen when action executes on main thread)
            return _pendingProduceAction;
        }

        if (_workTimer >= _variedWorkDuration)
        {
            Log.Print($"{_owner.Name}: Completed work shift, gathering harvest");
            DebugLog("ACTIVITY", $"Work phase complete, produced {_wheatProducedThisShift} wheat total", 0);
            LogStorageInfo();

            // Check if farmer should take a break first
            if (ActivityTiming.ShouldTakeBreak(BREAKPROBABILITY))
            {
                _breakTimer = ActivityTiming.GetBreakDuration(MINBREAKDURATION, MAXBREAKDURATION);
                _currentPhase = WorkPhase.TakingBreak;
                DebugLog("ACTIVITY", $"Taking a short break ({_breakTimer} ticks)", 0);
                return new IdleAction(_owner, this, Priority);
            }

            // No break, proceed to taking wheat
            _currentPhase = WorkPhase.TakingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        // Periodic progress log while working
        DebugLog("ACTIVITY", $"Working... progress: {_workTimer}/{_variedWorkDuration} ticks, wheat: {_wheatProducedThisShift}/{WHEATPRODUCEDPERSHIFT}, energy: {_energyNeed?.Value:F1}");

        // Still working, idle
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingBreak()
    {
        if (_owner == null)
        {
            return null;
        }

        _breakTimer--;

        if (_breakTimer <= 0)
        {
            DebugLog("ACTIVITY", "Break finished, gathering harvest", 0);
            _currentPhase = WorkPhase.TakingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        // During break, just idle (normal need decay applies)
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingWheat()
    {
        if (_owner == null)
        {
            return null;
        }

        // If we already have a take action in progress, check if it completed
        if (_takeWheatAction != null)
        {
            // Action was executed - check result via the callback-set value
            if (_actualWheatTaken > 0)
            {
                // Success - log and transition
                var farmStorage = _workplace.GetStorage();
                var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
                Log.Print($"{_owner.Name}: Took {_actualWheatTaken} wheat to bring home (Farm: {farmStorage?.GetContentsSummary() ?? "unknown"}, Inventory: {inventory?.GetContentsSummary() ?? "unknown"})");
            }
            else
            {
                // Action failed - log warning but continue (no wheat to bring)
                Log.Warn($"{_owner.Name}: Failed to take wheat from farm storage");
            }

            _takeWheatAction = null;

            // If no home, just complete here
            if (_home == null || !GodotObject.IsInstanceValid(_home))
            {
                Log.Print($"{_owner.Name}: No home to bring harvest to, shift complete");
                DebugLog("ACTIVITY", "No home to bring harvest to, completing activity", 0);
                Complete();
                return null;
            }

            DebugLog("ACTIVITY", "Wheat phase done, transitioning to GoingHome", 0);
            _currentPhase = WorkPhase.GoingHome;
            return new IdleAction(_owner, this, Priority);
        }

        // Check how much is available using memory (auto-observes when accessed)
        int available = _owner.GetStorageItemCount(_workplace, "wheat");
        if (available == 0)
        {
            Log.Print($"{_owner.Name}: No wheat at farm to bring home");

            // If no home, just complete here
            if (_home == null || !GodotObject.IsInstanceValid(_home))
            {
                Log.Print($"{_owner.Name}: No home to bring harvest to, shift complete");
                DebugLog("ACTIVITY", "No wheat and no home, completing activity", 0);
                Complete();
                return null;
            }

            // No wheat but has home - just go home
            DebugLog("ACTIVITY", "No wheat at farm, transitioning to GoingHome", 0);
            _currentPhase = WorkPhase.GoingHome;
            return new IdleAction(_owner, this, Priority);
        }

        // Take exactly WHEATTOBRINGHOME, or all available if less
        int actualAmount = System.Math.Min(WHEATTOBRINGHOME, available);

        // Create TakeFromStorageAction with callback to track result
        _takeWheatAction = new TakeFromStorageAction(
            _owner,
            this,
            _workplace,
            "wheat",
            actualAmount,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var takeAction = (TakeFromStorageAction)action;
                _actualWheatTaken = takeAction.ActualQuantity;
            }
        };

        return _takeWheatAction;
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
        if (_owner == null || _home == null)
        {
            Complete();
            return null;
        }

        // If we already have a deposit action in progress, check if it completed
        if (_depositWheatAction != null)
        {
            // Action was executed - check result
            int deposited = _depositWheatAction.ActualDeposited;
            if (deposited > 0)
            {
                var homeStorage = _home.GetStorage();
                Log.Print($"{_owner.Name}: Stored {deposited} wheat at home (Home: {homeStorage?.GetContentsSummary() ?? "unknown"})");
            }
            else
            {
                // Deposit failed - items stay in inventory
                Log.Warn($"{_owner.Name}: Home storage full, keeping wheat in inventory");
            }

            _depositWheatAction = null;
            Log.Print($"{_owner.Name}: Work day complete, harvest stored at home");
            DebugLog("ACTIVITY", "Wheat deposited at home, activity complete", 0);
            LogStorageInfo();
            Complete();
            return null;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Complete();
            return null;
        }

        // Check how much wheat we have to deposit
        int wheatCount = inventory.GetItemCount("wheat");
        if (wheatCount == 0)
        {
            Log.Print($"{_owner.Name}: Work day complete, no harvest to store");
            DebugLog("ACTIVITY", "No wheat in inventory, activity complete", 0);
            LogStorageInfo();
            Complete();
            return null;
        }

        // Create DepositToStorageAction to transfer items
        _depositWheatAction = new DepositToStorageAction(
            _owner,
            this,
            _home,
            "wheat",
            wheatCount,
            Priority);

        return _depositWheatAction;
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
