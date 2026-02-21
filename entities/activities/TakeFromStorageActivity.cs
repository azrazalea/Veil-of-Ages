using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that takes specified items from a facility's storage into the entity's inventory.
/// Supports both item-by-ID and tag-based modes.
///
/// Phases:
/// 1. Navigate — GoToFacilityActivity to reach storage access position (cross-area capable)
/// 2. Taking — TakeFromStorageAction per item (tag mode resolves tag to item ID first)
///
/// Interruption behavior: OnResume() nulls navigation, regresses to Navigate.
/// Taking progress (items already taken) is preserved.
///
/// Backward compatibility: Existing callers already navigate before using this activity.
/// The navigation phases complete immediately when already adjacent.
/// </summary>
public class TakeFromStorageActivity : Activity
{
    private enum Phase
    {
        Navigate,
        Taking
    }

    private readonly Facility _storageFacility;
    private readonly List<(string itemId, int quantity)>? _itemsToTake;
    private readonly string? _tag;
    private readonly int _tagQuantity;

    private Phase _currentPhase;
    private Activity? _navActivity;
    private int _currentIndex;
    private bool _tagResolved;

    /// <inheritdoc/>
    public override string DisplayName => L.TrFmt("activity.TAKING_FROM_STORAGE", _storageFacility.ContainingRoom?.Name ?? _storageFacility.Id);

    public override Room? TargetRoom => _storageFacility.ContainingRoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="TakeFromStorageActivity"/> class.
    /// Takes specific items by ID from a facility's storage.
    /// </summary>
    /// <param name="storageFacility">The facility to take items from.</param>
    /// <param name="itemsToTake">List of item IDs and quantities to take.</param>
    /// <param name="priority">Priority for actions returned by this activity.</param>
    public TakeFromStorageActivity(
        Facility storageFacility,
        List<(string itemId, int quantity)> itemsToTake,
        int priority)
    {
        _storageFacility = storageFacility;
        _itemsToTake = itemsToTake;
        Priority = priority;
        _currentPhase = Phase.Navigate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TakeFromStorageActivity"/> class.
    /// Takes items matching a tag from a facility's storage.
    /// After navigation, observes storage, resolves the tag to an item ID,
    /// then issues TakeFromStorageAction.
    /// </summary>
    /// <param name="storageFacility">The facility to take items from.</param>
    /// <param name="tag">Tag to match items against (e.g., "food").</param>
    /// <param name="quantity">Number of matching items to take.</param>
    /// <param name="priority">Priority for actions returned by this activity.</param>
    public TakeFromStorageActivity(
        Facility storageFacility,
        string tag,
        int quantity,
        int priority)
    {
        _storageFacility = storageFacility;
        _tag = tag;
        _tagQuantity = quantity;
        _itemsToTake = new List<(string itemId, int quantity)>();
        Priority = priority;
        _currentPhase = Phase.Navigate;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _navActivity = null;

        // Regress to navigation — preserve taking progress
        if (_currentPhase != Phase.Taking)
        {
            _currentPhase = Phase.Navigate;
        }
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
            DebugLog("TAKE_STORAGE", "Storage facility is no longer valid", 0);
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            Phase.Navigate => ProcessNavigate(position, perception),
            Phase.Taking => ProcessTaking(),
            _ => null
        };
    }

    private EntityAction? ProcessNavigate(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        if (_navActivity == null)
        {
            _navActivity = new GoToFacilityActivity(_storageFacility, Priority);
            _navActivity.Initialize(_owner);
            DebugLog("TAKE_STORAGE", $"Starting navigation to {_storageFacility.ContainingRoom?.Name ?? _storageFacility.Id}", 0);
        }

        var (result, action) = RunSubActivity(_navActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("TAKE_STORAGE", "Navigation failed", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        // We've arrived — transition to Taking
        _navActivity = null;
        _currentPhase = Phase.Taking;
        DebugLog("TAKE_STORAGE", $"Arrived at {_storageFacility.ContainingRoom?.Name ?? _storageFacility.Id}, starting to take items", 0);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTaking()
    {
        if (_owner == null)
        {
            return null;
        }

        // Tag mode: resolve tag to item ID on first call after arriving
        if (_tag != null && !_tagResolved)
        {
            _tagResolved = true;

            // Observe storage to update memory
            var storage = _owner.AccessFacilityStorage(_storageFacility);
            if (storage == null)
            {
                DebugLog("TAKE_STORAGE", "Cannot access storage (not adjacent?)", 0);
                Fail();
                return null;
            }

            var foundItem = storage.FindItemByTag(_tag);
            if (foundItem == null)
            {
                DebugLog("TAKE_STORAGE", $"No item with tag '{_tag}' found in storage", 0);
                Fail();
                return null;
            }

            // Resolve tag to specific item ID
            var resolvedId = foundItem.Definition.Id;
            if (string.IsNullOrEmpty(resolvedId))
            {
                DebugLog("TAKE_STORAGE", $"Found item with tag '{_tag}' but it has no ID", 0);
                Fail();
                return null;
            }

            _itemsToTake!.Add((resolvedId, _tagQuantity));
            DebugLog("TAKE_STORAGE", $"Resolved tag '{_tag}' to item '{resolvedId}'", 0);
        }

        // Check if all items have been taken
        if (_itemsToTake == null || _currentIndex >= _itemsToTake.Count)
        {
            DebugLog("TAKE_STORAGE", "All items taken successfully", 0);
            Complete();
            return null;
        }

        var (itemId, quantity) = _itemsToTake[_currentIndex];
        _currentIndex++;

        DebugLog("TAKE_STORAGE", $"Taking {quantity} {itemId} from {_storageFacility.ContainingRoom?.Name ?? _storageFacility.Id}", 0);

        return new TakeFromStorageAction(
            _owner,
            this,
            _storageFacility,
            itemId,
            quantity,
            Priority);
    }
}
