using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Inventory trait for beings (living and undead entities).
/// Provides personal item storage with capacity limits appropriate for carried items.
/// </summary>
public class InventoryTrait : BeingTrait, IStorageContainer
{
    private readonly List<Item> _items = [];

    /// <summary>
    /// Gets or sets the maximum volume capacity in cubic meters.
    /// Default: 0.02 m3 (20 liters, roughly a small backpack).
    /// </summary>
    public float VolumeCapacity { get; set; } = 0.02f;

    /// <summary>
    /// Gets or sets the maximum weight capacity in kilograms.
    /// Default: 15 kg (reasonable carry weight for most beings).
    /// </summary>
    public float WeightCapacity { get; set; } = 15.0f;

    /// <summary>
    /// Gets or sets the decay rate modifier for items in this inventory.
    /// Default: 1.0 (normal decay rate).
    /// </summary>
    public float DecayRateModifier { get; set; } = 1.0f;

    /// <summary>
    /// Gets the currently used volume in cubic meters.
    /// </summary>
    public float UsedVolume => _items.Sum(item => item.TotalVolume);

    /// <summary>
    /// Gets the currently used weight in kilograms.
    /// </summary>
    public float UsedWeight => _items.Sum(item => item.TotalWeight);

    /// <summary>
    /// Gets the remaining volume capacity in cubic meters.
    /// </summary>
    public float RemainingVolume => VolumeCapacity - UsedVolume;

    /// <summary>
    /// Gets the remaining weight capacity in kilograms.
    /// Returns float.MaxValue if weight capacity is unlimited (-1).
    /// </summary>
    public float RemainingWeight => WeightCapacity < 0 ? float.MaxValue : WeightCapacity - UsedWeight;

    /// <summary>
    /// Gets a value indicating whether the inventory is carrying an item that exceeds
    /// normal capacity limits (Rimworld-style over-capacity carry).
    /// </summary>
    public bool IsOverCapacity => UsedVolume > VolumeCapacity || (WeightCapacity >= 0 && UsedWeight > WeightCapacity);

    /// <inheritdoc/>
    public bool CanAdd(Item item)
    {
        if (item == null)
        {
            return false;
        }

        // Over-capacity carry: if inventory is empty and it's a single item,
        // allow it regardless of weight/volume (carrying one heavy thing)
        if (_items.Count == 0 && item.Quantity == 1)
        {
            return true;
        }

        // Check volume
        if (item.TotalVolume > RemainingVolume)
        {
            return false;
        }

        // Check weight (if limited)
        if (WeightCapacity >= 0 && item.TotalWeight > RemainingWeight)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool AddItem(Item item)
    {
        if (item == null)
        {
            return false;
        }

        // Try to merge with existing stacks first
        foreach (var existingItem in _items.Where(i => i.Definition.Id == item.Definition.Id))
        {
            var leftover = existingItem.TryMerge(item);
            if (leftover == null)
            {
                // Fully merged
                return true;
            }

            item = leftover;
        }

        // Check if we can add the remaining item
        if (!CanAdd(item))
        {
            Log.Print($"InventoryTrait: Cannot add item {item.Definition.Id} - insufficient capacity");
            return false;
        }

        _items.Add(item);
        return true;
    }

    /// <inheritdoc/>
    public Item? RemoveItem(string itemDefId, int quantity)
    {
        if (string.IsNullOrEmpty(itemDefId) || quantity < 1)
        {
            return null;
        }

        // Check if we have enough
        int totalAvailable = GetItemCount(itemDefId);
        if (totalAvailable < quantity)
        {
            return null;
        }

        int remaining = quantity;
        Item? result = null;

        // Collect from stacks, removing empty ones
        var itemsToRemove = new List<Item>();

        foreach (var item in _items.Where(i => i.Definition.Id == itemDefId))
        {
            if (remaining <= 0)
            {
                break;
            }

            if (item.Quantity <= remaining)
            {
                // Take the whole stack
                remaining -= item.Quantity;
                itemsToRemove.Add(item);

                if (result == null)
                {
                    result = item;
                }
                else
                {
                    result.TryMerge(item);
                }
            }
            else
            {
                // Split the stack
                var split = item.Split(remaining);
                if (split != null)
                {
                    remaining = 0;
                    if (result == null)
                    {
                        result = split;
                    }
                    else
                    {
                        result.TryMerge(split);
                    }
                }
            }
        }

        // Remove empty stacks
        foreach (var item in itemsToRemove)
        {
            _items.Remove(item);
        }

        return result;
    }

    /// <inheritdoc/>
    public bool HasItem(string itemDefId, int quantity = 1)
    {
        return GetItemCount(itemDefId) >= quantity;
    }

    /// <inheritdoc/>
    public int GetItemCount(string itemDefId)
    {
        if (string.IsNullOrEmpty(itemDefId))
        {
            return 0;
        }

        return _items
            .Where(i => i.Definition.Id == itemDefId)
            .Sum(i => i.Quantity);
    }

    /// <inheritdoc/>
    public Item? FindItem(string itemDefId)
    {
        if (string.IsNullOrEmpty(itemDefId))
        {
            return null;
        }

        return _items.FirstOrDefault(i => i.Definition.Id == itemDefId);
    }

    /// <inheritdoc/>
    public Item? FindItemByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return null;
        }

        return _items.FirstOrDefault(i => i.Definition.HasTag(tag));
    }

    /// <inheritdoc/>
    public IEnumerable<Item> GetAllItems()
    {
        return _items.AsReadOnly();
    }

    /// <inheritdoc/>
    public void ProcessDecay(int tickMultiplier = 1)
    {
        var spoiledItems = new List<Item>();

        foreach (var item in _items)
        {
            item.ApplyDecay(DecayRateModifier * tickMultiplier);

            if (item.IsSpoiled)
            {
                spoiledItems.Add(item);
            }
        }

        // Remove spoiled items
        foreach (var item in spoiledItems)
        {
            Log.Print($"InventoryTrait: Item {item.Definition.Id} has spoiled and been removed");
            _items.Remove(item);
        }
    }

    /// <summary>
    /// Get the total count of all items in this inventory.
    /// </summary>
    /// <returns>The sum of quantities of all items.</returns>
    public int GetTotalItemCount()
    {
        return _items.Sum(i => i.Quantity);
    }

    /// <summary>
    /// Check if this inventory is empty.
    /// </summary>
    /// <returns>True if no items are stored.</returns>
    public bool IsEmpty()
    {
        return _items.Count == 0;
    }

    /// <summary>
    /// Get the current encumbrance level as a percentage (0-1).
    /// Based on the more restrictive of volume or weight.
    /// </summary>
    /// <returns>Encumbrance percentage where 1.0 = fully loaded.</returns>
    public float GetEncumbranceLevel()
    {
        float volumeRatio = VolumeCapacity > 0 ? UsedVolume / VolumeCapacity : 0;
        float weightRatio = WeightCapacity > 0 ? UsedWeight / WeightCapacity : 0;

        return System.Math.Max(volumeRatio, weightRatio);
    }

    /// <inheritdoc/>
    public string GetContentsSummary()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var item in _items)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1}", item.Quantity, item.Definition.LocalizedName));
        }

        return sb.Length > 0 ? sb.ToString() : "empty";
    }
}
