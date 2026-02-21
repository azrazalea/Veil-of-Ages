using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to take items from a facility's storage by tag for consumption.
/// If the entity has inventory, the item is transferred there (making it portable
/// during interruption). Otherwise, the item is consumed directly from storage.
/// Requires entity to be adjacent to the facility.
/// </summary>
public class ConsumeFromStorageByTagAction : EntityAction
{
    private readonly Facility _facility;
    private readonly string _itemTag;
    private readonly int _quantity;
    private Item? _consumedItem;
    private bool _addedToInventory;

    /// <summary>
    /// Gets the item that was actually consumed (available after Execute succeeds).
    /// </summary>
    public Item? ConsumedItem => _consumedItem;

    /// <summary>
    /// Gets a value indicating whether gets whether the item was added to the entity's inventory (true) or consumed directly (false).
    /// </summary>
    public bool AddedToInventory => _addedToInventory;

    /// <summary>
    /// Gets actual quantity consumed (may be less than requested if not enough available).
    /// </summary>
    public int ActualQuantity => _consumedItem?.Quantity ?? 0;

    public ConsumeFromStorageByTagAction(
        Being entity,
        object source,
        Facility facility,
        string itemTag,
        int quantity,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _facility = facility;
        _itemTag = itemTag;
        _quantity = quantity;
    }

    public override bool Execute()
    {
        // Use TakeFromFacilityStorageByTag which handles:
        // - Adjacency check (returns null if not adjacent)
        // - Memory observation (updates entity's memory)
        // - Finding item by tag and removing it
        _consumedItem = Entity.TakeFromFacilityStorageByTag(_facility, _itemTag, _quantity);

        if (_consumedItem == null)
        {
            return false;
        }

        // Try to add to inventory so food is portable during interruption.
        // If entity has no inventory (e.g., zombies), consume directly from storage.
        var inventory = Entity.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            if (inventory.AddItem(_consumedItem))
            {
                _addedToInventory = true;
            }
            else
            {
                // Inventory full - put back in storage
                Entity.PutInFacilityStorage(_facility, _consumedItem);
                _consumedItem = null;
                return false;
            }
        }

        return true;
    }
}
