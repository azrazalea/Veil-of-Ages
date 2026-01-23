using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to consume (remove) items from a building's storage for in-place processing.
/// Unlike TakeFromStorageAction, the items are consumed directly at the workstation
/// rather than being transferred to inventory. Used for crafting reactions.
/// Requires entity to be adjacent to the building.
/// </summary>
public class ConsumeFromStorageAction : EntityAction
{
    private readonly Building _building;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private Item? _consumedItem;

    /// <summary>
    /// Gets the item that was actually consumed (available after Execute succeeds).
    /// </summary>
    public Item? ConsumedItem => _consumedItem;

    /// <summary>
    /// Gets actual quantity consumed (may be less than requested if not enough available).
    /// </summary>
    public int ActualQuantity => _consumedItem?.Quantity ?? 0;

    public ConsumeFromStorageAction(
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

        // Verify items are available before consuming
        if (!storage.HasItem(_itemDefId, _quantity))
        {
            return false;
        }

        // Remove items from storage (consumed for crafting)
        _consumedItem = storage.RemoveItem(_itemDefId, _quantity);

        if (_consumedItem == null)
        {
            return false;
        }

        // Update entity's memory about storage contents
        Entity.Memory?.ObserveStorage(_building, storage);

        return true;
    }
}
