using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities.Traits
{
    public class MindlessTrait : ITrait
    {
        protected Being? _owner;

        private enum MindlessState { Idle, Wandering }
        public bool IsInitialized { get; protected set; }
        public int Priority { get; set; }

        public virtual void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;

            GD.Print($"{_owner.Name}: Mindless trait initialized");
            IsInitialized = true;
        }

        public virtual void Process(double delta)
        {

        }
        public EntityAction? SuggestAction(Vector2 currentOwnerPosition, Perception currentPerception)
        {
            return null;
        }

        public bool RefusesCommand(EntityCommand command)
        {
            return false;
        }

        public bool IsOptionAvailable(DialogueOption option)
        {
            if (option.Command == null) return true;

            return !option.Command.IsComplex;
        }

        public string InitialDialogue(Being speaker)
        {
            return "The mindless being looks at you with its blank stare.";
        }

        public virtual void OnEvent(string eventName, params object[] args)
        {
        }

        public string? GetSuccessResponse(EntityCommand command)
        {
            return "The being silently begins doing as you asked.";
        }
        public string? GetFailureResponse(EntityCommand command)
        {
            return "The being does not move to obey.";
        }
        public string? GetSuccessResponse(string text)
        {
            return "The being silently begins doing as you asked.";
        }
        public string? GetFailureResponse(string text)
        {
            return "The being does not move to obey.";
        }
    }
}
