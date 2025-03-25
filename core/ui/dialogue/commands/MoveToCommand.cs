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
        private List<Vector2I> _path = [];
        private int _pathIndex = 0;

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            if (!Parameters.TryGetValue("targetPos", out var targetPosObj))
            {
                // Wait for target position to be set
                return new IdleAction(_owner, -1);
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

                // Check if we need to calculate a path
                if (_path.Count == 0 || _pathIndex >= _path.Count)
                {
                    var gridArea = _owner.GetGridArea();
                    if (gridArea != null)
                    {
                        _path = PathFinder.FindPath(gridArea, currentGridPos, targetPos);
                        _pathIndex = 0;

                        // If no path could be found, end command
                        if (_path.Count == 0)
                        {
                            return null;
                        }
                    }
                }

                // Follow the path
                if (_pathIndex < _path.Count)
                {
                    Vector2I nextPos = _path[_pathIndex];
                    _pathIndex++;

                    return new MoveAction(_owner, nextPos, -1);
                }
            }

            // No target or no path available, end command
            return null;
        }
    }
}
