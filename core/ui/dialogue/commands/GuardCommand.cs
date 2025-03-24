using System;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands
{
    public class GuardCommand(Being owner, Being commander, bool isComplex = false)
    : EntityCommand(owner, commander, isComplex)
    {
        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            return null;
        }
    }
}
