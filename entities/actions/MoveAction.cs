using Godot;

namespace VeilOfAges.Entities.Actions
{
    public class MoveAction(Being entity, object source, Vector2I targetPosition, int priority = 0) : EntityAction(entity, source, priority)
    {
        private Vector2I _targetPosition = targetPosition;

        public override void Execute()
        {
            Entity.TryMoveToGridPosition(_targetPosition);
        }
    }
}
