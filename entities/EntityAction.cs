using Godot;
using System;

namespace VeilOfAges.Entities
{
    public abstract class EntityAction(Being entity, int priority = 1)
    {
        public Being Entity { get; private set; } = entity;
        public int Priority { get; private set; } = priority;

        public abstract void Execute();
    }
}
