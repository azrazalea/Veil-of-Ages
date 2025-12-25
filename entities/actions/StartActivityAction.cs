using VeilOfAges.Entities.Activities;

namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Action that starts an activity on an entity.
/// When executed, sets the entity's current activity and initializes it.
/// The activity's first action will be returned on the next tick.
/// </summary>
public class StartActivityAction : EntityAction
{
    private readonly Activity _activity;

    public StartActivityAction(Being entity, object source, Activity activity, int priority = 0)
        : base(entity, source, priority: priority)
    {
        _activity = activity;
    }

    public override bool Execute()
    {
        Entity.SetCurrentActivity(_activity);
        return true;
    }
}
