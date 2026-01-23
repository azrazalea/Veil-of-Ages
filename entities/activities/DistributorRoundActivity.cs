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
/// </summary>
public class DistributorRoundActivity : Activity
{
    private enum DistributionPhase
    {
        GoingToGranary,
        ReadingOrders,
        CheckingGranaryStock,
        LoadingDeliveryItems,
        GoingToHousehold,
        CheckingHousehold,
        ExchangingItems,
        TakingBreak,
        ReturningToGranary,
        DepositingCollected,
        Done
    }

    private const uint READINGDURATION = 30;
    private const float BREAKPROBABILITY = 0.15f;
    private const uint MINBREAKDURATION = 20;
    private const uint MAXBREAKDURATION = 50;

    // Excess thresholds - collect items above these amounts
    // Keep low to prevent stale bread and wheat piling up
    private const int BREADEXCESSTHRESHOLD = 2;  // Collect bread if household has more than this
    private const int WHEATEXCESSTHRESHOLD = 5;  // Collect wheat if household has more than this

    private readonly Building _granary;
    private GoToBuildingActivity? _goToGranaryPhase;
    private GoToBuildingActivity? _goToHouseholdPhase;

    private DistributionPhase _currentPhase = DistributionPhase.GoingToGranary;
    private uint _phaseTimer;

    // All households to visit (from standing orders)
    private readonly List<Building> _householdsToVisit = new ();
    private int _currentHouseholdIndex;

    // What we're carrying to deliver (loaded from granary)
    private readonly Dictionary<string, int> _itemsToDeliver = new ();

    // What we've collected from households (to deposit at granary)
    // Tracked as item ID -> quantity since we use actions that work with item IDs
    private readonly Dictionary<string, int> _collectedItems = new ();

    // Standing orders reference for checking desired quantities
    private StandingOrders? _standingOrders;

    // Loading phase state - track which items still need to be loaded
    private List<(string itemId, int quantity)>? _itemsToLoad;
    private int _currentLoadIndex;
    private TransferBetweenStoragesAction? _loadAction;
    private int _lastLoadedQuantity;

    // Exchanging phase state - sub-phases for delivery and collection
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

    // Depositing phase state - track which collected items still need depositing
    private List<(string itemId, int quantity)>? _itemsToDeposit;
    private int _currentDepositIndex;
    private DepositToStorageAction? _depositAction;
    private int _lastDepositedQuantity;

    public override string DisplayName => _currentPhase switch
    {
        DistributionPhase.GoingToGranary => "Going to granary",
        DistributionPhase.ReadingOrders => "Reading delivery orders",
        DistributionPhase.CheckingGranaryStock => "Checking granary stock",
        DistributionPhase.LoadingDeliveryItems => "Loading items for delivery",
        DistributionPhase.GoingToHousehold => "Visiting household",
        DistributionPhase.CheckingHousehold => "Checking household stock",
        DistributionPhase.ExchangingItems => "Exchanging items",
        DistributionPhase.TakingBreak => "Taking a break",
        DistributionPhase.ReturningToGranary => "Returning to granary",
        DistributionPhase.DepositingCollected => "Depositing collected items",
        _ => "Distributing"
    };

    public DistributorRoundActivity(Building granary, int priority = 0)
    {
        _granary = granary;
        Priority = priority;

        // Distributors get hungry faster (lots of walking)
        NeedDecayMultipliers["hunger"] = 1.3f;
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

        return _currentPhase switch
        {
            DistributionPhase.GoingToGranary => ProcessGoingToGranary(position, perception),
            DistributionPhase.ReadingOrders => ProcessReadingOrders(),
            DistributionPhase.CheckingGranaryStock => ProcessCheckingGranaryStock(),
            DistributionPhase.LoadingDeliveryItems => ProcessLoadingDeliveryItems(),
            DistributionPhase.GoingToHousehold => ProcessGoingToHousehold(position, perception),
            DistributionPhase.CheckingHousehold => ProcessCheckingHousehold(),
            DistributionPhase.ExchangingItems => ProcessExchangingItems(),
            DistributionPhase.TakingBreak => ProcessTakingBreak(),
            DistributionPhase.ReturningToGranary => ProcessReturningToGranary(position, perception),
            DistributionPhase.DepositingCollected => ProcessDepositingCollected(),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToGranary(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_goToGranaryPhase == null)
        {
            _goToGranaryPhase = new GoToBuildingActivity(_granary, Priority, targetStorage: true);
            _goToGranaryPhase.Initialize(_owner);
        }

        var (result, action) = RunSubActivity(_goToGranaryPhase, position, perception);
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

        _currentPhase = DistributionPhase.ReadingOrders;
        _phaseTimer = ActivityTiming.GetVariedDuration(READINGDURATION, 0.2f);
        DebugLog("DISTRIBUTOR", "Arrived at granary, reading orders", 0);
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

        _currentPhase = DistributionPhase.CheckingGranaryStock;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessCheckingGranaryStock()
    {
        if (_owner == null)
        {
            return null;
        }

        // Observe granary storage
        var storage = _owner.AccessStorage(_granary);
        if (storage == null)
        {
            _goToGranaryPhase = null;
            _currentPhase = DistributionPhase.GoingToGranary;
            return new IdleAction(_owner, this, Priority);
        }

        DebugLog("DISTRIBUTOR", $"Granary stock: {storage.GetContentsSummary()}", 0);
        _currentPhase = DistributionPhase.LoadingDeliveryItems;
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

            // Calculate total needed for all households
            if (_standingOrders != null)
            {
                var targets = _standingOrders.GetDeliveryTargets();

                // Group by item type to load efficiently
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

                // Convert to list for sequential processing
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
            // Action was executed - record results
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
            // Done loading - start visiting households
            _itemsToLoad = null;
            _currentHouseholdIndex = 0;

            if (_householdsToVisit.Count > 0)
            {
                _goToHouseholdPhase = null;
                _currentPhase = DistributionPhase.GoingToHousehold;
            }
            else
            {
                DebugLog("DISTRIBUTOR", "No households to visit", 0);
                _currentPhase = DistributionPhase.ReturningToGranary;
            }

            return new IdleAction(_owner, this, Priority);
        }

        // Load next item type from granary
        var (nextItemId, neededQuantity) = _itemsToLoad[_currentLoadIndex];

        // Check how much is available at granary
        int available = _owner.GetStorageItemCount(_granary, nextItemId);
        if (available == 0)
        {
            // Skip this item type, nothing to load
            _currentLoadIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int amountToLoad = System.Math.Min(neededQuantity, available);

        // Create transfer action from granary to inventory
        _loadAction = TransferBetweenStoragesAction.FromBuilding(
            _owner,
            this,
            _granary,
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

    private EntityAction? ProcessGoingToHousehold(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_currentHouseholdIndex >= _householdsToVisit.Count)
        {
            _currentPhase = DistributionPhase.ReturningToGranary;
            return new IdleAction(_owner, this, Priority);
        }

        var household = _householdsToVisit[_currentHouseholdIndex];

        if (!GodotObject.IsInstanceValid(household))
        {
            _currentHouseholdIndex++;
            _goToHouseholdPhase = null;
            return new IdleAction(_owner, this, Priority);
        }

        if (_goToHouseholdPhase == null)
        {
            _goToHouseholdPhase = new GoToBuildingActivity(household, Priority, targetStorage: true);
            _goToHouseholdPhase.Initialize(_owner);
        }

        var (result, action) = RunSubActivity(_goToHouseholdPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                _currentHouseholdIndex++;
                _goToHouseholdPhase = null;
                return new IdleAction(_owner, this, Priority);
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        _currentPhase = DistributionPhase.CheckingHousehold;
        DebugLog("DISTRIBUTOR", $"Arrived at {household.BuildingName}", 0);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessCheckingHousehold()
    {
        if (_owner == null)
        {
            return null;
        }

        if (_currentHouseholdIndex >= _householdsToVisit.Count)
        {
            _currentPhase = DistributionPhase.ReturningToGranary;
            return new IdleAction(_owner, this, Priority);
        }

        var household = _householdsToVisit[_currentHouseholdIndex];

        // Observe household storage
        var storage = _owner.AccessStorage(household);
        if (storage == null)
        {
            // Not adjacent, skip this household
            _currentHouseholdIndex++;
            _goToHouseholdPhase = null;
            _currentPhase = DistributionPhase.GoingToHousehold;
            return new IdleAction(_owner, this, Priority);
        }

        DebugLog("DISTRIBUTOR", $"{household.BuildingName} has: {storage.GetContentsSummary()}", 0);

        _currentPhase = DistributionPhase.ExchangingItems;
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
            _currentPhase = DistributionPhase.ReturningToGranary;
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

            // Get delivery targets for this household
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
        _deliverAction = new DepositToStorageAction(
            _owner,
            this,
            household,
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

        // Create take action to collect excess bread
        _collectAction = new TakeFromStorageAction(
            _owner,
            this,
            household,
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

        // Create take action to collect excess wheat
        _collectAction = new TakeFromStorageAction(
            _owner,
            this,
            household,
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

        // Clear exchange state for next household
        _currentHouseholdTargets = null;

        // Move to next household
        MoveToNextHousehold();
        return new IdleAction(_owner, this, Priority);
    }

    private void MoveToNextHousehold()
    {
        _currentHouseholdIndex++;
        _goToHouseholdPhase = null;

        if (_currentHouseholdIndex < _householdsToVisit.Count)
        {
            if (ActivityTiming.ShouldTakeBreak(BREAKPROBABILITY))
            {
                _phaseTimer = ActivityTiming.GetBreakDuration(MINBREAKDURATION, MAXBREAKDURATION);
                _currentPhase = DistributionPhase.TakingBreak;
                DebugLog("DISTRIBUTOR", $"Taking a short break ({_phaseTimer} ticks)", 0);
            }
            else
            {
                _currentPhase = DistributionPhase.GoingToHousehold;
            }
        }
        else
        {
            _currentPhase = DistributionPhase.ReturningToGranary;
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
        _currentPhase = DistributionPhase.GoingToHousehold;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessReturningToGranary(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        _goToGranaryPhase = new GoToBuildingActivity(_granary, Priority, targetStorage: true);
        _goToGranaryPhase.Initialize(_owner);

        var (result, action) = RunSubActivity(_goToGranaryPhase, position, perception);
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
            _currentPhase = DistributionPhase.DepositingCollected;
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

        // First call: prepare list of items to deposit
        if (_itemsToDeposit == null)
        {
            _itemsToDeposit = new List<(string itemId, int quantity)>();

            // Convert collected items dictionary to list for sequential processing
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

        // Check if all items deposited
        if (_currentDepositIndex >= _itemsToDeposit.Count)
        {
            _itemsToDeposit = null;
            _collectedItems.Clear();
            DebugLog("DISTRIBUTOR", "Round complete!", 0);
            Complete();
            return null;
        }

        // Deposit next item type
        var (nextItemId, _) = _itemsToDeposit[_currentDepositIndex];

        // Check how much we actually have in inventory
        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            _currentDepositIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        int haveInInventory = inventory.GetItemCount(nextItemId);
        if (haveInInventory == 0)
        {
            // Don't have this item anymore (shouldn't happen but handle gracefully)
            _currentDepositIndex++;
            return new IdleAction(_owner, this, Priority);
        }

        // Create deposit action to granary
        _depositAction = new DepositToStorageAction(
            _owner,
            this,
            _granary,
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
