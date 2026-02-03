using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

namespace VeilOfAges.Entities.Actions;

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
        // Path calculation happens in Think() via CalculatePathIfNeeded()
        // Execute() only follows the pre-calculated path - no A* here
        return _pathFinder.FollowPath(Entity!);
    }
}
