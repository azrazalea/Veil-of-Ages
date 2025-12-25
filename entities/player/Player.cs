using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

public partial class Player : Being
{
    public const uint MAXCOMMANDNUM = 7;

    public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;
    private readonly ReorderableQueue<EntityCommand> _commandQueue = new ();

    public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
    {
        BaseMovementPointsPerTick = 0.5f; // Fast entity (2 ticks per tile)
        base.Initialize(gridArea, startGridPos, attributes);
        Name = "Lilith Galonadel";

        // Example of the new AddTraitToQueue method - much cleaner than creating and adding separately
        SelfAsEntity().AddTraitToQueue<LivingTrait>(0);
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
