using Godot;
using System;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;
using System.Collections.Generic;

namespace VeilOfAges.Entities.Traits
{
    public class UndeadTrait : ITrait
    {
        protected Being? _owner;
        public bool IsInitialized { get; protected set; }
        public int Priority { get; set; }


        public virtual void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;
            health.DisableBodySystem(BodySystemType.Pain);
            DisableLivingBodySystems(health);
            GD.Print($"{_owner.Name}: Undead trait initialized");
            IsInitialized = true;
        }

        public virtual void Process(double delta)
        {
            // Most undead behaviors are passive and don't need per-frame processing
            // This method is here for specialized undead that might need ongoing behaviors
        }

        public virtual void OnEvent(string eventName, params object[] args)
        {
        }

        public bool RefusesCommand(EntityCommand command)
        {
            return false;
        }

        public bool IsOptionAvailable(DialogueOption option)
        {
            return true;
        }
        public List<DialogueOption> GenerateDialogueOptions(Being speaker)
        {
            return [];
        }

        public virtual EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;
            return new IdleAction(_owner, this, -1);
        }
        private static void DisableLivingBodySystems(BodyHealth health)
        {
            health.DisableBodySystem(BodySystemType.Breathing);
            health.DisableBodySystem(BodySystemType.BloodPumping);
            health.DisableBodySystem(BodySystemType.BloodFiltration);
            health.DisableBodySystem(BodySystemType.Digestion);
            health.DisableBodySystem(BodySystemType.Sight);
            health.DisableBodySystem(BodySystemType.Hearing);
        }

        public string? GetSuccessResponse(EntityCommand command)
        {
            return null;
        }
        public string? GetFailureResponse(EntityCommand command)
        {
            return null;
        }
        public string? GetSuccessResponse(string text)
        {
            return null;
        }
        public string? GetFailureResponse(string text)
        {
            return null;
        }

        public string? GenerateDialogueDescription()
        {
            return null;
        }

        public int CompareTo(object? obj)
        {
            return (this as ITrait).GeneralCompareTo(obj);
        }
    }
}
