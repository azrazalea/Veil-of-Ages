using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Storage trait for buildings and other non-being entities.
/// Provides item storage capabilities with configurable capacity and decay modifiers.
/// </summary>
public class StorageTrait : Trait, IStorageContainer
{
    private readonly List<Item> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageTrait"/> class.
    /// Initializes a new instance of the StorageTrait with default values.
    /// </summary>
    public StorageTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageTrait"/> class.
    /// Initializes a new instance of the StorageTrait with specified capacity and facilities.
    /// </summary>
    /// <param name="volumeCapacity">Maximum volume in cubic meters.</param>
    /// <param name="weightCapacity">Maximum weight in kg, -1 for unlimited.</param>
    /// <param name="decayRateModifier">Decay rate modifier (0.5 = half decay, 2.0 = double).</param>
    /// <param name="facilities">List of available facilities.</param>
    public StorageTrait(float volumeCapacity, float weightCapacity = -1, float decayRateModifier = 1.0f, List<string>? facilities = null)
    {
        VolumeCapacity = volumeCapacity;
        WeightCapacity = weightCapacity;
        DecayRateModifier = decayRateModifier;
        if (facilities != null)
        {
            Facilities = facilities;
        }
    }

    /// <summary>
    /// Gets or sets the maximum volume capacity in cubic meters.
    /// </summary>
    public float VolumeCapacity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the maximum weight capacity in kilograms. -1 means unlimited.
    /// </summary>
    public float WeightCapacity { get; set; } = -1;

    /// <summary>
    /// Gets or sets the decay rate modifier for items in this storage.
    /// 0.5 = half decay rate (cold storage), 1.0 = normal, 2.0 = double (hot/humid).
    /// </summary>
    public float DecayRateModifier { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the facilities available at this storage location.
    /// Used for crafting requirements (e.g., "oven", "workbench", "forge").
    /// </summary>
    public List<string> Facilities { get; set; } = [];

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
    /// Returns float.MaxValue if weight capacity is unlimited.
    /// </summary>
    public float RemainingWeight => WeightCapacity < 0 ? float.MaxValue : WeightCapacity - UsedWeight;

    /// <inheritdoc/>
    public bool CanAdd(Item item)
    {
        if (item == null)
        {
            return false;
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
            Log.Print($"StorageTrait: Cannot add item {item.Definition.Id} - insufficient capacity");
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
    public void ProcessDecay()
    {
        var spoiledItems = new List<Item>();

        foreach (var item in _items)
        {
            item.ApplyDecay(DecayRateModifier);

            if (item.IsSpoiled)
            {
                spoiledItems.Add(item);
            }
        }

        // Remove spoiled items
        foreach (var item in spoiledItems)
        {
            Log.Print($"StorageTrait: Item {item.Definition.Id} has spoiled and been removed");
            _items.Remove(item);
        }
    }

    /// <summary>
    /// Check if this storage has a specific facility.
    /// </summary>
    /// <param name="facility">The facility name to check for.</param>
    /// <returns>True if the facility is available.</returns>
    public bool HasFacility(string facility)
    {
        return Facilities.Contains(facility, System.StringComparer.OrdinalIgnoreCase);
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

            sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1}", item.Quantity, item.Definition.Name));
        }

        return sb.Length > 0 ? sb.ToString() : "empty";
    }
}
