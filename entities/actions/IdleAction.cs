using Godot;

namespace VeilOfAges.Entities.Actions
{
    public class IdleAction(Being entity, object source, int priority = 0) : EntityAction(entity, source, priority)
    {
        public override void Execute()
        {
            // Entity does nothing this tick
            Entity.SetDirection(Vector2.Zero);
        }
    }
}
