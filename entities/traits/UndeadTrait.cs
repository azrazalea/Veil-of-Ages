using Godot;
using System;
using NecromancerKingdom.Entities;

namespace NecromancerKingdom.Entities.Traits
{
    public class UndeadTrait : ITrait
    {
        protected Being _owner;

        public virtual void Initialize(Being owner)
        {
            _owner = owner;
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
    }
}
