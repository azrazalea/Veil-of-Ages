using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Shared utility for creating navigation activities that handle cross-area travel.
/// Eliminates duplicated cross-area navigation logic across activities.
/// </summary>
public static class NavigationHelper
{
    /// <summary>
    /// Creates the appropriate navigation activity for reaching a target building.
    /// If the building is in a different area from the entity, uses WorldNavigator
    /// with GoToWorldPositionActivity for cross-area navigation. Otherwise uses
    /// GoToBuildingActivity for same-area navigation.
    /// </summary>
    /// <param name="owner">The entity navigating.</param>
    /// <param name="target">The building to navigate to.</param>
    /// <param name="priority">Action priority for the navigation activity.</param>
    /// <param name="targetStorage">If true, navigate to storage access position.</param>
    /// <returns>A navigation activity (GoToWorldPositionActivity or GoToBuildingActivity).</returns>
    public static Activity CreateNavigationToBuilding(
        Being owner, Building target, int priority, bool targetStorage = false)
    {
        if (owner.GridArea != null && target.GridArea != null
            && owner.GridArea != target.GridArea)
        {
            var plan = WorldNavigator.NavigateToPosition(
                owner, owner.GridArea, owner.GetCurrentGridPosition(),
                target.GridArea, target.GetCurrentGridPosition());
            if (plan != null)
            {
                return new GoToWorldPositionActivity(plan, priority);
            }
        }

        return new GoToBuildingActivity(target, priority, targetStorage: targetStorage);
    }

    /// <summary>
    /// Creates the appropriate navigation activity for reaching a facility position.
    /// If the facility is in a different area from the entity, uses WorldNavigator
    /// with GoToWorldPositionActivity for cross-area navigation. Otherwise uses
    /// GoToLocationActivity for same-area navigation.
    /// </summary>
    /// <param name="owner">The entity navigating.</param>
    /// <param name="facilityRef">Reference to the target facility.</param>
    /// <param name="priority">Action priority for the navigation activity.</param>
    /// <returns>A navigation activity (GoToWorldPositionActivity or GoToLocationActivity).</returns>
    public static Activity CreateNavigationToFacility(
        Being owner, FacilityReference facilityRef, int priority)
    {
        var currentArea = owner.GridArea;
        var targetArea = facilityRef.Area;

        if (targetArea != null && targetArea != currentArea)
        {
            var plan = WorldNavigator.NavigateToPosition(
                owner, currentArea!, owner.GetCurrentGridPosition(),
                targetArea, facilityRef.Position);
            if (plan != null)
            {
                return new GoToWorldPositionActivity(plan, priority);
            }
        }

        return new GoToLocationActivity(facilityRef.Position, priority);
    }
}
