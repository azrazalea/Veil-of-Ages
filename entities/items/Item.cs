using System;

namespace VeilOfAges.Entities.Items;

/// <summary>
/// Runtime instance of an item, representing a stack of items in the game world.
/// </summary>
public class Item
{
    /// <summary>
    /// Gets the definition that describes this item's properties.
    /// </summary>
    public ItemDefinition Definition { get; }

    /// <summary>
    /// Gets or sets current quantity in this stack.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets decay progress from 0 to 1, where 1 means fully spoiled.
    /// </summary>
    public float DecayProgress { get; private set; }

    /// <summary>
    /// Gets total volume of this stack in cubic meters.
    /// </summary>
    public float TotalVolume => Definition.VolumeM3 * Quantity;

    /// <summary>
    /// Gets total weight of this stack in kilograms.
    /// </summary>
    public float TotalWeight => Definition.WeightKg * Quantity;

    /// <summary>
    /// Gets a value indicating whether whether this item has fully decayed and is spoiled.
    /// </summary>
    public bool IsSpoiled => DecayProgress >= 1.0f;

    /// <summary>
    /// Gets a value indicating whether whether this item can be eaten.
    /// </summary>
    public bool IsEdible => Definition.EdibleNutrition > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Item"/> class.
    /// Create a new item instance.
    /// </summary>
    /// <param name="definition">The item definition for this item.</param>
    /// <param name="quantity">Initial quantity (default 1).</param>
    /// <exception cref="ArgumentNullException">Thrown if definition is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if quantity is less than 1.</exception>
    public Item(ItemDefinition definition, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1");
        }

        Definition = definition;
        Quantity = Math.Min(quantity, definition.StackLimit);
        DecayProgress = 0f;
    }

    /// <summary>
    /// Apply decay to this item based on its decay rate and an optional modifier.
    /// </summary>
    /// <param name="decayRateModifier">Modifier from storage container (1.0 = normal, 0.5 = half rate, etc.).</param>
    public void ApplyDecay(float decayRateModifier = 1.0f)
    {
        if (Definition.BaseDecayRatePerTick <= 0 || IsSpoiled)
        {
            return;
        }

        float decayAmount = Definition.BaseDecayRatePerTick * decayRateModifier;
        DecayProgress = Math.Min(1.0f, DecayProgress + decayAmount);
    }

    /// <summary>
    /// Split off a portion of this stack, creating a new Item with the specified amount.
    /// </summary>
    /// <param name="amount">The number of items to split off.</param>
    /// <returns>A new Item with the split amount, or null if the split is not possible.</returns>
    public Item? Split(int amount)
    {
        if (amount < 1 || amount >= Quantity)
        {
            return null;
        }

        Quantity -= amount;

        var newItem = new Item(Definition, amount)
        {
            DecayProgress = DecayProgress
        };

        return newItem;
    }

    /// <summary>
    /// Try to merge another item into this stack.
    /// </summary>
    /// <param name="other">The item to merge into this stack.</param>
    /// <returns>Leftover item if not all could be merged, or null if fully merged.</returns>
    public Item? TryMerge(Item other)
    {
        if (other == null || other.Definition.Id != Definition.Id)
        {
            return other;
        }

        // Don't merge if decay states are too different (threshold: 10% difference)
        if (Math.Abs(other.DecayProgress - DecayProgress) > 0.1f)
        {
            return other;
        }

        int availableSpace = Definition.StackLimit - Quantity;

        if (availableSpace <= 0)
        {
            return other;
        }

        if (other.Quantity <= availableSpace)
        {
            // Merge all
            Quantity += other.Quantity;

            // Average the decay progress weighted by quantity
            float totalQuantity = Quantity;
            float myWeight = (Quantity - other.Quantity) / totalQuantity;
            float otherWeight = other.Quantity / totalQuantity;
            DecayProgress = (DecayProgress * myWeight) + (other.DecayProgress * otherWeight);

            return null;
        }
        else
        {
            // Partial merge
            int originalQuantity = Quantity;
            Quantity = Definition.StackLimit;

            // Calculate weighted average decay for merged portion
            float totalQuantity = Quantity;
            float myWeight = originalQuantity / totalQuantity;
            float otherWeight = availableSpace / totalQuantity;
            DecayProgress = (DecayProgress * myWeight) + (other.DecayProgress * otherWeight);

            other.Quantity -= availableSpace;
            return other;
        }
    }

    /// <summary>
    /// Sets set the decay progress directly (used when creating items with pre-existing decay).
    /// </summary>
    internal float DecayProgressInternal
    {
        set => DecayProgress = Math.Clamp(value, 0f, 1f);
    }
}
