namespace VeilOfAges.Entities.Reactions;

/// <summary>
/// Simple record for representing an item and its quantity.
/// Used in reaction inputs/outputs and storage systems.
/// </summary>
/// <param name="ItemId">The unique identifier of the item definition.</param>
/// <param name="Quantity">The number of items.</param>
public record ItemQuantity(string ItemId, int Quantity);
