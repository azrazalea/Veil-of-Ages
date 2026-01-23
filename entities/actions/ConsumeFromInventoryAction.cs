using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to consume (remove) items from the entity's own inventory for direct consumption.
/// Unlike ConsumeFromStorageAction, this works with the entity's inventory rather than
/// building storage, and does not require adjacency to any building.
/// Used for eating food from inventory.
/// </summary>
public class ConsumeFromInventoryAction : EntityAction
{
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

    public ConsumeFromInventoryAction(
        Being entity,
        object source,
        string itemDefId,
        int quantity,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
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

        // Verify items are available before consuming
        if (!inventory.HasItem(_itemDefId, _quantity))
        {
            return false;
        }

        // Remove items from inventory (consumed for eating)
        _consumedItem = inventory.RemoveItem(_itemDefId, _quantity);

        return _consumedItem != null;
    }
}
