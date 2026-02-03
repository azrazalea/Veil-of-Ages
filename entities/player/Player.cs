using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

/// <summary>
/// The player-controlled necromancer entity.
/// Extends GenericBeing to load configuration from player.json definition.
/// Adds command queue functionality for player-specific input handling.
/// Features autonomous behavior via PlayerBehaviorTrait when no commands are queued.
/// </summary>
public partial class Player : GenericBeing
{
    public const uint MAXCOMMANDNUM = 7;

    private readonly ReorderableQueue<EntityCommand> _commandQueue = new ();

    /// <summary>
    /// Gets the player's home building from HomeTrait.
    /// </summary>
    public Building? Home => SelfAsEntity().GetTrait<HomeTrait>()?.Home;

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// Constructor sets the definition ID so GenericBeing loads from player.json.
    /// </summary>
    public Player()
    {
        DefinitionId = "player";
    }

    /// <summary>
    /// Sets the player's home building via HomeTrait and notifies ScholarJobTrait.
    /// Called by World after player initialization when assigning the player's house.
    /// </summary>
    /// <param name="home">The building to set as home.</param>
    public void SetHome(Building home)
    {
        // Set home via HomeTrait (single source of truth)
        var homeTrait = SelfAsEntity().GetTrait<HomeTrait>();
        homeTrait?.SetHome(home);

        // Notify ScholarJobTrait (needs to know workplace location)
        var scholarJobTrait = SelfAsEntity().GetTrait<ScholarJobTrait>();
        scholarJobTrait?.SetHome(home);
    }

    public override void Initialize(Area gridArea, Vector2I startGridPos, GameController? gameController = null, BeingAttributes? attributes = null, bool debugEnabled = false)
    {
        base.Initialize(gridArea, startGridPos, gameController, attributes, debugEnabled);
        Name = "Lilith Galonadel";
    }

    public override EntityAction Think(Vector2 currentPosition, ObservationData observationData)
    {
        if (_commandQueue.Count > 0 && _currentCommand == null)
        {
            _currentCommand = _commandQueue.Dequeue();
        }

        return base.Think(currentPosition, observationData);
    }

    public bool QueueCommand(EntityCommand command)
    {
        if (_commandQueue.Count >= MAXCOMMANDNUM)
        {
            return false;
        }

        _commandQueue.Enqueue(command);

        return true;
    }

    public ReorderableQueue<EntityCommand> GetCommandQueue()
    {
        return _commandQueue;
    }

    public bool HasAssignedCommand()
    {
        return _currentCommand != null;
    }

    public EntityCommand? GetAssignedCommand()
    {
        return _currentCommand;
    }
}
