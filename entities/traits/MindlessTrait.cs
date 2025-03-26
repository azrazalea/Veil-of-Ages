using VeilOfAges.UI;


namespace VeilOfAges.Entities.Traits
{
    public class MindlessTrait : BeingTrait
    {
        private enum MindlessState { Idle, Wandering }

        public override bool IsOptionAvailable(DialogueOption option)
        {
            if (option.Command == null) return true;

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
}
