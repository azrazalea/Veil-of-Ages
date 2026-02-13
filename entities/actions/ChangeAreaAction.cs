namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Main-thread action that transitions an entity to a different area via a TransitionPoint.
/// Called from GoToTransitionActivity after entity reaches the transition point position.
/// </summary>
public class ChangeAreaAction : EntityAction
{
    private readonly TransitionPoint _destination;

    public ChangeAreaAction(Being entity, object source, TransitionPoint destination, int priority = 0)
        : base(entity, source, priority: priority)
    {
        _destination = destination;
    }

    public override bool Execute()
    {
        // Get World reference from scene tree
        if (Entity.GetTree().GetFirstNodeInGroup("World") is not World world)
        {
            return false;
        }

        world.TransitionEntity(Entity, _destination);
        return true;
    }
}
