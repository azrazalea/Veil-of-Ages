using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Items;

/// <summary>
/// Autoload singleton manager for loading and creating items from JSON definitions.
/// Register as an autoload in project.godot.
/// </summary>
public partial class ItemResourceManager : ResourceManager<ItemResourceManager, ItemDefinition>
{
    protected override string ResourcePath => "res://resources/items";

    /// <summary>
    /// Create a new item instance from a definition ID.
    /// </summary>
    /// <param name="definitionId">The item definition ID.</param>
    /// <param name="quantity">The quantity to create (default 1).</param>
    /// <returns>A new Item instance or null if definition not found.</returns>
    public Item? CreateItem(string definitionId, int quantity = 1)
    {
        var definition = GetDefinition(definitionId);
        if (definition == null)
        {
            Log.Error($"Cannot create item: definition '{definitionId}' not found");
            return null;
        }

        return new Item(definition, quantity);
    }

    /// <summary>
    /// Get all item definitions matching a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Enumerable of matching item definitions.</returns>
    public IEnumerable<ItemDefinition> GetDefinitionsByCategory(ItemCategory category)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.Category == category)
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all item definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching item definitions.</returns>
    public IEnumerable<ItemDefinition> GetDefinitionsByTag(string tag)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.HasTag(tag))
            {
                yield return definition;
            }
        }
    }
}
