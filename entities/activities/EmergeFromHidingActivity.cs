using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity that makes a hidden entity emerge (become visible again).
/// Used when undead emerge from graveyard at dusk.
/// </summary>
public class EmergeFromHidingActivity : Activity
{
    public override string DisplayName => L.Tr("activity.EMERGING");

    /// <summary>
    /// Initializes a new instance of the <see cref="EmergeFromHidingActivity"/> class.
    /// Creates an activity to emerge from hiding.
    /// </summary>
    /// <param name="priority">Action priority.</param>
    public EmergeFromHidingActivity(int priority = 0)
    {
        Priority = priority;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Simply unhide the entity
        _owner.IsHidden = false;
        DebugLog("EMERGE", "Now visible", 0);
        Complete();
        return new IdleAction(_owner, this, Priority);
    }
}
