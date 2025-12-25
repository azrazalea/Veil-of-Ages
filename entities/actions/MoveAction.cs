using Godot;

namespace VeilOfAges.Entities.Actions;

public class MoveAction(Being entity, object source, Vector2I targetPosition, int priority = 1): EntityAction(entity, source, priority: priority)
{
    private Vector2I _targetPosition = targetPosition;

    public override bool Execute()
    {
        return Entity.TryMoveToGridPosition(_targetPosition);
    }
}
