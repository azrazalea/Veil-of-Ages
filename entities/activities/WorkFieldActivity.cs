using System.Collections.Generic;
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
/// Uses a Stateless state machine to manage phase transitions and interruption/resumption.
///
/// States:
/// GoingToWork -> GoingToCrop -> Working -> TakingBreak? -> TakingWheat -> GoingHome -> DepositingWheat
///
/// Interruption behavior (two-zone regression):
/// - Work zone (GoingToCrop through TakingWheat): Interrupted -> GoingToWork (re-navigate to workplace)
/// - Home zone (GoingHome through DepositingWheat): Interrupted -> GoingHome (re-navigate home)
/// - GoingToWork itself: PermitReentry (already at nav entry point)
/// - TakingBreak: Interrupted -> GoingToWork (simpler to re-navigate)
///
/// Drains energy while working (restored by sleeping).
/// </summary>
public class WorkFieldActivity : StatefulActivity<WorkFieldActivity.WorkState, WorkFieldActivity.WorkTrigger>
{
    /// <summary>
    /// States representing the phases of field work.
    /// </summary>
    public enum WorkState
    {
        GoingToWork,
        GoingToCrop,
        Working,
        TakingBreak,
        TakingWheat,
        GoingHome,
        DepositingWheat,
    }

    /// <summary>
    /// Triggers that cause state transitions.
    /// </summary>
    public enum WorkTrigger
    {
        ArrivedAtWork,
        ArrivedAtCrop,
        WorkComplete,
        TakeBreak,
        BreakComplete,
        WheatTaken,
        ArrivedHome,
        WheatDeposited,
        DayEnded,
        Interrupted,
        Resumed,
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

    // Progress variables preserved across interruptions
    private uint _workTimer;
    private uint _ticksSinceLastWheat;
    private int _wheatProducedThisShift;
    private uint _variedWorkDuration;
    private uint _breakTimer;
    private ProduceToStorageAction? _pendingProduceAction;
    private Need? _energyNeed;

    protected override WorkTrigger InterruptedTrigger => WorkTrigger.Interrupted;

    protected override WorkTrigger ResumedTrigger => WorkTrigger.Resumed;

    public override string DisplayName => _machine.State switch
    {
        WorkState.GoingToWork => "Going to work",
        WorkState.GoingToCrop => "Going to crops",
        WorkState.Working => $"Working at {_workplace.BuildingType}",
        WorkState.TakingBreak => "Taking a break",
        WorkState.TakingWheat => "Gathering harvest",
        WorkState.GoingHome => "Bringing harvest home",
        WorkState.DepositingWheat => "Storing harvest",
        _ => "Working"
    };

    public override Building? TargetBuilding => _machine.State == WorkState.GoingHome ? _home : _workplace;

    public override string? TargetFacilityId => _machine.State is WorkState.Working or WorkState.TakingBreak ? "crop" : null;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        // Delegate to the crop navigation sub-activity if we're in a working phase
        if (_machine.State is WorkState.Working or WorkState.TakingBreak && _currentSubActivity != null)
        {
            return _currentSubActivity.GetAlternativeGoalPositions(entity);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkFieldActivity"/> class.
    /// Create an activity to work at a building and bring harvest home.
    /// </summary>
    /// <param name="workplace">The building to work at (farm, etc.)</param>
    /// <param name="home">The home building to deposit harvest (can be null).</param>
    /// <param name="workDuration">How many ticks to work before taking a break.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public WorkFieldActivity(Building workplace, Building? home, uint workDuration, int priority = 0)
        : base(WorkState.GoingToWork)
    {
        _workplace = workplace;
        _home = home;
        _workDuration = workDuration;
        Priority = priority;

        // Working makes you hungry faster
        NeedDecayMultipliers["hunger"] = 1.2f;

        ConfigureStateMachine();
    }

    /// <summary>
    /// Configures the state machine transitions, including interruption/resumption behavior.
    ///
    /// Work zone states (GoingToCrop, Working, TakingBreak, TakingWheat) regress to GoingToWork on interruption.
    /// Home zone states (DepositingWheat) regress to GoingHome on interruption.
    /// GoingToWork and GoingHome use PermitReentry for both Interrupted and Resumed to force fresh pathfinder creation.
    /// Sub-activity references are automatically nulled by the base class OnTransitioned callback.
    /// </summary>
    private void ConfigureStateMachine()
    {
        // GoingToWork: navigation state for work zone entry
        _machine.Configure(WorkState.GoingToWork)
            .Permit(WorkTrigger.ArrivedAtWork, WorkState.GoingToCrop)
            .PermitReentry(WorkTrigger.Interrupted) // Already at nav entry, re-enter to force fresh pathfinder
            .PermitReentry(WorkTrigger.Resumed); // Re-enter to force fresh pathfinder

        // GoingToCrop: navigation to crop facility within workplace
        _machine.Configure(WorkState.GoingToCrop)
            .Permit(WorkTrigger.ArrivedAtCrop, WorkState.Working)
            .Permit(WorkTrigger.DayEnded, WorkState.TakingWheat)
            .Permit(WorkTrigger.Interrupted, WorkState.GoingToWork); // Work zone regression

        // Working: idle work timer phase
        _machine.Configure(WorkState.Working)
            .Permit(WorkTrigger.WorkComplete, WorkState.TakingWheat)
            .Permit(WorkTrigger.TakeBreak, WorkState.TakingBreak)
            .Permit(WorkTrigger.DayEnded, WorkState.TakingWheat)
            .Permit(WorkTrigger.Interrupted, WorkState.GoingToWork); // Work zone regression

        // TakingBreak: optional short break after work segment
        _machine.Configure(WorkState.TakingBreak)
            .Permit(WorkTrigger.BreakComplete, WorkState.TakingWheat)
            .Permit(WorkTrigger.Interrupted, WorkState.GoingToWork); // Work zone regression

        // TakingWheat: take items from farm storage into inventory
        _machine.Configure(WorkState.TakingWheat)
            .Permit(WorkTrigger.WheatTaken, WorkState.GoingHome)
            .Permit(WorkTrigger.Interrupted, WorkState.GoingToWork); // Work zone regression

        // GoingHome: navigation state for home zone entry
        _machine.Configure(WorkState.GoingHome)
            .Permit(WorkTrigger.ArrivedHome, WorkState.DepositingWheat)
            .PermitReentry(WorkTrigger.Interrupted) // Home zone nav entry, re-enter to force fresh pathfinder
            .PermitReentry(WorkTrigger.Resumed); // Re-enter to force fresh pathfinder

        // DepositingWheat: deposit items from inventory to home storage
        _machine.Configure(WorkState.DepositingWheat)
            .Permit(WorkTrigger.Interrupted, WorkState.GoingHome); // Home zone regression

        // WheatDeposited is handled by Complete() directly, no state transition needed
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
            if (_machine.State is WorkState.Working or WorkState.GoingToCrop)
            {
                Log.Print($"{_owner.Name}: Work time ended, gathering harvest to bring home");
                _machine.Fire(WorkTrigger.DayEnded);
            }
        }

        return _machine.State switch
        {
            WorkState.GoingToWork => ProcessGoingToWork(position, perception),
            WorkState.GoingToCrop => ProcessGoingToCrop(position, perception),
            WorkState.Working => ProcessWorking(),
            WorkState.TakingBreak => ProcessTakingBreak(),
            WorkState.TakingWheat => ProcessTakingWheat(position, perception),
            WorkState.GoingHome => ProcessGoingHome(position, perception),
            WorkState.DepositingWheat => ProcessDepositingWheat(position, perception),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToWork(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Run the navigation sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("ACTIVITY", $"Starting navigation to workplace: {_workplace.BuildingName}", 0);
                return new GoToBuildingActivity(_workplace, Priority);
            },
            position, perception);
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
            _machine.Fire(WorkTrigger.ArrivedAtWork);
        }
        else
        {
            // No crop facility defined - fire ArrivedAtWork to get to GoingToCrop,
            // then immediately fire ArrivedAtCrop to skip to Working
            _machine.Fire(WorkTrigger.ArrivedAtWork);
            _machine.Fire(WorkTrigger.ArrivedAtCrop);
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

        // Run the navigation sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("ACTIVITY", $"Starting navigation to crop facility at {_workplace.BuildingName}", 0);
                return new GoToFacilityActivity(_workplace, "crop", Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Fall back to working at current position
                Log.Warn($"{_owner.Name}: Could not reach crop facility, working at current position");
                DebugLog("ACTIVITY", "Failed to reach crop facility, starting work phase anyway", 0);
                _machine.Fire(WorkTrigger.ArrivedAtCrop);
                LogStorageInfo();
                return new IdleAction(_owner, this, Priority);
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at crop
        _machine.Fire(WorkTrigger.ArrivedAtCrop);
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

        // Grant farming skill XP while working
        _owner.SkillSystem?.GainXp("farming", 0.01f);

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
                DebugLog("ACTIVITY", $"Taking a short break ({_breakTimer} ticks)", 0);
                _machine.Fire(WorkTrigger.TakeBreak);
                return new IdleAction(_owner, this, Priority);
            }

            // No break, proceed to taking wheat
            _machine.Fire(WorkTrigger.WorkComplete);
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
            _machine.Fire(WorkTrigger.BreakComplete);
            return new IdleAction(_owner, this, Priority);
        }

        // During break, just idle (normal need decay applies)
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingWheat(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check availability before creating sub-activity (only when _currentSubActivity is null)
        if (_currentSubActivity == null)
        {
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
                _machine.Fire(WorkTrigger.WheatTaken);
                return new IdleAction(_owner, this, Priority);
            }
        }

        // Run the take sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                int available = _owner!.GetStorageItemCount(_workplace, "wheat");
                int actualAmount = System.Math.Min(WHEATTOBRINGHOME, available);
                var itemsToTake = new List<(string itemId, int quantity)> { ("wheat", actualAmount) };
                DebugLog("ACTIVITY", $"Taking up to {actualAmount} wheat from farm", 0);
                return new TakeFromStorageActivity(_workplace, itemsToTake, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Take failed - log warning but continue (no wheat to bring)
                Log.Warn($"{_owner.Name}: Failed to take wheat from farm storage");

                // If no home, just complete here
                if (_home == null || !GodotObject.IsInstanceValid(_home))
                {
                    Log.Print($"{_owner.Name}: No home to bring harvest to, shift complete");
                    DebugLog("ACTIVITY", "No home to bring harvest to, completing activity", 0);
                    Complete();
                    return null;
                }

                DebugLog("ACTIVITY", "Take failed, transitioning to GoingHome anyway", 0);
                _machine.Fire(WorkTrigger.WheatTaken);
                return new IdleAction(_owner, this, Priority);

            case SubActivityResult.Continue:
                return action;

            case SubActivityResult.Completed:
                // Check how much wheat we actually have in inventory now
                var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
                int wheatInInventory = inventory?.GetItemCount("wheat") ?? 0;

                if (wheatInInventory > 0)
                {
                    var farmStorage = _workplace.GetStorage();
                    Log.Print($"{_owner.Name}: Took {wheatInInventory} wheat to bring home (Farm: {farmStorage?.GetContentsSummary() ?? "unknown"}, Inventory: {inventory?.GetContentsSummary() ?? "unknown"})");
                }

                // If no home, just complete here
                if (_home == null || !GodotObject.IsInstanceValid(_home))
                {
                    Log.Print($"{_owner.Name}: No home to bring harvest to, shift complete");
                    DebugLog("ACTIVITY", "No home to bring harvest to, completing activity", 0);
                    Complete();
                    return null;
                }

                DebugLog("ACTIVITY", "Wheat taken, transitioning to GoingHome", 0);
                _machine.Fire(WorkTrigger.WheatTaken);
                return new IdleAction(_owner, this, Priority);
        }

        return null;
    }

    private EntityAction? ProcessGoingHome(Vector2I position, Perception perception)
    {
        if (_owner == null || _home == null)
        {
            Complete();
            return null;
        }

        // Run the navigation sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("ACTIVITY", $"Starting navigation to home: {_home!.BuildingName}", 0);
                return new GoToBuildingActivity(_home, Priority, targetStorage: true);
            },
            position, perception);
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
        _machine.Fire(WorkTrigger.ArrivedHome);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingWheat(Vector2I position, Perception perception)
    {
        if (_owner == null || _home == null)
        {
            Complete();
            return null;
        }

        // Check inventory before creating sub-activity (only when _currentSubActivity is null)
        if (_currentSubActivity == null)
        {
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
        }

        // Run the deposit sub-activity (created lazily via factory)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                var inventory = _owner!.SelfAsEntity().GetTrait<InventoryTrait>();
                int wheatCount = inventory?.GetItemCount("wheat") ?? 0;
                var itemsToDeposit = new List<(string itemId, int quantity)> { ("wheat", wheatCount) };
                DebugLog("ACTIVITY", $"Depositing {wheatCount} wheat to home", 0);
                return new DepositToStorageActivity(_home!, itemsToDeposit, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Deposit failed - items stay in inventory
                Log.Warn($"{_owner.Name}: Home storage full, keeping wheat in inventory");
                Log.Print($"{_owner.Name}: Work day complete, harvest stored at home");
                DebugLog("ACTIVITY", "Deposit failed, activity complete", 0);
                LogStorageInfo();
                Complete();
                return null;

            case SubActivityResult.Continue:
                return action;

            case SubActivityResult.Completed:
                var homeStorage = _home.GetStorage();
                Log.Print($"{_owner.Name}: Stored wheat at home (Home: {homeStorage?.GetContentsSummary() ?? "unknown"})");
                Log.Print($"{_owner.Name}: Work day complete, harvest stored at home");
                DebugLog("ACTIVITY", "Wheat deposited at home, activity complete", 0);
                LogStorageInfo();
                Complete();
                return null;
        }

        return null;
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
