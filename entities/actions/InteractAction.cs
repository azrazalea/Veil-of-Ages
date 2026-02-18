using VeilOfAges.UI;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action that interacts with an IInteractable target.
/// Checks adjacency via TryInteract, then lets the interactable handle its own behavior.
/// Executes on the main thread so UI operations (dialogue, etc.) are safe.
/// </summary>
public class InteractAction : EntityAction
{
    private readonly IInteractable _target;
    private readonly Dialogue _dialogue;

    public InteractAction(Being entity, object source, IInteractable target, Dialogue dialogue, int priority = 0)
        : base(entity, source, priority: priority)
    {
        _target = target;
        _dialogue = dialogue;
    }

    public override bool Execute()
    {
        return _target.TryInteract(Entity, _dialogue);
    }
}
