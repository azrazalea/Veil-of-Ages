using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Logging utility that prefixes messages with the current game tick.
/// Supports both console output and file-based entity debug logging.
/// </summary>
public static class Log
{
    private static readonly Dictionary<string, StreamWriter> _debugWriters = new ();
    private static readonly Dictionary<string, ulong> _lastLogTicks = new ();
    private static string? _logDirectory;
    private static bool _debugInitialized;

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

    /// <summary>
    /// Log a debug message for a specific entity to both console and file.
    /// Only call this for entities with debug enabled.
    /// Messages are rate-limited per entity+category combination.
    /// </summary>
    /// <param name="entityName">Name of the entity (used for filename).</param>
    /// <param name="category">Category of the message (e.g., "NEEDS", "ACTIVITY", "STORAGE").</param>
    /// <param name="message">The message to log.</param>
    /// <param name="tickInterval">Minimum ticks between logs for this entity+category (0 = no limit).</param>
    public static void EntityDebug(string entityName, string category, string message, int tickInterval = 100)
    {
        // Rate limiting check
        if (tickInterval > 0)
        {
            var key = $"{entityName}:{category}";
            var currentTick = GameController.CurrentTick;

            if (_lastLogTicks.TryGetValue(key, out var lastTick))
            {
                if (currentTick - lastTick < (ulong)tickInterval)
                {
                    return; // Skip this log, too soon
                }
            }

            _lastLogTicks[key] = currentTick;
        }

        var formattedMessage = $"[{category}] {message}";

        // Print to console with entity identifier
        Print($"[DEBUG:{entityName}] {formattedMessage}");

        // Write to entity's debug file
        WriteToEntityFile(entityName, category, message);
    }

    private static void WriteToEntityFile(string entityName, string category, string message)
    {
        InitializeDebugSystem();

        if (_logDirectory == null)
        {
            return;
        }

        try
        {
            var writer = GetOrCreateWriter(entityName);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            var tick = GameController.CurrentTick;
            var logLine = $"[{timestamp}] [Tick {tick}] [{category}] {message}";

            writer.WriteLine(logLine);
            writer.Flush();
        }
        catch (Exception e)
        {
            GD.PushWarning($"Failed to write debug log for {entityName}: {e.Message}");
        }
    }

    private static void InitializeDebugSystem()
    {
        if (_debugInitialized)
        {
            return;
        }

        try
        {
            _logDirectory = ProjectSettings.GlobalizePath("user://logs/entities");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Clear old log files on startup
            foreach (var file in Directory.GetFiles(_logDirectory, "*.log"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            _debugInitialized = true;
            Print($"Entity debug logging initialized at {_logDirectory}");
        }
        catch (Exception e)
        {
            GD.PushError($"Failed to initialize debug logging: {e.Message}");
            _logDirectory = null;
        }
    }

    private static StreamWriter GetOrCreateWriter(string entityName)
    {
        var safeName = SanitizeFileName(entityName);

        if (!_debugWriters.TryGetValue(safeName, out var writer))
        {
            var filePath = Path.Combine(_logDirectory!, $"{safeName}.log");
            writer = new StreamWriter(filePath, append: true);
            _debugWriters[safeName] = writer;

            writer.WriteLine($"=== Debug Log for {entityName} ===");
            writer.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine(new string('=', 50));
            writer.WriteLine();
        }

        return writer;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    /// <summary>
    /// Close all debug log files. Call on game exit.
    /// </summary>
    public static void Shutdown()
    {
        foreach (var writer in _debugWriters.Values)
        {
            try
            {
                writer.WriteLine();
                writer.WriteLine($"=== Log ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                writer.Close();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _debugWriters.Clear();
        _lastLogTicks.Clear();
        _debugInitialized = false;
    }
}
