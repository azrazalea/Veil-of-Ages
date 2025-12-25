using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

public class MoveAlongPathAction : EntityAction
{
    private readonly PathFinder _pathFinder;

    public MoveAlongPathAction(Being entity, object source, PathFinder pathFinder, int priority = 1)
        : base(entity, source, priority: priority)
    {
        _pathFinder = pathFinder;
    }

    public override bool Execute()
    {
        // The PathFinder knows the goal and handles calculation on demand
        return _pathFinder.TryFollowPath(Entity);
    }
}
