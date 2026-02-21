using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that deposits specified items from the entity's inventory to a facility's storage.
/// Each call to GetNextAction returns a DepositToStorageAction for the current item.
/// </summary>
public class DepositToStorageActivity : Activity
{
    private readonly Facility _storageFacility;
    private readonly List<(string itemId, int quantity)> _itemsToDeposit;
    private int _currentIndex;

    /// <inheritdoc/>
    public override string DisplayName => L.TrFmt("activity.DEPOSITING_TO_STORAGE", _storageFacility.ContainingRoom?.Name ?? _storageFacility.Id);

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositToStorageActivity"/> class.
    /// </summary>
    /// <param name="storageFacility">The facility to deposit items to.</param>
    /// <param name="itemsToDeposit">List of item IDs and quantities to deposit.</param>
    /// <param name="priority">Priority for actions returned by this activity.</param>
    public DepositToStorageActivity(
        Facility storageFacility,
        List<(string itemId, int quantity)> itemsToDeposit,
        int priority)
    {
        _storageFacility = storageFacility;
        _itemsToDeposit = itemsToDeposit;
        Priority = priority;
    }

    /// <inheritdoc/>
    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if facility is still valid
        if (!GodotObject.IsInstanceValid(_storageFacility))
        {
            DebugLog("DEPOSIT_STORAGE", "Storage facility is no longer valid", 0);
            Fail();
            return null;
        }

        // Check if all items have been deposited
        if (_currentIndex >= _itemsToDeposit.Count)
        {
            DebugLog("DEPOSIT_STORAGE", "All items deposited successfully", 0);
            Complete();
            return null;
        }

        var (itemId, quantity) = _itemsToDeposit[_currentIndex];
        _currentIndex++;

        DebugLog("DEPOSIT_STORAGE", $"Depositing {quantity} {itemId} to {_storageFacility.ContainingRoom?.Name ?? _storageFacility.Id}", 0);

        return new DepositToStorageAction(
            _owner,
            this,
            _storageFacility,
            itemId,
            quantity,
            Priority);
    }
}
