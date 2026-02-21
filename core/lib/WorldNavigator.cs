using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Grid;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Represents a step in a navigation plan.
/// </summary>
public abstract record NavigationStep;

/// <summary>
/// Move to a position within the current area using pathfinding.
/// </summary>
public record GoToPositionStep(Area Area, Vector2I Position): NavigationStep;

/// <summary>
/// Traverse a transition point to change areas.
/// </summary>
public record TransitionStep(TransitionPoint TransitionPoint): NavigationStep;

/// <summary>
/// A complete navigation plan from source to destination, potentially across areas.
/// </summary>
public class NavigationPlan
{
    public List<NavigationStep> Steps { get; } = new ();

    /// <summary>
    /// Gets the facility reference at the destination, if navigating to a facility.
    /// </summary>
    public FacilityReference? TargetFacility { get; init; }

    public bool IsEmpty => Steps.Count == 0;

    public bool IsCrossArea => Steps.Any(s => s is TransitionStep);
}

/// <summary>
/// Static utility for cross-area route planning using an entity's knowledge.
/// Does NOT access the World directly — only uses the entity's SharedKnowledge
/// and PersonalMemory to discover routes. Entities only know what their
/// knowledge sources tell them.
/// </summary>
public static class WorldNavigator
{
    /// <summary>
    /// Cross-area distance penalty applied when comparing facilities/buildings
    /// across different areas to prefer same-area targets.
    /// </summary>
    public const float CROSSAREAPENALTY = 10000f;

    /// <summary>
    /// Find a route from one area to another using known transition points.
    /// Uses BFS on the area connectivity graph built from the entity's knowledge.
    /// </summary>
    /// <param name="entity">The entity whose knowledge to query.</param>
    /// <param name="sourceArea">Starting area.</param>
    /// <param name="targetArea">Destination area.</param>
    /// <returns>List of transition points to traverse, or null if no route found.</returns>
    public static List<TransitionPoint>? FindRouteToArea(Being entity, Area sourceArea, Area targetArea)
    {
        if (sourceArea == targetArea)
        {
            return new List<TransitionPoint>();
        }

        // Gather ALL known transition points from the entity's knowledge
        var allTransitions = GetKnownTransitionPoints(entity);

        // BFS on area connectivity
        var visited = new HashSet<Area> { sourceArea };
        var queue = new Queue<(Area area, List<TransitionPoint> path)>();
        queue.Enqueue((sourceArea, new List<TransitionPoint>()));

        while (queue.Count > 0)
        {
            var (currentArea, path) = queue.Dequeue();

            // Find all transitions FROM this area that have a valid linked point
            var transitionsFromHere = allTransitions
                .Where(t => t.SourceArea == currentArea && t.TransitionPoint.LinkedPoint != null)
                .ToList();

            foreach (var transition in transitionsFromHere)
            {
                var linkedPoint = transition.TransitionPoint.LinkedPoint!;
                var nextArea = linkedPoint.SourceArea;

                if (visited.Contains(nextArea))
                {
                    continue;
                }

                visited.Add(nextArea);

                var newPath = new List<TransitionPoint>(path) { transition.TransitionPoint };

                if (nextArea == targetArea)
                {
                    return newPath;
                }

                queue.Enqueue((nextArea, newPath));
            }
        }

        return null; // No route found
    }

    /// <summary>
    /// Create a navigation plan to reach a specific position, potentially in another area.
    /// </summary>
    public static NavigationPlan? NavigateToPosition(Being entity, Area sourceArea, Vector2I sourcePos, Area targetArea, Vector2I targetPos)
    {
        var plan = new NavigationPlan();

        if (sourceArea == targetArea)
        {
            // Same area — simple navigation
            plan.Steps.Add(new GoToPositionStep(targetArea, targetPos));
            return plan;
        }

        // Cross-area — find route
        var route = FindRouteToArea(entity, sourceArea, targetArea);
        if (route == null)
        {
            return null;
        }

        foreach (var transitionPoint in route)
        {
            // Go to the transition point in current area
            plan.Steps.Add(new GoToPositionStep(transitionPoint.SourceArea, transitionPoint.SourcePosition));

            // Execute transition
            plan.Steps.Add(new TransitionStep(transitionPoint));
        }

        // Final step: go to target position in target area
        plan.Steps.Add(new GoToPositionStep(targetArea, targetPos));

        return plan;
    }

    /// <summary>
    /// Create a navigation plan to reach the nearest facility of a given type.
    /// Checks SharedKnowledge first (permanent village knowledge), then PersonalMemory (discovered).
    /// </summary>
    public static NavigationPlan? NavigateToFacility(Being entity, string facilityType)
    {
        var currentArea = entity.GridArea;
        var currentPos = entity.GetCurrentGridPosition();
        if (currentArea == null)
        {
            return null;
        }

        // Search SharedKnowledge for the nearest facility
        FacilityReference? bestFacility = null;
        float bestDist = float.MaxValue;

        foreach (var knowledge in entity.SharedKnowledge)
        {
            var candidate = knowledge.GetNearestFacilityOfType(facilityType, currentArea, currentPos);
            if (candidate == null)
            {
                continue;
            }

            float dist = currentPos.DistanceSquaredTo(candidate.Position);
            if (candidate.Area != currentArea)
            {
                dist += CROSSAREAPENALTY;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                bestFacility = candidate;
            }
        }

        if (bestFacility != null)
        {
            // Build plan to the facility from SharedKnowledge
            var facilityArea = bestFacility.Area ?? currentArea;
            var plan = NavigateToPosition(entity, currentArea, currentPos, facilityArea, bestFacility.Position);
            if (plan == null)
            {
                return null;
            }

            // Wrap the steps with facility metadata
            var result = new NavigationPlan
            {
                TargetFacility = bestFacility,
            };
            foreach (var step in plan.Steps)
            {
                result.Steps.Add(step);
            }

            return result;
        }

        // Not found in SharedKnowledge — check PersonalMemory
        if (entity.Memory == null)
        {
            return null;
        }

        var recalled = entity.Memory.RecallFacilitiesOfType(facilityType);
        if (recalled.Count == 0)
        {
            return null;
        }

        // Find nearest recalled facility
        FacilityObservation? nearestObs = null;
        float nearestObsDist = float.MaxValue;

        foreach (var obs in recalled)
        {
            float dist = currentPos.DistanceSquaredTo(obs.Position);
            if (obs.Area != currentArea)
            {
                dist += CROSSAREAPENALTY;
            }

            if (dist < nearestObsDist)
            {
                nearestObsDist = dist;
                nearestObs = obs;
            }
        }

        if (nearestObs == null)
        {
            return null;
        }

        var targetArea = nearestObs.Area ?? currentArea;
        return NavigateToPosition(entity, currentArea, currentPos, targetArea, nearestObs.Position);
    }

    /// <summary>
    /// Gather all transition points known to the entity from SharedKnowledge and PersonalMemory.
    /// </summary>
    private static List<TransitionPointReference> GetKnownTransitionPoints(Being entity)
    {
        var result = new List<TransitionPointReference>();
        var seen = new HashSet<TransitionPoint>();

        // From SharedKnowledge (permanent village knowledge)
        foreach (var knowledge in entity.SharedKnowledge)
        {
            foreach (var tp in knowledge.GetAllTransitionPoints())
            {
                if (seen.Add(tp.TransitionPoint))
                {
                    result.Add(tp);
                }
            }
        }

        // PersonalMemory doesn't track transition points currently,
        // but could be extended later for exploration
        return result;
    }
}
