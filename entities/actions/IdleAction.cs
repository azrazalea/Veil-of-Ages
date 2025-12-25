using Godot;

namespace VeilOfAges.Entities.Actions;

public class IdleAction(Being entity, object source, int priority = 1): EntityAction(entity, source, priority: priority)
{
    public override bool Execute()
    {
        // Entity does nothing this tick
        Entity.SetDirection(Vector2.Zero);
        return true;
    }
}
