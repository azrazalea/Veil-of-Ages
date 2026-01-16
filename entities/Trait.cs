using System;
using Godot;

namespace VeilOfAges.Entities;

/// <summary>
/// Base class for all traits that can be applied to entities.
/// Provides common functionality and a standard interface for trait behaviors.
/// </summary>
public class Trait : IComparable
{
    // Common properties
    public bool IsInitialized { get; protected set; }
    public int Priority { get; set; }

    // Random number generator for common use
    protected RandomNumberGenerator _rng = new ();

    /// <summary>
    /// Initialize the trait with an entity owner.
    /// </summary>
    public virtual void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }

        _rng.Randomize();
        IsInitialized = true;
    }

    /// <summary>
    /// Process method called every frame/tick.
    /// </summary>
    public virtual void Process(double delta)
    {
    }

    /// <summary>
    /// Event handler for various entity events.
    /// </summary>
    public virtual void OnEvent(string eventName, params object[] args)
    {
    }

    // IComparable implementation for sorting traits by priority
    // Uses GetHashCode as tiebreaker to ensure traits with same priority are still distinct
    public int CompareTo(object? obj)
    {
        if (obj is Trait otherTrait)
        {
            int priorityCompare = Priority.CompareTo(otherTrait.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            // Same priority - use hash code to ensure distinct traits aren't considered equal
            return GetHashCode().CompareTo(otherTrait.GetHashCode());
        }

        return -1;
    }
}
