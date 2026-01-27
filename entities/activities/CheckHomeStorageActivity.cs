using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that goes to a building and observes its storage contents.
/// Used when an entity needs to refresh their memory of what's in storage
/// (e.g., hungry but no memory of food locations).
///
/// This activity:
/// 1. Navigates to the target building using GoToBuildingActivity
/// 2. Calls AccessStorage to observe and update memory
/// 3. Completes (allowing other traits to then act on the updated memory).
/// </summary>
public class CheckHomeStorageActivity : Activity
{
    private readonly Building _targetBuilding;
    private GoToBuildingActivity? _goToPhase;
    private bool _hasObserved;

    public override string DisplayName => _hasObserved
        ? $"Checked {_targetBuilding.BuildingName}"
        : $"Going to check {_targetBuilding.BuildingName}";
    public override Building? TargetBuilding => _targetBuilding;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckHomeStorageActivity"/> class.
    /// Create an activity to go to a building and observe its storage.
    /// </summary>
    /// <param name="targetBuilding">The building to check.</param>
    /// <param name="priority">Action priority.</param>
    public CheckHomeStorageActivity(Building targetBuilding, int priority = 0)
    {
        _targetBuilding = targetBuilding;
        Priority = priority;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if building still exists
        if (!GodotObject.IsInstanceValid(_targetBuilding))
        {
            DebugLog("CHECK_STORAGE", "Building no longer valid, failing", 0);
            Fail();
            return null;
        }

        // Phase 1: Navigate to building (targeting storage position)
        if (_goToPhase == null)
        {
            _goToPhase = new GoToBuildingActivity(_targetBuilding, Priority, targetStorage: true);
            _goToPhase.Initialize(_owner);
            DebugLog("CHECK_STORAGE", $"Starting navigation to {_targetBuilding.BuildingName}", 0);
        }

        // Run navigation sub-activity
        var (result, action) = RunSubActivity(_goToPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("CHECK_STORAGE", "Navigation failed", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to observation
                break;
        }

        // Phase 2: Observe storage (we've arrived at the building)
        if (!_hasObserved)
        {
            var storage = _owner.AccessStorage(_targetBuilding);
            _hasObserved = true;

            if (storage != null)
            {
                DebugLog("CHECK_STORAGE", $"Observed storage: {storage.GetContentsSummary()}", 0);
            }
            else
            {
                DebugLog("CHECK_STORAGE", "Building has no storage", 0);
            }

            // Complete immediately after observing
            Complete();
            return null;
        }

        // Should not reach here, but complete if we do
        Complete();
        return null;
    }
}
