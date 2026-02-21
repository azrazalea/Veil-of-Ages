using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait for granaries that manages standing delivery orders.
/// Distributors read orders from this trait to know what to deliver where.
/// </summary>
public class GranaryTrait : Trait
{
    /// <summary>
    /// Gets the standing orders for food distribution.
    /// </summary>
    public StandingOrders Orders { get; } = new ();

    /// <summary>
    /// Initialize standing orders based on village households.
    /// Called after all village rooms are placed.
    /// </summary>
    /// <param name="village">The village containing the households.</param>
    public void InitializeOrdersFromVillage(Village village)
    {
        foreach (var room in village.Rooms)
        {
            // Skip non-houses
            if (room.Type != "House")
            {
                continue;
            }

            // Check if this house has a baker resident
            bool hasBaker = HasBakerResident(room);

            // Check if this house has a scholar resident (can't produce own food)
            bool hasScholar = HasScholarResident(room);

            if (hasBaker)
            {
                // Baker houses get wheat for baking
                Orders.SetDeliveryTarget(room, "wheat", 10, priority: 1);
            }
            else
            {
                // Scholar houses get higher priority for bread delivery
                // (scholars can't produce their own food)
                int priority = hasScholar ? -1 : 0;
                Orders.SetDeliveryTarget(room, "bread", 5, priority: priority);
            }
        }
    }

    /// <summary>
    /// Check if a room has a baker resident.
    /// </summary>
    private static bool HasBakerResident(Room room)
    {
        foreach (var resident in room.Residents)
        {
            if (resident.SelfAsEntity().HasTrait<BakerJobTrait>())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a room has a scholar resident (who can't produce their own food).
    /// Scholars include the player character and any other entities with ScholarJobTrait.
    /// </summary>
    private static bool HasScholarResident(Room room)
    {
        foreach (var resident in room.Residents)
        {
            if (resident.SelfAsEntity().HasTrait<ScholarJobTrait>())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Update standing order for a specific household.
    /// Call this when a new resident is added to a house after initial setup.
    /// </summary>
    /// <param name="room">The household room to update.</param>
    public void UpdateHouseholdOrder(Room room)
    {
        if (room.Type != "House")
        {
            return;
        }

        bool hasBaker = HasBakerResident(room);
        bool hasScholar = HasScholarResident(room);

        if (hasBaker)
        {
            Orders.SetDeliveryTarget(room, "wheat", 10, priority: 1);
        }
        else
        {
            int priority = hasScholar ? -1 : 0;
            Orders.SetDeliveryTarget(room, "bread", 5, priority: priority);
        }
    }

    /// <summary>
    /// Get a debug summary of standing orders.
    /// </summary>
    public string GetOrdersSummary()
    {
        var targets = Orders.GetDeliveryTargets();
        if (targets.Count == 0)
        {
            return "No standing orders";
        }

        var summary = targets
            .Select(t => $"{t.Household.Name}: {t.DesiredQuantity}x {t.ItemId}")
            .ToList();
        return string.Join(", ", summary);
    }
}
