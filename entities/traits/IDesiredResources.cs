using System.Collections.Generic;
using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Interface for traits that specify desired resource levels at home.
/// Entities with this trait will try to maintain a stockpile of resources at their home storage.
/// </summary>
public interface IDesiredResources
{
    /// <summary>
    /// Gets the desired resource levels for home storage.
    /// Key: item definition ID (e.g., "flour", "bread", "wheat")
    /// Value: desired quantity to maintain in stock.
    /// </summary>
    IReadOnlyDictionary<string, int> DesiredResources { get; }

    /// <summary>
    /// Check which resources are below desired levels in the given storage.
    /// </summary>
    /// <param name="storage">The storage container to check (typically home storage).</param>
    /// <returns>
    /// Dictionary of item IDs to needed quantities.
    /// Only includes items that are below desired levels.
    /// Returns empty dictionary if all desires are met or storage is null.
    /// </returns>
    Dictionary<string, int> GetMissingResources(IStorageContainer? storage)
    {
        var missing = new Dictionary<string, int>();

        if (storage == null)
        {
            // If no storage, all desired resources are missing
            foreach (var (itemId, desiredQty) in DesiredResources)
            {
                missing[itemId] = desiredQty;
            }

            return missing;
        }

        foreach (var (itemId, desiredQty) in DesiredResources)
        {
            int currentQty = storage.GetItemCount(itemId);
            if (currentQty < desiredQty)
            {
                missing[itemId] = desiredQty - currentQty;
            }
        }

        return missing;
    }

    /// <summary>
    /// Check if the storage meets all desired resource levels.
    /// </summary>
    /// <param name="storage">The storage container to check.</param>
    /// <returns>True if all desired resources are at or above their target quantities.</returns>
    bool AreDesiresMet(IStorageContainer? storage)
    {
        if (storage == null)
        {
            return DesiredResources.Count == 0;
        }

        foreach (var (itemId, desiredQty) in DesiredResources)
        {
            if (storage.GetItemCount(itemId) < desiredQty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the deficit for a specific item.
    /// </summary>
    /// <param name="storage">The storage container to check.</param>
    /// <param name="itemId">The item definition ID to check.</param>
    /// <returns>
    /// The number of items needed to meet the desired level.
    /// Returns 0 if the item is at or above the desired level, or if it's not in the desired list.
    /// </returns>
    int GetDeficit(IStorageContainer? storage, string itemId)
    {
        if (!DesiredResources.TryGetValue(itemId, out int desiredQty))
        {
            return 0; // Not a desired resource
        }

        int currentQty = storage?.GetItemCount(itemId) ?? 0;
        return currentQty < desiredQty ? desiredQty - currentQty : 0;
    }
}
