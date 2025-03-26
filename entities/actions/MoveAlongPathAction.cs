using System;

namespace VeilOfAges.Entities.Actions
{
    public class MoveAlongPathAction(Being entity, object source, Action<EntityAction>? onSuccessful = null, Action<EntityAction>? onSelected = null, int priority = 1) : EntityAction(entity, source, onSelected, onSuccessful, priority)
    {
        public override bool Execute()
        {
            return Entity.MoveAlongPath();
        }
    }
}
