using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities
{
    // Base interface for all traits
    public interface ITrait
    {
        public bool IsInitialized { get; }
        public int Priority { get; set; }

        // Initialize the trait with its owner
        void Initialize(Being owner, BodyHealth health) { }
        void Initialize(IEntity owner) { }

        // Process trait behavior (called every frame/tick)
        void Process(double delta);

        string? InitialDialogue(Being speaker)
        {
            return null;
        }

        bool RefusesCommand(EntityCommand command);
        bool IsOptionAvailable(DialogueOption option);
        string? GetSuccessResponse(EntityCommand command);
        string? GetFailureResponse(EntityCommand command);
        string? GetSuccessResponse(string text);
        string? GetFailureResponse(string text);

        // Optional method for handling events
        void OnEvent(string eventName, params object[] args);

        EntityAction? SuggestAction(Vector2 currentOwnerPosition, Perception currentPerception);
    }

    public class TraitPriorityComparer : IComparer<ITrait>
    {
        public int Compare(ITrait? x, ITrait? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            return x.Priority.CompareTo(y.Priority);
        }
    }
}
