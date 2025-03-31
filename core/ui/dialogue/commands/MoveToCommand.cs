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
        private Being? _targetEntity = null;
        bool GoalSet = false;

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            if (MyPathfinder == null) return null;

            var targetPosExists = Parameters.TryGetValue("targetPos", out var targetPosObj);
            var targetEntityExists = Parameters.TryGetValue("targetEntity", out var targetEntityObj);

            if (!targetPosExists && !targetEntityExists)
            {
                // Wait for target position to be set
                return new IdleAction(_owner, this, 0);
            }

            // Check if we have a target position
            if (!_targetPos.HasValue && targetPosObj != null)
            {
                _targetPos = (Vector2I)targetPosObj;
            }

            if (_targetEntity == null && targetEntityObj != null)
            {
                _targetEntity = (Being)targetEntityObj;
            }

            // If we have a target position
            if (_targetPos.HasValue)
            {
                Vector2I targetPos = _targetPos.Value;

                if (!GoalSet)
                {
                    MyPathfinder.SetPositionGoal(_owner, targetPos);
                    GoalSet = true;
                }
            }


            if (_targetEntity != null)
            {

                if (!GoalSet)
                {
                    MyPathfinder.SetEntityProximityGoal(_owner, _targetEntity);
                    GoalSet = true;
                }
            }

            // Check if we've reached the target or are in a broken state
            if ((!_targetPos.HasValue && _targetEntity == null) || MyPathfinder.IsGoalReached(_owner)) return null;

            return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
        }
    }
}
