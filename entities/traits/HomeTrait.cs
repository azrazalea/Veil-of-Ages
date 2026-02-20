using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that provides home room/building reference for entities.
/// Centralizes home storage so other traits can query it at runtime.
/// Home is typically set during entity spawning via SetHome().
///
/// Stores a Room reference internally. For callers that need the Building,
/// use the HomeBuilding convenience property.
/// </summary>
public class HomeTrait : BeingTrait
{
    private Room? _home;

    /// <summary>
    /// Gets the entity's home room.
    /// </summary>
    public Room? Home => _home;

    /// <summary>
    /// Gets the entity's home building (convenience for callers that need the Building).
    /// Returns the building that owns the home room, or null if no home is set.
    /// </summary>
    public Building? HomeBuilding => _home?.Owner;

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
        // Building.AddResident handles both building-level and room-level tracking.
        if (_home != null && _owner != null)
        {
            _home.Owner.AddResident(_owner);
        }

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
        Log.Print($"{_owner?.Name}: Home set to room '{room.Name}' in {room.Owner.BuildingName}");

        // Register as a resident of the home building.
        // Building.AddResident handles both building-level and room-level tracking.
        // Only call AddResident if _owner is already set (i.e., Initialize has run).
        // If called from Configure() before Initialize(), the registration is
        // deferred to Initialize() where _owner becomes available.
        if (_owner != null)
        {
            room.Owner.AddResident(_owner);
        }
    }

    /// <summary>
    /// Sets the entity's home building by resolving to its default room.
    /// Backward-compatible overload for callers that pass a Building.
    /// Logs a warning if the building has no rooms (defensive, shouldn't happen).
    /// </summary>
    public void SetHome(Building building)
    {
        var defaultRoom = building.GetDefaultRoom();
        if (defaultRoom == null)
        {
            Log.Warn($"{_owner?.Name}: Cannot set home to {building.BuildingName} — building has no rooms");
            return;
        }

        SetHome(defaultRoom);
    }
}
