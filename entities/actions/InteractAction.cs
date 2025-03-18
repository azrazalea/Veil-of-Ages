using Godot;

namespace VeilOfAges.Entities.Actions
{
    public class InteractAction : EntityAction
    {
        private Vector2I _targetPosition;

        public InteractAction(Being entity, Vector2I targetPosition, int priority = 0)
            : base(entity, priority)
        {
            _targetPosition = targetPosition;
        }

        public override void Execute()
        {
            // Find what's at the target position and interact with it
            var world = Entity.GetTree().GetFirstNodeInGroup("World") as World;
            // TODO: Implement
            // world?.InteractAtPosition(Entity, _targetPosition);
        }
    }
}
