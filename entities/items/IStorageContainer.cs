using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VeilOfAges.Entities.Items;

/// <summary>
/// Interface for anything that can store items (buildings, inventories, containers).
/// </summary>
public interface IStorageContainer
{
    /// <summary>
    /// Gets the maximum volume capacity in cubic meters.
    /// </summary>
    float VolumeCapacity { get; }

    /// <summary>
    /// Gets the maximum weight capacity in kilograms. -1 means unlimited.
    /// </summary>
    float WeightCapacity { get; }

    /// <summary>
    /// Gets the currently used volume in cubic meters.
    /// </summary>
    float UsedVolume { get; }

    /// <summary>
    /// Gets the currently used weight in kilograms.
    /// </summary>
    float UsedWeight { get; }

    /// <summary>
    /// Gets the remaining volume capacity in cubic meters.
    /// </summary>
    float RemainingVolume => VolumeCapacity - UsedVolume;

    /// <summary>
    /// Gets the remaining weight capacity in kilograms.
    /// Returns float.MaxValue if weight capacity is unlimited.
    /// </summary>
    float RemainingWeight => WeightCapacity < 0 ? float.MaxValue : WeightCapacity - UsedWeight;

    /// <summary>
    /// Gets the decay rate modifier for items in this container.
    /// 0.5 = half decay rate, 1.0 = normal, 2.0 = double decay rate.
    /// </summary>
    float DecayRateModifier { get; }

    /// <summary>
    /// Check if an item can be added to this container.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True if the item can be added, false otherwise.</returns>
    bool CanAdd(Item item);

    /// <summary>
    /// Add an item to this container.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>True if the item was added, false otherwise.</returns>
    bool AddItem(Item item);

    /// <summary>
    /// Remove items from this container.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to remove.</param>
    /// <param name="quantity">The quantity to remove.</param>
    /// <returns>The removed item, or null if not enough items were available.</returns>
    Item? RemoveItem(string itemDefId, int quantity);

    /// <summary>
    /// Check if this container has a specific quantity of an item.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to check.</param>
    /// <param name="quantity">The required quantity (default 1).</param>
    /// <returns>True if the container has at least the specified quantity.</returns>
    bool HasItem(string itemDefId, int quantity = 1);

    /// <summary>
    /// Get the total count of an item type in this container.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to count.</param>
    /// <returns>The total quantity of the item in this container.</returns>
    int GetItemCount(string itemDefId);

    /// <summary>
    /// Find an item by its definition ID.
    /// </summary>
    /// <param name="itemDefId">The item definition ID to find.</param>
    /// <returns>The first matching item, or null if not found.</returns>
    Item? FindItem(string itemDefId);

    /// <summary>
    /// Find an item by a tag on its definition.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>The first item with a matching tag, or null if not found.</returns>
    Item? FindItemByTag(string tag);

    /// <summary>
    /// Get all items in this container.
    /// </summary>
    /// <returns>An enumerable of all items.</returns>
    IEnumerable<Item> GetAllItems();

    /// <summary>
    /// Process decay for all items in this container.
    /// Should be called each game tick.
    /// </summary>
    void ProcessDecay();

    /// <summary>
    /// Get a summary string of all items in this container for debug logging.
    /// Returns format like "3 wheat, 5 bread" or "empty" if no items.
    /// </summary>
    /// <returns>A human-readable summary of container contents.</returns>
    string GetContentsSummary()
    {
        var items = GetAllItems();
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(CultureInfo.InvariantCulture, $"{item.Quantity} {item.Definition.Name}");
        }

        return sb.Length > 0 ? sb.ToString() : "empty";
    }
}
