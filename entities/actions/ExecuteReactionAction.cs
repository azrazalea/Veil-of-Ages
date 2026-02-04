using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action that executes a reaction using ONLY the entity's inventory.
/// Consumes inputs from inventory and produces outputs to inventory.
/// This is an atomic action - all inputs must be present or the action fails.
/// </summary>
public class ExecuteReactionAction : EntityAction
{
    private readonly ReactionDefinition _reaction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteReactionAction"/> class.
    /// </summary>
    /// <param name="entity">The entity executing the reaction.</param>
    /// <param name="source">The source object that created this action.</param>
    /// <param name="reaction">The reaction definition to execute.</param>
    /// <param name="priority">Priority for this action.</param>
    public ExecuteReactionAction(
        Being entity,
        object source,
        ReactionDefinition reaction,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _reaction = reaction;
    }

    /// <inheritdoc/>
    public override bool Execute()
    {
        // Get entity's inventory
        var inventory = Entity.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory == null)
        {
            Log.Warn($"ExecuteReactionAction: Entity {Entity.Name} has no inventory");
            return false;
        }

        // Verify ALL inputs are in inventory before consuming any
        foreach (var input in _reaction.Inputs)
        {
            if (input.ItemId == null)
            {
                Log.Warn($"ExecuteReactionAction: Reaction {_reaction.Id} has null input ItemId");
                return false;
            }

            if (!inventory.HasItem(input.ItemId, input.Quantity))
            {
                Log.Warn($"ExecuteReactionAction: Missing input {input.ItemId} x{input.Quantity} for reaction {_reaction.Id}");
                return false;
            }
        }

        // Remove ALL inputs from inventory
        foreach (var input in _reaction.Inputs)
        {
            var removed = inventory.RemoveItem(input.ItemId!, input.Quantity);
            if (removed == null || removed.Quantity < input.Quantity)
            {
                // This shouldn't happen since we verified above, but log just in case
                Log.Error($"ExecuteReactionAction: Failed to remove input {input.ItemId} x{input.Quantity} - this should not happen");
                return false;
            }
        }

        // Create and add ALL outputs to inventory
        foreach (var output in _reaction.Outputs)
        {
            if (output.ItemId == null)
            {
                Log.Warn($"ExecuteReactionAction: Reaction {_reaction.Id} has null output ItemId");
                continue;
            }

            var definition = ItemResourceManager.Instance.GetDefinition(output.ItemId);
            if (definition == null)
            {
                Log.Warn($"ExecuteReactionAction: Unknown output item {output.ItemId} for reaction {_reaction.Id}");
                continue;
            }

            var outputItem = new Item(definition, output.Quantity);
            if (!inventory.AddItem(outputItem))
            {
                Log.Warn($"ExecuteReactionAction: Failed to add output {output.ItemId} x{output.Quantity} to inventory - may be full");

                // Continue adding other outputs even if one fails
            }
        }

        Log.Print($"ExecuteReactionAction: Entity {Entity.Name} completed reaction {_reaction.Name}");
        return true;
    }
}
