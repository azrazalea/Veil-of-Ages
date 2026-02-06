using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to their home building and then hides them.
/// Used by undead to retreat to the graveyard at dawn.
/// </summary>
public class HideInBuildingActivity : Activity
{
    private readonly Building _targetBuilding;
    private GoToBuildingActivity? _goToPhase;
    private bool _hasArrived;

    public override string DisplayName => _hasArrived
        ? "Hiding"
        : $"Retreating to {_targetBuilding.BuildingType}";

    public override Building? TargetBuilding => _targetBuilding;

    /// <summary>
    /// Initializes a new instance of the <see cref="HideInBuildingActivity"/> class.
    /// Creates an activity to navigate to a building and hide inside.
    /// </summary>
    /// <param name="targetBuilding">The building to hide in.</param>
    /// <param name="priority">Action priority.</param>
    public HideInBuildingActivity(Building targetBuilding, int priority = 0)
    {
        _targetBuilding = targetBuilding;
        Priority = priority;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _goToPhase = null; // Force fresh pathfinder
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
            DebugLog("HIDE", "Target building no longer valid", 0);
            Fail();
            return null;
        }

        // Phase 1: Navigate to building interior
        if (!_hasArrived)
        {
            if (_goToPhase == null)
            {
                _goToPhase = new GoToBuildingActivity(_targetBuilding, Priority, requireInterior: true);
                _goToPhase.Initialize(_owner);
            }

            var (result, action) = RunSubActivity(_goToPhase, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    DebugLog("HIDE", "Failed to reach building", 0);
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    _hasArrived = true;
                    DebugLog("HIDE", $"Arrived at {_targetBuilding.BuildingName}, now hiding", 0);
                    break;
            }
        }

        // Phase 2: Hide the entity
        if (_hasArrived)
        {
            _owner.IsHidden = true;
            DebugLog("HIDE", "Now hidden", 0);
            Complete();
            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }
}
