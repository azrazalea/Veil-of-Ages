using System.Collections.Generic;
using System.Linq;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Building trait for granaries that manages standing delivery orders.
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
    /// Called after all village buildings are placed.
    /// </summary>
    /// <param name="village">The village containing the households.</param>
    public void InitializeOrdersFromVillage(Village village)
    {
        foreach (var building in village.Buildings)
        {
            // Skip non-houses
            if (building.BuildingType != "House")
            {
                continue;
            }

            // Check if this house has a baker resident
            bool hasBaker = HasBakerResident(building);

            if (hasBaker)
            {
                // Baker houses get wheat for baking
                Orders.SetDeliveryTarget(building, "wheat", 10, priority: 1);
            }
            else
            {
                // Regular houses get bread
                Orders.SetDeliveryTarget(building, "bread", 5, priority: 0);
            }
        }
    }

    /// <summary>
    /// Check if a building has a baker resident.
    /// </summary>
    private static bool HasBakerResident(Building building)
    {
        foreach (var resident in building.GetResidents())
        {
            if (resident.SelfAsEntity().HasTrait<BakerJobTrait>())
            {
                return true;
            }
        }

        return false;
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
            .Select(t => $"{t.Household.BuildingName}: {t.DesiredQuantity}x {t.ItemId}")
            .ToList();
        return string.Join(", ", summary);
    }
}
