using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Autonomy;
using VeilOfAges.UI;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Core.Debug;

/// <summary>
/// Abstract base class for debug commands that can be queued and executed.
/// </summary>
public abstract class DebugCommand
{
    /// <summary>
    /// Gets human-readable description of what this command does.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Execute the command.
    /// </summary>
    /// <param name="controller">The game controller to execute against.</param>
    /// <returns>True if the command executed successfully, false otherwise.</returns>
    public abstract bool Execute(GameController controller);
}

/// <summary>
/// Command to pause the simulation.
/// </summary>
public class PauseCommand : DebugCommand
{
    public override string Description => "Pause simulation";

    public override bool Execute(GameController controller)
    {
        controller.PauseSimulation();
        return true;
    }
}

/// <summary>
/// Command to resume the simulation.
/// </summary>
public class ResumeCommand : DebugCommand
{
    public override string Description => "Resume simulation";

    public override bool Execute(GameController controller)
    {
        controller.ResumeSimulation();
        return true;
    }
}

/// <summary>
/// Command to step a specified number of ticks while paused.
/// </summary>
public class StepCommand : DebugCommand
{
    /// <summary>
    /// Gets number of ticks to step.
    /// </summary>
    public int Ticks { get; }

    public override string Description => $"Step {Ticks} tick{(Ticks == 1 ? string.Empty : "s")}";

    /// <summary>
    /// Initializes a new instance of the <see cref="StepCommand"/> class.
    /// Create a step command.
    /// </summary>
    /// <param name="ticks">Number of ticks to step (default 1).</param>
    public StepCommand(int ticks = 1)
    {
        Ticks = ticks;
    }

    public override bool Execute(GameController controller)
    {
        // Can only step when simulation is paused
        if (!controller.SimulationPaused())
        {
            return false;
        }

        // Validation passed - actual tick execution is handled by the server
        return true;
    }
}

/// <summary>
/// Command to move the player to a grid position.
/// </summary>
public class PlayerMoveToCommand(int x, int y): DebugCommand
{
    public override string Description => $"Move player to ({x}, {y})";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        var cmd = new MoveToCommand(player, player, isComplex: false);
        cmd.WithParameter("targetPos", new Vector2I(x, y));
        return player.QueueCommand(cmd);
    }
}

/// <summary>
/// Command to make the player follow a named entity.
/// </summary>
public class PlayerFollowCommand(string entityName): DebugCommand
{
    public override string Description => $"Player follow '{entityName}'";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        var target = world!.GetBeings().FirstOrDefault(b =>
            string.Equals(b.Name, entityName, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Log.Warn($"DebugServer: Entity '{entityName}' not found for follow command");
            return false;
        }

        var cmd = new FollowCommand(player, target, isComplex: false);
        return player.QueueCommand(cmd);
    }
}

/// <summary>
/// Command to cancel the player's current command.
/// </summary>
public class PlayerCancelCommand : DebugCommand
{
    public override string Description => "Cancel player command";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        player.AssignCommand(null);
        return true;
    }
}

/// <summary>
/// Command to enable or disable an autonomy rule.
/// </summary>
public class AutonomySetEnabledCommand(string ruleId, bool enabled): DebugCommand
{
    public override string Description => $"{(enabled ? "Enable" : "Disable")} autonomy rule '{ruleId}'";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        if (player.AutonomyConfig.GetRule(ruleId) == null)
        {
            Log.Warn($"DebugServer: Autonomy rule '{ruleId}' not found");
            return false;
        }

        player.AutonomyConfig.SetEnabled(ruleId, enabled);
        player.ReapplyAutonomy();
        return true;
    }
}

/// <summary>
/// Command to change the priority of an autonomy rule.
/// </summary>
public class AutonomyReorderCommand(string ruleId, int priority): DebugCommand
{
    public override string Description => $"Reorder autonomy rule '{ruleId}' to priority {priority}";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        if (player.AutonomyConfig.GetRule(ruleId) == null)
        {
            Log.Warn($"DebugServer: Autonomy rule '{ruleId}' not found");
            return false;
        }

        player.AutonomyConfig.ReorderRule(ruleId, priority);
        player.ReapplyAutonomy();
        return true;
    }
}

/// <summary>
/// Command to add a new autonomy rule.
/// </summary>
public class AutonomyAddRuleCommand(string id, string displayName, string traitType, int priority, DayPhaseType[] ? phases, Dictionary<string, object?>? parameters = null)
    : DebugCommand
{
    public override string Description => $"Add autonomy rule '{id}'";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        if (player.AutonomyConfig.GetRule(id) != null)
        {
            Log.Warn($"DebugServer: Autonomy rule '{id}' already exists");
            return false;
        }

        player.AutonomyConfig.AddRule(new AutonomyRule(id, displayName, traitType, priority, phases, parameters));
        player.ReapplyAutonomy();
        return true;
    }
}

/// <summary>
/// Command to remove an autonomy rule.
/// </summary>
public class AutonomyRemoveRuleCommand(string ruleId): DebugCommand
{
    public override string Description => $"Remove autonomy rule '{ruleId}'";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        if (!player.AutonomyConfig.RemoveRule(ruleId))
        {
            Log.Warn($"DebugServer: Autonomy rule '{ruleId}' not found");
            return false;
        }

        player.ReapplyAutonomy();
        return true;
    }
}

/// <summary>
/// Command to force reapply all autonomy rules.
/// </summary>
public class AutonomyReapplyCommand : DebugCommand
{
    public override string Description => "Reapply all autonomy rules";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null)
        {
            return false;
        }

        player.ReapplyAutonomy();
        return true;
    }
}

/// <summary>
/// Command to use a transition point at the player's current position.
/// Checks if the player is standing on a TransitionPoint and queues a ChangeAreaAction.
/// </summary>
public class PlayerUseTransitionCommand : DebugCommand
{
    public override string Description => "Use transition at player position";

    public override bool Execute(GameController controller)
    {
        var world = controller.GetTree().GetFirstNodeInGroup("World") as World;
        var player = world?.Player;
        if (player == null || player.GridArea == null)
        {
            return false;
        }

        var playerPos = player.GetCurrentGridPosition();
        var transitionPoint = world!.GetTransitionPointAt(player.GridArea, playerPos);

        if (transitionPoint?.LinkedPoint == null)
        {
            Log.Warn($"DebugServer: No transition point at player position {playerPos}");
            return false;
        }

        // Queue a ChangeAreaAction to execute on next tick
        var action = new ChangeAreaAction(player, this, transitionPoint.LinkedPoint, priority: 0);
        action.Execute();
        return true;
    }
}
