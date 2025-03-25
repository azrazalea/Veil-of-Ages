using System;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands
{
    /// <summary>
    /// This command exists to start dialogue with a Being (owner). The reason this is a command is so that
    /// the Entity can refuse to talk if they have need to do so. It doesn't actually do anything.
    /// </summary>
    /// <param name="owner">The entity being talked to.</param>
    /// <param name="commander">The entity requesting dialogue.</param>
    /// <param name="isComplex">It is not complex.</param>
    public class TalkCommand(Being owner, Being commander, bool isComplex = false)
    : EntityCommand(owner, commander, isComplex)
    {
        public const int Priority = -1;

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            return null;
        }
    }
}
