using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Reactions;

/// <summary>
/// Singleton manager for loading and accessing reaction definitions.
/// Registered as a Godot autoload for automatic initialization.
/// </summary>
public partial class ReactionResourceManager : ResourceManager<ReactionResourceManager, ReactionDefinition>
{
    protected override string ResourcePath => "res://resources/reactions";

    /// <summary>
    /// Get all reaction definitions that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsByTag(string tag)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.HasTag(tag))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all reaction definitions that can be performed with the available facilities.
    /// </summary>
    /// <param name="availableFacilities">Set of facility IDs available at the location.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsForFacilities(IEnumerable<string>? availableFacilities)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.CanPerformWith(availableFacilities))
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Get all reaction definitions that require a specific facility.
    /// </summary>
    /// <param name="facilityId">The facility ID to filter by.</param>
    /// <returns>Enumerable of matching reaction definitions.</returns>
    public IEnumerable<ReactionDefinition> GetReactionsRequiringFacility(string facilityId)
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.RequiresFacility(facilityId))
            {
                yield return definition;
            }
        }
    }
}
