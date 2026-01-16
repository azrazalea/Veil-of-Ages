using Godot;

namespace VeilOfAges.Entities.Actions;

public class InteractAction : EntityAction
{
    private readonly Vector2I _targetPosition;

    public InteractAction(Being entity, object source, Vector2I targetPosition, int priority = 0)
        : base(entity, source, priority: priority)
    {
        _targetPosition = targetPosition;
    }

    public override bool Execute()
    {
        // Find what's at the target position and interact with it
        if (Entity.GetTree().GetFirstNodeInGroup("World") is not World)
        {
            return false;
        }

        // TODO: Implement
        // world.InteractAtPosition(Entity, _targetPosition);
        _ = _targetPosition; // Suppress warning until implemented
        return false;
    }
}
