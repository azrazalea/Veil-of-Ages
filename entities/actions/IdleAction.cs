using Godot;

namespace NecromancerKingdom.Entities.Actions
{
    public class IdleAction(Being entity, int priority = 0) : EntityAction(entity, priority)
    {
        public override void Execute()
        {
            // Entity does nothing this tick
            Entity.SetDirection(Vector2.Zero);
        }
    }
}
