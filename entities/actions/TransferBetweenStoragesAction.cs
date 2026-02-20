using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to transfer items from one storage to another.
/// Source or destination must be the entity's inventory.
/// For facility-to-facility transfers, entity must be adjacent to both.
/// Uses safe transfer - items never disappear.
/// </summary>
public class TransferBetweenStoragesAction : EntityAction
{
    private readonly IStorageContainer? _source;
    private readonly IStorageContainer? _destination;
    private readonly Facility? _sourceFacility;
    private readonly Facility? _destFacility;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private int _actualTransferred;

    /// <summary>
    /// Gets actual quantity transferred (may be less than requested).
    /// </summary>
    public int ActualTransferred => _actualTransferred;

    /// <summary>
    /// Create a transfer from facility storage to entity inventory.
    /// </summary>
    public static TransferBetweenStoragesAction FromFacility(
        Being entity,
        object source,
        Facility facility,
        string itemDefId,
        int quantity,
        int priority = 0)
    {
        var inventory = entity.SelfAsEntity().GetTrait<InventoryTrait>();
        var storage = facility.SelfAsEntity().GetTrait<StorageTrait>();
        return new TransferBetweenStoragesAction(
            entity,
            source,
            storage,
            inventory,
            facility,
            null,
            itemDefId,
            quantity,
            priority);
    }

    /// <summary>
    /// Create a transfer from entity inventory to facility storage.
    /// </summary>
    public static TransferBetweenStoragesAction ToFacility(
        Being entity,
        object source,
        Facility facility,
        string itemDefId,
        int quantity,
        int priority = 0)
    {
        var inventory = entity.SelfAsEntity().GetTrait<InventoryTrait>();
        var storage = facility.SelfAsEntity().GetTrait<StorageTrait>();
        return new TransferBetweenStoragesAction(
            entity,
            source,
            inventory,
            storage,
            null,
            facility,
            itemDefId,
            quantity,
            priority);
    }

    private TransferBetweenStoragesAction(
        Being entity,
        object source,
        IStorageContainer? sourceContainer,
        IStorageContainer? destContainer,
        Facility? sourceFacility,
        Facility? destFacility,
        string itemDefId,
        int quantity,
        int priority)
        : base(entity, source, priority: priority)
    {
        _source = sourceContainer;
        _destination = destContainer;
        _sourceFacility = sourceFacility;
        _destFacility = destFacility;
        _itemDefId = itemDefId;
        _quantity = quantity;
    }

    public override bool Execute()
    {
        // Require valid source and destination containers
        if (_source == null || _destination == null)
        {
            return false;
        }

        // Verify adjacency for any facilities involved
        if (_sourceFacility != null && !Entity.CanAccessFacility(_sourceFacility))
        {
            return false;
        }

        if (_destFacility != null && !Entity.CanAccessFacility(_destFacility))
        {
            return false;
        }

        // Use safe transfer
        _actualTransferred = _source.TransferTo(_destination, _itemDefId, _quantity);

        // Update memory for any facilities involved
        if (_sourceFacility != null)
        {
            var storage = _sourceFacility.SelfAsEntity().GetTrait<StorageTrait>();
            if (storage != null)
            {
                Entity.Memory?.ObserveStorage(_sourceFacility, storage);
            }
        }

        if (_destFacility != null)
        {
            var storage = _destFacility.SelfAsEntity().GetTrait<StorageTrait>();
            if (storage != null)
            {
                Entity.Memory?.ObserveStorage(_destFacility, storage);
            }
        }

        return _actualTransferred > 0;
    }
}
