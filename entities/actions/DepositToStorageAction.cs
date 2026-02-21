using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to deposit items from entity's inventory to a facility's storage.
/// Requires entity to be adjacent to the facility.
/// Uses safe transfer that won't lose items.
/// </summary>
public class DepositToStorageAction : EntityAction
{
    private readonly Facility _facility;
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
        Facility facility,
        string itemDefId,
        int quantity,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _facility = facility;
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
        if (!Entity.CanAccessFacility(_facility))
        {
            return false;
        }

        var storage = _facility.SelfAsEntity().GetTrait<StorageTrait>();
        if (storage == null)
        {
            return false;
        }

        // Use safe transfer - items won't disappear
        // Cast to IStorageContainer to access default interface method
        _actualDeposited = ((IStorageContainer)inventory).TransferTo(storage, _itemDefId, _quantity);

        // Update entity's memory about storage contents
        Entity.Memory?.ObserveStorage(_facility, storage);

        return _actualDeposited > 0;
    }
}
