using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for one distribution round.
/// The distributor visits all households to:
/// 1. Deliver items they need (from granary stock)
/// 2. Collect excess items (bread, wheat) to bring back to granary.
///
/// Uses a Stateless state machine to manage phase transitions and interruption/resumption.
///
/// States (simplified from 10 to 8 by folding navigation into observation states):
/// CheckingGranaryStock → ReadingOrders → LoadingDeliveryItems →
/// CheckingHousehold → ExchangingItems → TakingBreak →
/// ReturningToGranary → DepositingCollected
///
/// Three-zone regression on interruption:
/// - Granary zone (ReadingOrders, LoadingDeliveryItems) regresses to CheckingGranaryStock
/// - Household zone (ExchangingItems, TakingBreak) regresses to CheckingHousehold
/// - Return zone (DepositingCollected) regresses to ReturningToGranary
///
/// Navigation states (CheckingGranaryStock, CheckingHousehold, ReturningToGranary) use
/// PermitReentry to force fresh sub-activity creation on interruption and resumption.
/// CheckStorageActivity handles cross-area navigation + storage observation in one step.
/// </summary>
public class DistributorRoundActivity : StatefulActivity<DistributorRoundActivity.DistributionState, DistributorRoundActivity.DistributionTrigger>
{
    public enum DistributionState
    {
        CheckingGranaryStock,
        ReadingOrders,
        LoadingDeliveryItems,
        CheckingHousehold,
        ExchangingItems,
        TakingBreak,
        ReturningToGranary,
        DepositingCollected,
    }

    public enum DistributionTrigger
    {
        StockChecked,
        OrdersRead,
        ItemsLoaded,
        HouseholdChecked,
        ExchangeComplete,
        BreakComplete,
        ArrivedBackAtGranary,
        ItemsDeposited,
        NoHouseholdsToVisit,
        TakeBreak,
        Interrupted,
        Resumed,
    }

    private const uint READINGDURATION = 30;
    private const float BREAKPROBABILITY = 0.15f;
    private const uint MINBREAKDURATION = 20;
    private const uint MAXBREAKDURATION = 50;

    // Excess thresholds - collect items above these amounts
    private const int BREADEXCESSTHRESHOLD = 2;
    private const int WHEATEXCESSTHRESHOLD = 5;

    private readonly Building _granary;

    private uint _phaseTimer;

    // All households to visit (from standing orders)
    private readonly List<Building> _householdsToVisit = new ();
    private int _currentHouseholdIndex;

    // What we're carrying to deliver (loaded from granary)
    private readonly Dictionary<string, int> _itemsToDeliver = new ();

    // What we've collected from households (to deposit at granary)
    private readonly Dictionary<string, int> _collectedItems = new ();

    // Standing orders reference for checking desired quantities
    private StandingOrders? _standingOrders;

    // Loading phase state
    private List<(string itemId, int quantity)>? _itemsToLoad;
    private int _currentLoadIndex;
    private TransferBetweenStoragesAction? _loadAction;
    private int _lastLoadedQuantity;

    // Exchanging phase state
    private enum ExchangeSubPhase
    {
        Delivering,
        CollectingBread,
        CollectingWheat,
        Done
    }

    private ExchangeSubPhase _exchangeSubPhase;
    private List<DeliveryTarget>? _currentHouseholdTargets;
    private int _currentDeliveryTargetIndex;
    private DepositToStorageAction? _deliverAction;
    private int _lastDeliveredQuantity;
    private TakeFromStorageAction? _collectAction;
    private int _lastCollectedQuantity;

    // Depositing phase state
    private List<(string itemId, int quantity)>? _itemsToDeposit;
    private int _currentDepositIndex;
    private DepositToStorageAction? _depositAction;
    private int _lastDepositedQuantity;

    protected override DistributionTrigger InterruptedTrigger => DistributionTrigger.Interrupted;

    protected override DistributionTrigger ResumedTrigger => DistributionTrigger.Resumed;

    public override string DisplayName => _machine.State switch
    {
        DistributionState.CheckingGranaryStock => L.Tr("activity.CHECKING_GRANARY"),
        DistributionState.ReadingOrders => L.Tr("activity.READING_ORDERS"),
        DistributionState.LoadingDeliveryItems => L.Tr("activity.LOADING_DELIVERY"),
        DistributionState.CheckingHousehold => L.Tr("activity.CHECKING_HOUSEHOLD"),
        DistributionState.ExchangingItems => L.Tr("activity.EXCHANGING_ITEMS"),
        DistributionState.TakingBreak => L.Tr("activity.TAKING_BREAK"),
        DistributionState.ReturningToGranary => L.Tr("activity.RETURNING_TO_GRANARY"),
        DistributionState.DepositingCollected => L.Tr("activity.DEPOSITING_COLLECTED"),
        _ => L.Tr("activity.DISTRIBUTING")
    };

    public override Building? TargetBuilding => _machine.State switch
    {
        DistributionState.CheckingHousehold or DistributionState.ExchangingItems
            => _currentHouseholdIndex < _householdsToVisit.Count ? _householdsToVisit[_currentHouseholdIndex] : _granary,
        _ => _granary
    };

    public DistributorRoundActivity(Building granary, int priority = 0)
        : base(DistributionState.CheckingGranaryStock)
    {
        _granary = granary;
        Priority = priority;

        // Distributors get hungry faster (lots of walking)
        NeedDecayMultipliers["hunger"] = 1.3f;

        ConfigureStateMachine();
    }

    /// <summary>
    /// Configures the state machine transitions, including interruption/resumption behavior.
    ///
    /// Three-zone regression:
    /// - Granary zone: ReadingOrders, LoadingDeliveryItems regress to CheckingGranaryStock
    /// - Household zone: ExchangingItems, TakingBreak regress to CheckingHousehold
    /// - Return zone: DepositingCollected regresses to ReturningToGranary
    ///
    /// Navigation+observation states (CheckingGranaryStock, CheckingHousehold) use PermitReentry
    /// for both Interrupted and Resumed to force fresh CheckStorageActivity creation.
    /// ReturningToGranary uses PermitReentry for fresh navigation.
    /// Sub-activity references are automatically nulled by the base class OnTransitioned callback.
    /// </summary>
    private void ConfigureStateMachine()
    {
        // CheckingGranaryStock: navigation + observation via CheckStorageActivity (granary zone entry)
        _machine.Configure(DistributionState.CheckingGranaryStock)
            .Permit(DistributionTrigger.StockChecked, DistributionState.ReadingOrders)
            .PermitReentry(DistributionTrigger.Interrupted)
            .PermitReentry(DistributionTrigger.Resumed);

        // ReadingOrders: timed work phase at granary
        _machine.Configure(DistributionState.ReadingOrders)
            .Permit(DistributionTrigger.OrdersRead, DistributionState.LoadingDeliveryItems)
            .Permit(DistributionTrigger.Interrupted, DistributionState.CheckingGranaryStock);

        // LoadingDeliveryItems: transfer items from granary to inventory
        _machine.Configure(DistributionState.LoadingDeliveryItems)
            .Permit(DistributionTrigger.ItemsLoaded, DistributionState.CheckingHousehold)
            .Permit(DistributionTrigger.NoHouseholdsToVisit, DistributionState.ReturningToGranary)
            .Permit(DistributionTrigger.Interrupted, DistributionState.CheckingGranaryStock);

        // CheckingHousehold: navigation + observation via CheckStorageActivity
        _machine.Configure(DistributionState.CheckingHousehold)
            .Permit(DistributionTrigger.HouseholdChecked, DistributionState.ExchangingItems)
            .Permit(DistributionTrigger.NoHouseholdsToVisit, DistributionState.ReturningToGranary)
            .PermitReentry(DistributionTrigger.Interrupted)
            .PermitReentry(DistributionTrigger.Resumed);

        // ExchangingItems: deliver and collect items at household
        _machine.Configure(DistributionState.ExchangingItems)
            .Permit(DistributionTrigger.ExchangeComplete, DistributionState.CheckingHousehold)
            .Permit(DistributionTrigger.TakeBreak, DistributionState.TakingBreak)
            .Permit(DistributionTrigger.NoHouseholdsToVisit, DistributionState.ReturningToGranary)
            .Permit(DistributionTrigger.Interrupted, DistributionState.CheckingHousehold);

        // TakingBreak: short rest between households
        _machine.Configure(DistributionState.TakingBreak)
            .Permit(DistributionTrigger.BreakComplete, DistributionState.CheckingHousehold)
            .Permit(DistributionTrigger.Interrupted, DistributionState.CheckingHousehold);

        // ReturningToGranary: navigation via GoToBuildingActivity (cross-area capable)
        _machine.Configure(DistributionState.ReturningToGranary)
            .Permit(DistributionTrigger.ArrivedBackAtGranary, DistributionState.DepositingCollected)
            .PermitReentry(DistributionTrigger.Interrupted)
            .PermitReentry(DistributionTrigger.Resumed);

        // DepositingCollected: deposit collected items at granary
        _machine.Configure(DistributionState.DepositingCollected)
            .Permit(DistributionTrigger.Interrupted, DistributionState.ReturningToGranary);
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);
        DebugLog("DISTRIBUTOR", $"Started distribution round at {_granary.BuildingName}", 0);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        if (!GodotObject.IsInstanceValid(_granary))
        {
            Fail();
            return null;
        }

        return _machine.State switch
        {
            DistributionState.CheckingGranaryStock => ProcessCheckingGranaryStock(position, perception),
            DistributionState.ReadingOrders => ProcessReadingOrders(),
            DistributionState.LoadingDeliveryItems => ProcessLoadingDeliveryItems(),
            DistributionState.CheckingHousehold => ProcessCheckingHousehold(position, perception),
            DistributionState.ExchangingItems => ProcessExchangingItems(),
            DistributionState.TakingBreak => ProcessTakingBreak(),
            DistributionState.ReturningToGranary => ProcessReturningToGranary(position, perception),
            DistributionState.DepositingCollected => ProcessDepositingCollected(),
            _ => null
        };
    }

    private EntityAction? ProcessCheckingGranaryStock(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Resolve granary to its storage facility for CheckStorageActivity
        var granaryStorage = _granary.GetDefaultRoom()?.GetStorageFacility();
        if (granaryStorage == null)
        {
            DebugLog("DISTRIBUTOR", "Granary has no storage facility", 0);
            Fail();
            return null;
        }

        // Use CheckStorageActivity to navigate to granary and observe storage
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("DISTRIBUTOR", $"Starting CheckStorageActivity for granary: {_granary.BuildingName}", 0);
                return new CheckStorageActivity(granaryStorage, Priority);
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
                break;
        }

        // Storage observed, set up reading phase timer
        _phaseTimer = ActivityTiming.GetVariedDuration(READINGDURATION, 0.2f);
        DebugLog("DISTRIBUTOR", "Granary stock checked, reading orders", 0);
        _machine.Fire(DistributionTrigger.StockChecked);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessReadingOrders()
    {
        if (_owner == null)
        {
            return null;
        }

        _phaseTimer--;
        if (_phaseTimer > 0)
        {
            return new IdleAction(_owner, this, Priority);
        }

        var granaryTrait = _granary.Traits.OfType<GranaryTrait>().FirstOrDefault();
        if (granaryTrait == null)
        {
            DebugLog("DISTRIBUTOR", "Granary has no GranaryTrait, completing round", 0);
            Complete();
            return null;
        }

        _standingOrders = granaryTrait.Orders;

        // Build list of all households to visit
        _householdsToVisit.Clear();
        var targets = _standingOrders.GetDeliveryTargets();
        DebugLog("DISTRIBUTOR", $"Standing orders has {targets.Count} delivery targets", 0);
        foreach (var target in targets)
        {
            DebugLog("DISTRIBUTOR", $"  Order: {target.Household.BuildingName} (hash: {target.Household.GetHashCode()}) wants {target.DesiredQuantity}x {target.ItemId}", 0);
            if (!_householdsToVisit.Contains(target.Household) && GodotObject.IsInstanceValid(target.Household))
            {
                _householdsToVisit.Add(target.Household);
                DebugLog("DISTRIBUTOR", $"  Added to visit list: {target.Household.BuildingName} (hash: {target.Household.GetHashCode()})", 0);
            }
        }

        DebugLog("DISTRIBUTOR", $"Will visit {_householdsToVisit.Count} households", 0);
        foreach (var h in _householdsToVisit)
        {
            DebugLog("DISTRIBUTOR", $"  To visit: {h.BuildingName} (hash: {h.GetHashCode()})", 0);
        }

        _machine.Fire(DistributionTrigger.OrdersRead);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessLoadingDeliveryItems()
    {
        if (_owner == null)
        {
            return null;
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Fail();
            return null;
        }

        // First call: calculate what needs to be loaded
        if (_itemsToLoad == null)
        {
            _itemsToDeliver.Clear();
            _itemsToLoad = new List<(string itemId, int quantity)>();

            if (_standingOrders != null)
            {
                var targets = _standingOrders.GetDeliveryTargets();

                var neededByItem = new Dictionary<string, int>();
                foreach (var target in targets)
                {
                    int householdHas = _owner.GetStorageItemCount(target.Household, target.ItemId);
                    int needed = target.DesiredQuantity - householdHas;
                    if (needed > 0)
                    {
                        neededByItem.TryGetValue(target.ItemId, out int existing);
                        neededByItem[target.ItemId] = existing + needed;
                    }
                }

                foreach (var (itemId, quantity) in neededByItem)
                {
                    _itemsToLoad.Add((itemId, quantity));
                }
            }

            _currentLoadIndex = 0;
            DebugLog("DISTRIBUTOR", $"Need to load {_itemsToLoad.Count} item types from granary", 0);
        }

        // Check if previous load action completed
        if (_loadAction != null)
        {
            if (_lastLoadedQuantity > 0)
            {
                var (itemId, _) = _itemsToLoad[_currentLoadIndex];
                _itemsToDeliver[itemId] = _lastLoadedQuantity;
                DebugLog("DISTRIBUTOR", $"Loaded {_lastLoadedQuantity}x {itemId} for delivery", 0);
            }

            _loadAction = null;
            _lastLoadedQuantity = 0;
            _currentLoadIndex++;
        }

        // Check if we've loaded all items
        if (_currentLoadIndex >= _itemsToLoad.Count)
        {
            _itemsToLoad = null;
            _currentHouseholdIndex = 0;

            if (_householdsToVisit.Count > 0)
            {
                _machine.Fire(DistributionTrigger.ItemsLoaded);
            }
            else
            {
                DebugLog("DISTRIBUTOR", "No households to visit", 0);
                _machine.Fire(DistributionTrigger.NoHouseholdsToVisit);
            }

            return new IdleAction(_owner, this, Priority);
        }

        // Load next item type from granary
        var (nextItemId, neededQuantity) = _itemsToLoad[_currentLoadIndex];

        int available = _owner.GetStorageItemCount(_granary, nextItemId);
        if (available == 0)
        {
            _currentLoadIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int amountToLoad = System.Math.Min(neededQuantity, available);

        var granaryFacility = _granary.GetDefaultRoom()?.GetStorageFacility();
        if (granaryFacility == null)
        {
            DebugLog("DISTRIBUTOR", "Granary has no storage facility", 0);
            return new IdleAction(_owner, this, Priority);
        }

        _loadAction = TransferBetweenStoragesAction.FromFacility(
            _owner,
            this,
            granaryFacility,
            nextItemId,
            amountToLoad,
            Priority);
        _loadAction.OnSuccessful = (action) =>
        {
            var transferAction = (TransferBetweenStoragesAction)action;
            _lastLoadedQuantity = transferAction.ActualTransferred;
        };

        return _loadAction;
    }

    private EntityAction? ProcessCheckingHousehold(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if we've visited all households
        if (_currentHouseholdIndex >= _householdsToVisit.Count)
        {
            _machine.Fire(DistributionTrigger.NoHouseholdsToVisit);
            return new IdleAction(_owner, this, Priority);
        }

        var household = _householdsToVisit[_currentHouseholdIndex];

        if (!GodotObject.IsInstanceValid(household))
        {
            _currentHouseholdIndex++;
            _currentSubActivity = null;
            return new IdleAction(_owner, this, Priority);
        }

        // Resolve household to its storage facility for CheckStorageActivity
        var householdStorage = household.GetDefaultRoom()?.GetStorageFacility();
        if (householdStorage == null)
        {
            DebugLog("DISTRIBUTOR", $"Household {household.BuildingName} has no storage facility, skipping", 0);
            _currentHouseholdIndex++;
            _currentSubActivity = null;
            return new IdleAction(_owner, this, Priority);
        }

        // Use CheckStorageActivity to navigate to household and observe storage
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("DISTRIBUTOR", $"Starting CheckStorageActivity for household: {household.BuildingName}", 0);
                return new CheckStorageActivity(householdStorage, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Skip this household on failure
                _currentHouseholdIndex++;
                _currentSubActivity = null;
                return new IdleAction(_owner, this, Priority);
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        DebugLog("DISTRIBUTOR", $"Checked {household.BuildingName}", 0);
        _machine.Fire(DistributionTrigger.HouseholdChecked);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessExchangingItems()
    {
        if (_owner == null)
        {
            return null;
        }

        if (_currentHouseholdIndex >= _householdsToVisit.Count)
        {
            _machine.Fire(DistributionTrigger.NoHouseholdsToVisit);
            return new IdleAction(_owner, this, Priority);
        }

        var household = _householdsToVisit[_currentHouseholdIndex];
        if (!GodotObject.IsInstanceValid(household))
        {
            MoveToNextHousehold();
            return new IdleAction(_owner, this, Priority);
        }

        // Initialize exchange sub-phase state on first call for this household
        if (_currentHouseholdTargets == null)
        {
            _exchangeSubPhase = ExchangeSubPhase.Delivering;
            _currentDeliveryTargetIndex = 0;

            if (_standingOrders != null)
            {
                _currentHouseholdTargets = _standingOrders.GetDeliveryTargets()
                    .Where(t => t.Household == household)
                    .ToList();
                DebugLog("DISTRIBUTOR", $"Matched {_currentHouseholdTargets.Count} targets for {household.BuildingName}", 0);
            }
            else
            {
                _currentHouseholdTargets = new List<DeliveryTarget>();
            }
        }

        return _exchangeSubPhase switch
        {
            ExchangeSubPhase.Delivering => ProcessDelivering(household),
            ExchangeSubPhase.CollectingBread => ProcessCollectingBread(household),
            ExchangeSubPhase.CollectingWheat => ProcessCollectingWheat(household),
            ExchangeSubPhase.Done => ProcessExchangeDone(),
            _ => new IdleAction(_owner, this, Priority)
        };
    }

    private EntityAction? ProcessDelivering(Building household)
    {
        if (_owner == null || _currentHouseholdTargets == null)
        {
            return null;
        }

        // Check if previous delivery action completed
        if (_deliverAction != null)
        {
            // Action was executed - record results
            if (_lastDeliveredQuantity > 0)
            {
                var target = _currentHouseholdTargets[_currentDeliveryTargetIndex];
                DebugLog("DISTRIBUTOR", $"Delivered {_lastDeliveredQuantity}x {target.ItemId} to {household.BuildingName}", 0);
            }

            _deliverAction = null;
            _lastDeliveredQuantity = 0;
            _currentDeliveryTargetIndex++;
        }

        // Check if all targets delivered
        if (_currentDeliveryTargetIndex >= _currentHouseholdTargets.Count)
        {
            // Move to collecting bread
            _exchangeSubPhase = ExchangeSubPhase.CollectingBread;
            return new IdleAction(_owner, this, Priority);
        }

        // Deliver to next target
        var nextTarget = _currentHouseholdTargets[_currentDeliveryTargetIndex];

        // Check how much household already has (from memory observation in CheckingHousehold phase)
        int householdHas = _owner.GetStorageItemCount(household, nextTarget.ItemId);
        int needed = nextTarget.DesiredQuantity - householdHas;

        if (needed <= 0)
        {
            // Household doesn't need more of this item
            _currentDeliveryTargetIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        // Check how much we have to deliver
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            _currentDeliveryTargetIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int haveInInventory = inventory.GetItemCount(nextTarget.ItemId);
        if (haveInInventory == 0)
        {
            // Don't have any of this item to deliver
            _currentDeliveryTargetIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int amountToDeliver = System.Math.Min(needed, haveInInventory);

        // Create deposit action to deliver items
        var householdFacility = household.GetDefaultRoom()?.GetStorageFacility();
        if (householdFacility == null)
        {
            DebugLog("DISTRIBUTOR", $"Household {household.BuildingName} has no storage facility", 0);
            return new IdleAction(_owner, this, Priority);
        }

        _deliverAction = new DepositToStorageAction(
            _owner,
            this,
            householdFacility,
            nextTarget.ItemId,
            amountToDeliver,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var depositAction = (DepositToStorageAction)action;
                _lastDeliveredQuantity = depositAction.ActualDeposited;
            }
        };

        return _deliverAction;
    }

    private EntityAction? ProcessCollectingBread(Building household)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if previous collect action completed
        if (_collectAction != null)
        {
            if (_lastCollectedQuantity > 0)
            {
                // Track collected items for later deposit
                _collectedItems.TryGetValue("bread", out int existing);
                _collectedItems["bread"] = existing + _lastCollectedQuantity;
                DebugLog("DISTRIBUTOR", $"Collected {_lastCollectedQuantity}x excess bread from {household.BuildingName}", 0);
            }

            _collectAction = null;
            _lastCollectedQuantity = 0;

            // Move to collecting wheat
            _exchangeSubPhase = ExchangeSubPhase.CollectingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        // Check how much bread household has
        int breadCount = _owner.GetStorageItemCount(household, "bread");
        if (breadCount <= BREADEXCESSTHRESHOLD)
        {
            // No excess bread to collect
            _exchangeSubPhase = ExchangeSubPhase.CollectingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        int excessBread = breadCount - BREADEXCESSTHRESHOLD;

        var householdFacilityBread = household.GetDefaultRoom()?.GetStorageFacility();
        if (householdFacilityBread == null)
        {
            DebugLog("DISTRIBUTOR", $"Household {household.BuildingName} has no storage facility for bread collection", 0);
            _exchangeSubPhase = ExchangeSubPhase.CollectingWheat;
            return new IdleAction(_owner, this, Priority);
        }

        _collectAction = new TakeFromStorageAction(
            _owner,
            this,
            householdFacilityBread,
            "bread",
            excessBread,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var takeAction = (TakeFromStorageAction)action;
                _lastCollectedQuantity = takeAction.ActualQuantity;
            }
        };

        return _collectAction;
    }

    private EntityAction? ProcessCollectingWheat(Building household)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if previous collect action completed
        if (_collectAction != null)
        {
            if (_lastCollectedQuantity > 0)
            {
                // Track collected items for later deposit
                _collectedItems.TryGetValue("wheat", out int existing);
                _collectedItems["wheat"] = existing + _lastCollectedQuantity;
                DebugLog("DISTRIBUTOR", $"Collected {_lastCollectedQuantity}x excess wheat from {household.BuildingName}", 0);
            }

            _collectAction = null;
            _lastCollectedQuantity = 0;

            // Done with this household
            _exchangeSubPhase = ExchangeSubPhase.Done;
            return new IdleAction(_owner, this, Priority);
        }

        // Check how much wheat household has
        int wheatCount = _owner.GetStorageItemCount(household, "wheat");
        if (wheatCount <= WHEATEXCESSTHRESHOLD)
        {
            // No excess wheat to collect
            _exchangeSubPhase = ExchangeSubPhase.Done;
            return new IdleAction(_owner, this, Priority);
        }

        int excessWheat = wheatCount - WHEATEXCESSTHRESHOLD;

        var householdFacilityWheat = household.GetDefaultRoom()?.GetStorageFacility();
        if (householdFacilityWheat == null)
        {
            DebugLog("DISTRIBUTOR", $"Household {household.BuildingName} has no storage facility for wheat collection", 0);
            _exchangeSubPhase = ExchangeSubPhase.Done;
            return new IdleAction(_owner, this, Priority);
        }

        _collectAction = new TakeFromStorageAction(
            _owner,
            this,
            householdFacilityWheat,
            "wheat",
            excessWheat,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var takeAction = (TakeFromStorageAction)action;
                _lastCollectedQuantity = takeAction.ActualQuantity;
            }
        };

        return _collectAction;
    }

    private EntityAction? ProcessExchangeDone()
    {
        if (_owner == null)
        {
            return null;
        }

        _currentHouseholdTargets = null;
        MoveToNextHousehold();
        return new IdleAction(_owner, this, Priority);
    }

    private void MoveToNextHousehold()
    {
        _currentHouseholdIndex++;

        if (_currentHouseholdIndex < _householdsToVisit.Count)
        {
            if (ActivityTiming.ShouldTakeBreak(BREAKPROBABILITY))
            {
                _phaseTimer = ActivityTiming.GetBreakDuration(MINBREAKDURATION, MAXBREAKDURATION);
                _machine.Fire(DistributionTrigger.TakeBreak);
                DebugLog("DISTRIBUTOR", $"Taking a short break ({_phaseTimer} ticks)", 0);
            }
            else
            {
                _machine.Fire(DistributionTrigger.ExchangeComplete);
            }
        }
        else
        {
            _machine.Fire(DistributionTrigger.NoHouseholdsToVisit);
        }
    }

    private EntityAction? ProcessTakingBreak()
    {
        if (_owner == null)
        {
            return null;
        }

        _phaseTimer--;
        if (_phaseTimer > 0)
        {
            return new IdleAction(_owner, this, Priority);
        }

        DebugLog("DISTRIBUTOR", "Break finished, continuing rounds", 0);
        _machine.Fire(DistributionTrigger.BreakComplete);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessReturningToGranary(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // Use GoToBuildingActivity for cross-area capable navigation back to granary
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("DISTRIBUTOR", $"Starting navigation back to granary: {_granary.BuildingName}", 0);
                return new GoToBuildingActivity(_granary, Priority, targetStorage: true);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Complete();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        // Back at granary
        int totalCollected = _collectedItems.Values.Sum();
        if (totalCollected > 0)
        {
            _machine.Fire(DistributionTrigger.ArrivedBackAtGranary);
            DebugLog("DISTRIBUTOR", $"Back at granary, depositing {_collectedItems.Count} collected item types ({totalCollected} total items)", 0);
        }
        else
        {
            DebugLog("DISTRIBUTOR", "Round complete, no items to deposit", 0);
            Complete();
        }

        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingCollected()
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // First call: build deposit list from collected items
        if (_itemsToDeposit == null)
        {
            _itemsToDeposit = new List<(string itemId, int quantity)>();

            foreach (var (itemId, quantity) in _collectedItems)
            {
                if (quantity > 0)
                {
                    _itemsToDeposit.Add((itemId, quantity));
                }
            }

            _currentDepositIndex = 0;
            DebugLog("DISTRIBUTOR", $"Have {_itemsToDeposit.Count} item types to deposit at granary", 0);
        }

        // Check if previous deposit action completed
        if (_depositAction != null)
        {
            if (_lastDepositedQuantity > 0)
            {
                var (itemId, _) = _itemsToDeposit[_currentDepositIndex];
                DebugLog("DISTRIBUTOR", $"Deposited {_lastDepositedQuantity}x {itemId} to granary", 0);
            }
            else
            {
                var (itemId, _) = _itemsToDeposit[_currentDepositIndex];
                DebugLog("DISTRIBUTOR", $"Granary full, couldn't deposit {itemId}", 0);
            }

            _depositAction = null;
            _lastDepositedQuantity = 0;
            _currentDepositIndex++;
        }

        // Check if we've deposited all items
        if (_currentDepositIndex >= _itemsToDeposit.Count)
        {
            _itemsToDeposit = null;
            _collectedItems.Clear();
            DebugLog("DISTRIBUTOR", "Round complete!", 0);
            Complete();
            return null;
        }

        // Deposit next item type to granary
        var (nextItemId, _) = _itemsToDeposit[_currentDepositIndex];

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            _currentDepositIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int haveInInventory = inventory.GetItemCount(nextItemId);
        if (haveInInventory == 0)
        {
            // Don't have this item in inventory (may have been lost)
            _currentDepositIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        var granaryDepositFacility = _granary.GetDefaultRoom()?.GetStorageFacility();
        if (granaryDepositFacility == null)
        {
            DebugLog("DISTRIBUTOR", "Granary has no storage facility for deposit", 0);
            _currentDepositIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        _depositAction = new DepositToStorageAction(
            _owner,
            this,
            granaryDepositFacility,
            nextItemId,
            haveInInventory,
            Priority)
        {
            OnSuccessful = (action) =>
            {
                var depositAct = (DepositToStorageAction)action;
                _lastDepositedQuantity = depositAct.ActualDeposited;
            }
        };

        return _depositAction;
    }
}
