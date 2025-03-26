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
            Vector2I commanderPos = new(0, 0);

            foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
            {
                if (entity == _commander)
                {
                    commanderVisible = true;
                    commanderPos = position;
                    break;
                }
            }

            // Check if commander is visible
            if (commanderVisible)
            {
                // Check if we're already close enough
                int distance = Math.Max(
                    Math.Abs(commanderPos.X - currentGridPos.X),
                    Math.Abs(commanderPos.Y - currentGridPos.Y)
                );

                if (distance <= 1)
                {
                    // Clear path and wait
                    MyPathfinder.ClearPath();
                    return new IdleAction(_owner, this, -1);
                }

                // Update path if needed
                if (MyPathfinder.IsPathComplete() || _updateTicks >= _pathUpdateFrequency)
                {
                    // Use PathFinder to get a proper path
                    var gridArea = _owner.GetGridArea();
                    if (gridArea != null)
                    {
                        MyPathfinder.SetPath(gridArea, currentGridPos, commanderPos);
                        _updateTicks = 0;
                    }
                }
            }
            else if (_updateTicks >= _pathUpdateFrequency * 3)
            {
                // If commander hasn't been seen for a while, command is complete
                return null;
            }

            // If we have a path, follow it
            if (MyPathfinder.CurrentPath.Count > 0 && MyPathfinder.PathIndex < MyPathfinder.CurrentPath.Count)
            {
                return new MoveAlongPathAction(_owner, this, priority: -1);
            }

            // Default to idle
            return new IdleAction(_owner, this, -1);
        }
    }
}
