using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.UI;

namespace VeilOfAges.Entities.Traits;

public class MindlessTrait : BeingTrait
{
    private enum MindlessState
    {
        Idle,
        Wandering
    }

    /// <summary>
    /// Mindless beings push other entities instead of communicating.
    /// </summary>
    public override EntityAction? GetBlockingResponse(Being blockingEntity, Vector2I targetPosition)
    {
        if (_owner == null)
        {
            return null;
        }

        // Calculate push direction from our position toward the target
        var myPos = _owner.GetCurrentGridPosition();
        var pushDirection = (targetPosition - myPos).Sign();

        return new PushAction(_owner, this, blockingEntity, pushDirection, priority: 0);
    }

    /// <summary>
    /// Mindless beings don't understand communication.
    /// They ignore MoveRequest events - the requester must push them instead.
    /// </summary>
    public override bool HandleReceivedEvent(EntityEvent evt)
    {
        switch (evt.Type)
        {
            case EntityEventType.MoveRequest:
                // Ignore - mindless beings don't understand communication
                return true; // Handled (by ignoring)

            default:
                return false; // Let default handling take over
        }
    }

    public override bool IsOptionAvailable(DialogueOption option)
    {
        if (option.Command == null)
        {
            return true;
        }

        return !option.Command.IsComplex;
    }

    public override string InitialDialogue(Being speaker)
    {
        return "The mindless being looks at you with its blank stare.";
    }

    public override string? GetSuccessResponse(EntityCommand command)
    {
        return "The being silently begins doing as you asked.";
    }

    public override string? GetFailureResponse(EntityCommand command)
    {
        return "The being does not move to obey.";
    }

    public override string? GetSuccessResponse(string text)
    {
        return "The being silently begins doing as you asked.";
    }

    public override string? GetFailureResponse(string text)
    {
        return "The being does not move to obey.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am a non-sapiant being.";
    }
}
