using Godot;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action for mindless beings to physically push another entity.
/// This is essentially an attack action that costs a turn.
/// The target entity will receive an EntityPushed event and will stumble
/// in the push direction if possible.
/// </summary>
public class PushAction : EntityAction
{
    private readonly Being _targetEntity;
    private readonly Vector2I _pushDirection;

    public PushAction(
        Being entity,
        object source,
        Being targetEntity,
        Vector2I pushDirection,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _targetEntity = targetEntity;
        _pushDirection = pushDirection;
    }

    public override bool Execute()
    {
        // Validate the target is still valid
        if (!GodotObject.IsInstanceValid(_targetEntity))
        {
            return false;
        }

        // Queue a push event on the target entity
        // They will process this at the start of their next Think() cycle
        // and stumble in the push direction
        _targetEntity.QueueEvent(
            EntityEventType.EntityPushed,
            Entity,
            new PushData(_pushDirection));

        return true;
    }
}
