using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that provides home room reference for entities.
/// Centralizes home storage so other traits can query it at runtime.
/// Home is typically set during entity spawning via SetHome().
///
/// Stores a Room reference internally.
/// </summary>
public class HomeTrait : BeingTrait
{
    private Room? _home;

    /// <summary>
    /// Gets the entity's home room.
    /// </summary>
    public Room? Home => _home;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// </summary>
    public HomeTrait()
    {
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Register as resident now that _owner is available.
        // Configure() may have set _home before _owner was set,
        // so we need to do the AddResident call here.
        if (_home != null && _owner != null)
        {
            _home.AddResident(_owner);
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Configures the trait from JSON parameters.
    /// Home is typically set via SetHome() during spawning, not via JSON.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        var home = config.GetRoom("home");
        if (home != null)
        {
            SetHome(home);
        }
    }

    /// <summary>
    /// Check if the entity is currently inside their home room.
    /// Returns false if owner or home is null.
    /// </summary>
    public bool IsEntityAtHome()
    {
        if (_owner == null || _home == null)
        {
            return false;
        }

        return _home.ContainsAbsolutePosition(_owner.GetCurrentGridPosition());
    }

    /// <summary>
    /// Sets the entity's home room directly.
    /// Called when assigning a specific room as home.
    /// </summary>
    public void SetHome(Room room)
    {
        _home = room;
        Log.Print($"{_owner?.Name}: Home set to room '{room.Name}'");

        // Register as a resident of the home room directly.
        // Only call AddResident if _owner is already set (i.e., Initialize has run).
        // If called from Configure() before Initialize(), the registration is
        // deferred to Initialize() where _owner becomes available.
        if (_owner != null)
        {
            room.AddResident(_owner);
        }
    }
}
