using System;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands;

public class CancelCommand(Being owner, Being commander, bool isComplex = false)
: EntityCommand(owner, commander, isComplex)
{
    public override string DisplayName => L.Tr("command.CANCEL_ORDERS");

    public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
    {
        return null;
    }
}
