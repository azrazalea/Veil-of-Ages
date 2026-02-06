using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that provides home building reference for entities.
/// Centralizes home storage so other traits can query it at runtime.
/// Home is typically set during entity spawning via SetHome().
/// </summary>
public class HomeTrait : BeingTrait
{
    private Building? _home;

    /// <summary>
    /// Gets the entity's home building.
    /// </summary>
    public Building? Home => _home;

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
        _home?.AddResident(_owner!);

        IsInitialized = true;
    }

    /// <summary>
    /// Configures the trait from JSON parameters.
    /// Home is typically set via SetHome() during spawning, not via JSON.
    /// </summary>
    public override void Configure(TraitConfiguration config)
    {
        var home = config.GetBuilding("home");
        if (home != null)
        {
            SetHome(home);
        }
    }

    /// <summary>
    /// Sets the entity's home building.
    /// Called during entity spawning when assigning homes.
    /// </summary>
    public void SetHome(Building home)
    {
        _home = home;
        Log.Print($"{_owner?.Name}: Home set to {home.BuildingName}");

        // Register as a resident of the home.
        // Only call AddResident if _owner is already set (i.e., Initialize has run).
        // If called from Configure() before Initialize(), the registration is
        // deferred to Initialize() where _owner becomes available.
        if (_owner != null)
        {
            home.AddResident(_owner);
        }
    }
}
