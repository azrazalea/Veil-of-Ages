using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to produce (create and add) items to a building's storage from in-place processing.
/// Unlike DepositToStorageAction, items are created from an item definition rather than
/// being transferred from inventory. Used for crafting reactions.
/// Requires entity to be adjacent to the building.
/// </summary>
public class ProduceToStorageAction : EntityAction
{
    private readonly Building _building;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private int _actualProduced;

    /// <summary>
    /// Gets actual quantity produced and added to storage.
    /// May be less than requested if storage is full or item definition not found.
    /// </summary>
    public int ActualProduced => _actualProduced;

    public ProduceToStorageAction(
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

        // Get the item definition
        var itemDef = ItemResourceManager.Instance.GetDefinition(_itemDefId);
        if (itemDef == null)
        {
            Log.Error($"ProduceToStorageAction: Item definition '{_itemDefId}' not found");
            return false;
        }

        // Create the item
        var item = new Item(itemDef, _quantity);

        // Try to add to storage
        if (!storage.AddItem(item))
        {
            Log.Warn($"ProduceToStorageAction: Storage full, {_quantity}x {_itemDefId} lost!");

            // Still return true because the production "happened" even if storage couldn't hold it
            // This matches the original behavior where items were lost if storage was full
            _actualProduced = 0;
        }
        else
        {
            _actualProduced = _quantity;
        }

        // Update entity's memory about storage contents
        Entity.Memory?.ObserveStorage(_building, storage);

        return true;
    }
}
