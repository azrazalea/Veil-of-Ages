using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that takes specified items from a building's storage into the entity's inventory.
/// Each call to GetNextAction returns a TakeFromStorageAction for the current item.
/// </summary>
public class TakeFromStorageActivity : Activity
{
    private readonly Building _sourceBuilding;
    private readonly List<(string itemId, int quantity)> _itemsToTake;
    private int _currentIndex;

    /// <inheritdoc/>
    public override string DisplayName => $"Taking items from {_sourceBuilding.BuildingName}";

    /// <summary>
    /// Initializes a new instance of the <see cref="TakeFromStorageActivity"/> class.
    /// </summary>
    /// <param name="sourceBuilding">The building to take items from.</param>
    /// <param name="itemsToTake">List of item IDs and quantities to take.</param>
    /// <param name="priority">Priority for actions returned by this activity.</param>
    public TakeFromStorageActivity(
        Building sourceBuilding,
        List<(string itemId, int quantity)> itemsToTake,
        int priority)
    {
        _sourceBuilding = sourceBuilding;
        _itemsToTake = itemsToTake;
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

        // Check if building is still valid
        if (!GodotObject.IsInstanceValid(_sourceBuilding))
        {
            DebugLog("TAKE_STORAGE", "Source building is no longer valid", 0);
            Fail();
            return null;
        }

        // Check if all items have been taken
        if (_currentIndex >= _itemsToTake.Count)
        {
            DebugLog("TAKE_STORAGE", "All items taken successfully", 0);
            Complete();
            return null;
        }

        var (itemId, quantity) = _itemsToTake[_currentIndex];
        _currentIndex++;

        DebugLog("TAKE_STORAGE", $"Taking {quantity} {itemId} from {_sourceBuilding.BuildingName}", 0);

        return new TakeFromStorageAction(
            _owner,
            this,
            _sourceBuilding,
            itemId,
            quantity,
            Priority);
    }
}
