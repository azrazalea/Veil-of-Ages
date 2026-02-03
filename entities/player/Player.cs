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
/// </summary>
public partial class Player : GenericBeing
{
    public const uint MAXCOMMANDNUM = 7;

    private readonly ReorderableQueue<EntityCommand> _commandQueue = new ();

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// Constructor sets the definition ID so GenericBeing loads from player.json.
    /// </summary>
    public Player()
    {
        DefinitionId = "player";
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
