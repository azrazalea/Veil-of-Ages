using Godot;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Needs.Strategies;

/// <summary>
/// Interface for identifying food sources appropriate for the entity.
/// </summary>
public interface IFoodSourceIdentifier
{
    /// <summary>
    /// Identify a suitable food source for the entity.
    /// </summary>
    /// <param name="owner">The entity looking for food.</param>
    /// <param name="perception">The entity's current perception.</param>
    /// <returns>A food source building or null if none found.</returns>
    Building? IdentifyFoodSource(Being owner, Perception perception);
}

/// <summary>
/// Interface for acquiring food (moving to food sources).
/// </summary>
public interface IFoodAcquisitionStrategy
{
    /// <summary>
    /// Get an action to move to the food source.
    /// </summary>
    /// <param name="owner">The entity acquiring food.</param>
    /// <param name="foodSource">The identified food source.</param>
    /// <returns>An action to move to the food source.</returns>
    EntityAction? GetAcquisitionAction(Being owner, Building foodSource);

    /// <summary>
    /// Check if the entity is at the food source.
    /// </summary>
    /// <param name="owner">The entity checking position.</param>
    /// <param name="foodSource">The food source to check against.</param>
    /// <returns>True if at the food source, false otherwise.</returns>
    bool IsAtFoodSource(Being owner, Building foodSource);
}

/// <summary>
/// Interface for handling the effects of consuming food.
/// </summary>
public interface IConsumptionEffect
{
    /// <summary>
    /// Apply the effects of consuming food.
    /// </summary>
    /// <param name="owner">The entity consuming food.</param>
    /// <param name="need">The need being satisfied.</param>
    /// <param name="foodSource">The food source being consumed from.</param>
    void Apply(Being owner, Need need, Building foodSource);
}

/// <summary>
/// Interface for handling critical need states when no food is available.
/// </summary>
public interface ICriticalStateHandler
{
    /// <summary>
    /// Handle the case when an entity is in a critical need state with no food source available.
    /// </summary>
    /// <param name="owner">The entity in critical state.</param>
    /// <param name="need">The need that is critical.</param>
    /// <returns>An action to take, or null if no special action needed.</returns>
    EntityAction? HandleCriticalState(Being owner, Need need);
}
