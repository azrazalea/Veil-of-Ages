using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities
{
    // Base interface for all traits
    public interface ITrait : IComparable
    {
        public bool IsInitialized { get; }
        public int Priority { get; set; }
        public PathFinder? MyPathfinder { get; set; }

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
        List<DialogueOption> GenerateDialogueOptions(Being speaker);
        string? GenerateDialogueDescription();

        // Optional method for handling events
        void OnEvent(string eventName, params object[] args);

        EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception);

        public int GeneralCompareTo(object? obj)
        {
            if (obj is ITrait otherTrait)
            {
                return Priority.CompareTo(otherTrait?.Priority ?? -1);
            }

            return -1;
        }
    }
}
