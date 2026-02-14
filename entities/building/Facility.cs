using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Entities.WorkOrders;

namespace VeilOfAges.Entities;

/// <summary>
/// A functional component within a building (e.g., oven, well, storage area).
/// Facilities can have their own traits but are not full entities.
/// </summary>
public class Facility
{
    public string Id { get; }
    public List<Vector2I> Positions { get; }
    public bool RequireAdjacent { get; }
    public Building Owner { get; }
    public List<Trait> Traits { get; } = new ();

    /// <summary>
    /// Gets or sets the interaction handler for this facility, if any.
    /// </summary>
    public IFacilityInteractable? Interactable { get; set; }

    public Facility(string id, List<Vector2I> positions, bool requireAdjacent, Building owner)
    {
        Id = id;
        Positions = positions;
        RequireAdjacent = requireAdjacent;
        Owner = owner;
    }

    /// <summary>
    /// Add a trait to this facility.
    /// </summary>
    public void AddTrait(Trait trait)
    {
        Traits.Add(trait);
    }

    /// <summary>
    /// Get the first trait of the specified type.
    /// </summary>
    /// <typeparam name="T">The trait type to find.</typeparam>
    /// <returns>The first matching trait, or null if not found.</returns>
    public T? GetTrait<T>()
        where T : Trait
    {
        return Traits.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Check if this facility has a trait of the specified type.
    /// </summary>
    /// <typeparam name="T">The trait type to check for.</typeparam>
    /// <returns>True if the facility has the trait, false otherwise.</returns>
    public bool HasTrait<T>()
        where T : Trait
    {
        return Traits.OfType<T>().Any();
    }

    /// <summary>
    /// Get all absolute grid positions of this facility.
    /// </summary>
    /// <returns>The absolute grid positions (building position + each facility position).</returns>
    public List<Vector2I> GetAbsolutePositions()
    {
        var buildingPos = Owner.GetCurrentGridPosition();
        return Positions.Select(p => buildingPos + p).ToList();
    }

    /// <summary>
    /// Gets the currently active work order on this facility, if any.
    /// </summary>
    public WorkOrder? ActiveWorkOrder { get; private set; }

    /// <summary>
    /// Start a work order on this facility.
    /// </summary>
    public void StartWorkOrder(WorkOrder order)
    {
        if (ActiveWorkOrder != null)
        {
            Log.Warn($"Facility {Id}: Cannot start work order - already has active order");
            return;
        }

        ActiveWorkOrder = order;
    }

    /// <summary>
    /// Complete and clear the active work order.
    /// </summary>
    public void CompleteWorkOrder()
    {
        ActiveWorkOrder = null;
    }

    /// <summary>
    /// Cancel the active work order (progress is lost).
    /// </summary>
    public void CancelWorkOrder()
    {
        ActiveWorkOrder = null;
    }
}
