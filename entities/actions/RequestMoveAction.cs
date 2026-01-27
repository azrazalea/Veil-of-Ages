using Godot;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action for sapient beings to politely request another entity to move.
/// This is a communication action that costs a turn.
/// The target entity will receive a MoveRequest event and may step aside,
/// ask us to queue, or indicate they're stuck.
/// </summary>
public class RequestMoveAction : EntityAction
{
    private readonly Being _targetEntity;
    private readonly Vector2I _targetPosition;

    public RequestMoveAction(
        Being entity,
        object source,
        Being targetEntity,
        Vector2I targetPosition,
        int priority = 0)
        : base(entity, source, priority: priority)
    {
        _targetEntity = targetEntity;
        _targetPosition = targetPosition;
    }

    public override bool Execute()
    {
        // Validate the target is still valid
        if (!GodotObject.IsInstanceValid(_targetEntity))
        {
            return false;
        }

        // Queue a move request event on the target entity
        // They will process this at the start of their next Think() cycle
        _targetEntity.QueueEvent(
            EntityEventType.MoveRequest,
            Entity,
            new MoveRequestData(_targetPosition));

        return true;
    }
}
