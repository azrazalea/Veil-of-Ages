using System;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands;

public class PatrolCommand(Being owner, Being commander)
: EntityCommand(owner, commander)
{
    public override string DisplayName => L.Tr("command.PATROL");

    public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
    {
        return null;
    }
}
