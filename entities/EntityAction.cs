using Godot;
using System;

namespace VeilOfAges.Entities
{
    public abstract class EntityAction(Being entity, object source, int priority = 1)
    {
        public Being Entity { get; private set; } = entity;
        /// <summary>
        /// Lower values are higher priority.
        /// </summary>
        public int Priority { get; private set; } = priority;
        /// <summary>
        /// What class generated this action?
        /// </summary>
        public object Source { get; private set; } = source;

        // Optional callback when the action is actually selected for execution
        public Action<EntityAction>? OnSelected { get; set; }

        public abstract void Execute();
    }
}
