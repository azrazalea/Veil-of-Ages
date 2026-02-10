namespace VeilOfAges.Entities.Actions;

/// <summary>
/// Ultra-high priority action that removes all autonomy-managed traits,
/// cancels the current activity, and re-applies the autonomy configuration.
/// Runs on the main thread so trait removal is safe.
/// </summary>
public class ReapplyAutonomyAction : EntityAction
{
    public ReapplyAutonomyAction(Being entity, object source)
        : base(entity, source, priority: -100)
    {
    }

    public override bool Execute()
    {
        if (Entity is Player player)
        {
            player.AutonomyConfig.Reapply(player);
            return true;
        }

        return false;
    }
}
