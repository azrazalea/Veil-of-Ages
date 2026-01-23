using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to consume (remove) items from a building's storage by tag for direct consumption.
/// Searches for items matching the specified tag and consumes them.
/// Unlike TakeFromStorageAction, the items are consumed directly rather than being
/// transferred to inventory. Used for eating food from home storage.
/// Requires entity to be adjacent to the building.
/// </summary>
public class ConsumeFromStorageByTagAction : EntityAction
{
    private readonly Building _building;
    private readonly string _itemTag;
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

    public ConsumeFromStorageByTagAction(
        Being entity,
        object source,
        Building building,
        string itemTag,
        int quantity,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _building = building;
        _itemTag = itemTag;
        _quantity = quantity;
    }

    public override bool Execute()
    {
        // Use TakeFromStorageByTag which handles:
        // - Adjacency check (returns null if not adjacent)
        // - Memory observation (updates entity's memory)
        // - Finding item by tag and removing it
        _consumedItem = Entity.TakeFromStorageByTag(_building, _itemTag, _quantity);

        return _consumedItem != null;
    }
}
