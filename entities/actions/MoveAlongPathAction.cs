using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Actions
{
    public class MoveAlongPathAction(Being entity, object source, Action<EntityAction>? onSuccessful = null, Action<EntityAction>? onSelected = null, int priority = 0) : EntityAction(entity, source, onSelected, onSuccessful, priority)
    {
        public override bool Execute()
        {
            return Entity.MoveAlongPath();
        }
    }
}
