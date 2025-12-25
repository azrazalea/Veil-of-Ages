using System;
using Godot;

namespace VeilOfAges.Entities;

public abstract class EntityAction(Being entity, object source, Action<EntityAction>? onSelected = null, Action<EntityAction>? onSuccessful = null, int priority = 1)
{
    public Being Entity { get; private set; } = entity;

    /// <summary>
    /// Gets lower values are higher priority.
    /// </summary>
    public int Priority { get; private set; } = priority;

    /// <summary>
    /// Gets what class generated this action?.
    /// </summary>
    public object Source { get; private set; } = source;

    /// <summary>
    /// Gets or sets optional callback when the action is actually selected for execution.
    /// </summary>
    public Action<EntityAction>? OnSelected { get; set; } = onSelected;

    /// <summary>
    /// Gets or sets optional callback when the action successfully executes.
    /// </summary>
    public Action<EntityAction>? OnSuccessful { get; set; } = onSuccessful;

    public abstract bool Execute();
}
