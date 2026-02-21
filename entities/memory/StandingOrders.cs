using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Entities;

namespace VeilOfAges.Entities.Memory;

/// <summary>
/// Represents a delivery target for the distributor.
/// </summary>
/// <param name="Household">The household room to deliver to.</param>
/// <param name="ItemId">The item definition ID to deliver.</param>
/// <param name="DesiredQuantity">How many items the household wants.</param>
/// <param name="Priority">Delivery priority (lower = more urgent).</param>
public record DeliveryTarget(
    Room Household,
    string ItemId,
    int DesiredQuantity,
    int Priority = 0);

/// <summary>
/// Standing orders for food distribution from a granary.
/// Thread-safe for reading during entity Think() cycles.
/// </summary>
public class StandingOrders
{
    private readonly List<DeliveryTarget> _targets = new ();
    private readonly object _lock = new ();

    /// <summary>
    /// Get a snapshot of all delivery targets (thread-safe).
    /// Returns only valid rooms, sorted by priority.
    /// </summary>
    public List<DeliveryTarget> GetDeliveryTargets()
    {
        lock (_lock)
        {
            return _targets
                .Where(t => !t.Household.IsDestroyed)
                .OrderBy(t => t.Priority)
                .ToList();
        }
    }

    /// <summary>
    /// Set or update a delivery target for a household.
    /// </summary>
    public void SetDeliveryTarget(Room household, string itemId, int desiredQuantity, int priority = 0)
    {
        lock (_lock)
        {
            // Remove existing target for this household+item
            _targets.RemoveAll(t => t.Household == household && t.ItemId == itemId);

            if (desiredQuantity > 0)
            {
                _targets.Add(new DeliveryTarget(household, itemId, desiredQuantity, priority));
            }
        }
    }

    /// <summary>
    /// Remove all targets for a household.
    /// </summary>
    public void RemoveTargetsForHousehold(Room household)
    {
        lock (_lock)
        {
            _targets.RemoveAll(t => t.Household == household);
        }
    }

    /// <summary>
    /// Remove targets pointing to destroyed rooms.
    /// </summary>
    public void CleanupInvalidTargets()
    {
        lock (_lock)
        {
            _targets.RemoveAll(t => t.Household.IsDestroyed);
        }
    }

    /// <summary>
    /// Gets get count of delivery targets.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _targets.Count(t => !t.Household.IsDestroyed);
            }
        }
    }
}
