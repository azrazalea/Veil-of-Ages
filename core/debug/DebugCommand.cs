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
