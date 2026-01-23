using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to transfer items from one storage to another.
/// Source or destination must be the entity's inventory.
/// For building-to-building transfers, entity must be adjacent to both.
/// Uses safe transfer - items never disappear.
/// </summary>
public class TransferBetweenStoragesAction : EntityAction
{
    private readonly IStorageContainer _source;
    private readonly IStorageContainer _destination;
    private readonly Building? _sourceBuilding;
    private readonly Building? _destBuilding;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private int _actualTransferred;

    /// <summary>
    /// Gets actual quantity transferred (may be less than requested).
    /// </summary>
    public int ActualTransferred => _actualTransferred;

    /// <summary>
    /// Create a transfer from building storage to entity inventory.
    /// </summary>
    public static TransferBetweenStoragesAction FromBuilding(
        Being entity,
        object source,
        Building building,
        string itemDefId,
        int quantity,
        int priority = 0)
    {
        var inventory = entity.SelfAsEntity().GetTrait<InventoryTrait>();
        var storage = building.GetStorage();
        return new TransferBetweenStoragesAction(
            entity,
            source,
            storage!,
            inventory!,
            building,
            null,
            itemDefId,
            quantity,
            priority);
    }

    /// <summary>
    /// Create a transfer from entity inventory to building storage.
    /// </summary>
    public static TransferBetweenStoragesAction ToBuilding(
        Being entity,
        object source,
        Building building,
        string itemDefId,
        int quantity,
        int priority = 0)
    {
        var inventory = entity.SelfAsEntity().GetTrait<InventoryTrait>();
        var storage = building.GetStorage();
        return new TransferBetweenStoragesAction(
            entity,
            source,
            inventory!,
            storage!,
            null,
            building,
            itemDefId,
            quantity,
            priority);
    }

    private TransferBetweenStoragesAction(
        Being entity,
        object source,
        IStorageContainer sourceContainer,
        IStorageContainer destContainer,
        Building? sourceBuilding,
        Building? destBuilding,
        string itemDefId,
        int quantity,
        int priority)
        : base(entity, source, priority: priority)
    {
        _source = sourceContainer;
        _destination = destContainer;
        _sourceBuilding = sourceBuilding;
        _destBuilding = destBuilding;
        _itemDefId = itemDefId;
        _quantity = quantity;
    }

    public override bool Execute()
    {
        // Verify adjacency for any buildings involved
        if (_sourceBuilding != null && !Entity.CanAccessBuildingStorage(_sourceBuilding))
        {
            return false;
        }

        if (_destBuilding != null && !Entity.CanAccessBuildingStorage(_destBuilding))
        {
            return false;
        }

        // Use safe transfer
        _actualTransferred = _source.TransferTo(_destination, _itemDefId, _quantity);

        // Update memory for any buildings involved
        if (_sourceBuilding != null)
        {
            var storage = _sourceBuilding.GetStorage();
            if (storage != null)
            {
                Entity.Memory?.ObserveStorage(_sourceBuilding, storage);
            }
        }

        if (_destBuilding != null)
        {
            var storage = _destBuilding.GetStorage();
            if (storage != null)
            {
                Entity.Memory?.ObserveStorage(_destBuilding, storage);
            }
        }

        return _actualTransferred > 0;
    }
}
