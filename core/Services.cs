using System;
using System.Collections.Generic;

namespace VeilOfAges.Core;

/// <summary>
/// Static service locator for accessing global singletons without god-knowledge coupling.
/// Register concrete types directly (e.g., GameController, Player).
/// </summary>
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new ();

    /// <summary>
    /// Registers a concrete instance under its type.
    /// Overwrites any previously registered instance of the same type.
    /// </summary>
    public static void Register<T>(T instance)
        where T : notnull
    {
        _services[typeof(T)] = instance;
    }

    /// <summary>
    /// Returns the registered instance of type T.
    /// Throws <see cref="InvalidOperationException"/> if no instance has been registered.
    /// </summary>
    public static T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        throw new InvalidOperationException(
            $"No service registered for type '{typeof(T).FullName}'. " +
            $"Ensure Register<{typeof(T).Name}>() is called before Get<{typeof(T).Name}>().");
    }

    /// <summary>
    /// Attempts to retrieve the registered instance of type T.
    /// Returns true and outputs the instance if found; returns false and outputs null otherwise.
    /// </summary>
    public static bool TryGet<T>(out T? service)
    {
        if (_services.TryGetValue(typeof(T), out var raw))
        {
            service = (T)raw;
            return true;
        }

        service = default;
        return false;
    }
}
