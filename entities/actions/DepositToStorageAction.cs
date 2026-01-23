using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to deposit items from entity's inventory to a building's storage.
/// Requires entity to be adjacent to the building.
/// Uses safe transfer that won't lose items.
/// </summary>
public class DepositToStorageAction : EntityAction
{
    private readonly Building _building;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private int _actualDeposited;

    /// <summary>
    /// Gets actual quantity deposited (may be less than requested if storage full).
    /// </summary>
    public int ActualDeposited => _actualDeposited;

    public DepositToStorageAction(
        Being entity,
        object source,
        Building building,
        string itemDefId,
        int quantity,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _building = building;
        _itemDefId = itemDefId;
        _quantity = quantity;
    }

    public override bool Execute()
    {
        var inventory = Entity.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            return false;
        }

        // Check adjacency
        if (!Entity.CanAccessBuildingStorage(_building))
        {
            return false;
        }

        var storage = _building.GetStorage();
        if (storage == null)
        {
            return false;
        }

        // Use safe transfer - items won't disappear
        // Cast to IStorageContainer to access default interface method
        _actualDeposited = ((IStorageContainer)inventory).TransferTo(storage, _itemDefId, _quantity);

        // Update entity's memory about storage contents
        Entity.Memory?.ObserveStorage(_building, storage);

        return _actualDeposited > 0;
    }
}
