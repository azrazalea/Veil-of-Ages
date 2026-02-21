using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that moves an entity to a room and then hides them.
/// Used by undead to retreat to the graveyard at dawn.
/// </summary>
public class HideInBuildingActivity : Activity
{
    private readonly Room _targetRoom;
    private Activity? _goToPhase;
    private bool _hasArrived;

    public override string DisplayName => _hasArrived
        ? L.Tr("activity.HIDING")
        : L.TrFmt("activity.RETREATING_TO", _targetRoom.Type ?? _targetRoom.Name);

    public override Room? TargetRoom => _targetRoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="HideInBuildingActivity"/> class.
    /// Creates an activity to navigate to a room and hide inside.
    /// </summary>
    /// <param name="targetRoom">The room to hide in.</param>
    /// <param name="priority">Action priority.</param>
    public HideInBuildingActivity(Room targetRoom, int priority = 0)
    {
        _targetRoom = targetRoom;
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

        // Check if room still exists (Room is plain C#, not GodotObject)
        if (_targetRoom.IsDestroyed)
        {
            DebugLog("HIDE", "Target room no longer valid", 0);
            Fail();
            return null;
        }

        // Phase 1: Navigate to room interior
        if (!_hasArrived)
        {
            if (_goToPhase == null)
            {
                _goToPhase = new GoToRoomActivity(_targetRoom, Priority, requireInterior: true);
                _goToPhase.Initialize(_owner);
            }

            var (result, action) = RunSubActivity(_goToPhase, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    DebugLog("HIDE", "Failed to reach room", 0);
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    _hasArrived = true;
                    DebugLog("HIDE", $"Arrived at {_targetRoom.Name}, now hiding", 0);
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
