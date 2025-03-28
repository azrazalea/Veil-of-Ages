using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands
{
    public class MoveToCommand(Being owner, Being commander, bool isComplex = false)
    : EntityCommand(owner, commander, isComplex)
    {
        private Vector2I? _targetPos = null;

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            if (MyPathfinder == null) return null;

            if (!Parameters.TryGetValue("targetPos", out var targetPosObj))
            {
                // Wait for target position to be set
                return new IdleAction(_owner, this, 0);
            }

            // Check if we have a target position
            if (!_targetPos.HasValue && targetPosObj != null)
            {
                _targetPos = (Vector2I)targetPosObj;
            }

            // If we have a target position
            if (_targetPos.HasValue)
            {
                Vector2I targetPos = _targetPos.Value;


                // Check if we've reached the target
                if (currentGridPos == targetPos)
                {
                    return null; // Command complete
                }

                MyPathfinder.SetPositionGoal(_owner, targetPos);
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
            }
            // No target or no path available, end command
            return null;
        }
    }
}
