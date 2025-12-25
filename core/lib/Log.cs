using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Logging utility that prefixes messages with the current game tick.
/// </summary>
public static class Log
{
    /// <summary>
    /// Print a message prefixed with the current tick.
    /// </summary>
    public static void Print(string message)
    {
        GD.Print($"[{GameController.CurrentTick}] {message}");
    }

    /// <summary>
    /// Print an error message prefixed with the current tick.
    /// </summary>
    public static void Error(string message)
    {
        GD.PushError($"[{GameController.CurrentTick}] {message}");
    }

    /// <summary>
    /// Print a warning message prefixed with the current tick.
    /// </summary>
    public static void Warn(string message)
    {
        GD.PushWarning($"[{GameController.CurrentTick}] {message}");
    }

    /// <summary>
    /// Print a rich text message prefixed with the current tick.
    /// Supports BBCode formatting.
    /// </summary>
    public static void PrintRich(string message)
    {
        GD.PrintRich($"[{GameController.CurrentTick}] {message}");
    }
}
