using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands
{
    public class FollowCommand(Being owner, Being commander, bool isComplex = false)
    : EntityCommand(owner, commander, isComplex)
    {
        private int _updateTicks = 0;
        private int _pathUpdateFrequency = 10; // Update path every 10 ticks

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            if (MyPathfinder == null) return null;

            _updateTicks++;

            // Try to find the commander in perception
            bool commanderVisible = false;

            foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
            {
                if (entity == _commander)
                {
                    commanderVisible = true;
                    break;
                }
            }

            // Check if commander is visible
            if (commanderVisible)
            {
                // Set the goal but don't calculate the path yet
                MyPathfinder.SetEntityProximityGoal(_owner, _commander, 1);

                // Pass the pathfinder to the action
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
            }
            else if (_updateTicks >= _pathUpdateFrequency * 3)
            {
                // If commander hasn't been seen for a while, command is complete
                return null;
            }

            // Default to idle
            return new IdleAction(_owner, this, -1);
        }
    }
}
