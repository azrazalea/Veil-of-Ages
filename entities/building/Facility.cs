using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities;

/// <summary>
/// A functional component within a building (e.g., oven, well, storage area).
/// Facilities can have their own traits but are not full entities.
/// </summary>
public class Facility
{
    public string Id { get; }
    public Vector2I Position { get; }
    public bool RequireAdjacent { get; }
    public Building Owner { get; }
    public List<Trait> Traits { get; } = new ();

    public Facility(string id, Vector2I position, bool requireAdjacent, Building owner)
    {
        Id = id;
        Position = position;
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
    /// Get the absolute grid position of this facility.
    /// </summary>
    /// <returns>The absolute grid position (building position + facility position).</returns>
    public Vector2I GetAbsolutePosition()
    {
        return Owner.GetCurrentGridPosition() + Position;
    }
}
