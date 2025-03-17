using Godot;
using System;
using NecromancerKingdom.Entities;
using NecromancerKingdom.Entities.Beings.Health;
using NecromancerKingdom.Entities.Actions;

namespace NecromancerKingdom.Entities.Traits
{
    public class UndeadTrait : ITrait
    {
        protected Being _owner;

        public virtual void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;
            health.DisableBodySystem(BodySystemType.Pain);
            DisableLivingBodySystems(health);
            GD.Print($"{_owner.Name}: Undead trait initialized");
        }

        public virtual void Process(double delta)
        {
            // Most undead behaviors are passive and don't need per-frame processing
            // This method is here for specialized undead that might need ongoing behaviors
        }

        public virtual void OnEvent(string eventName, params object[] args)
        {
        }
        public EntityAction SuggestAction()
        {
            return new IdleAction(_owner);
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
    }
}
