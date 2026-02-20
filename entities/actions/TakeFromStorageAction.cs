using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action to take items from a facility's storage into the entity's inventory.
/// Requires entity to be adjacent to the facility.
/// </summary>
public class TakeFromStorageAction : EntityAction
{
    private readonly Facility _facility;
    private readonly string _itemDefId;
    private readonly int _quantity;
    private Item? _takenItem;

    /// <summary>
    /// Gets the item that was actually taken (available after Execute succeeds).
    /// </summary>
    public Item? TakenItem => _takenItem;

    /// <summary>
    /// Gets actual quantity taken (may be less than requested).
    /// </summary>
    public int ActualQuantity => _takenItem?.Quantity ?? 0;

    public TakeFromStorageAction(
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
        // TakeFromFacilityStorage handles adjacency check and memory observation
        _takenItem = Entity.TakeFromFacilityStorage(_facility, _itemDefId, _quantity);

        if (_takenItem == null)
        {
            return false;
        }

        // Add to entity's inventory
        var inventory = Entity.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            // Put back if no inventory
            Entity.PutInFacilityStorage(_facility, _takenItem);
            _takenItem = null;
            return false;
        }

        if (!inventory.AddItem(_takenItem))
        {
            // Put back if inventory full
            Entity.PutInFacilityStorage(_facility, _takenItem);
            _takenItem = null;
            return false;
        }

        return true;
    }
}
