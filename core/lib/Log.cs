using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Logging utility that prefixes messages with the current game tick.
/// Supports both console output and file-based logging.
/// All log messages go to both the Godot console AND a log file.
/// </summary>
public static class Log
{
    private static readonly Dictionary<string, StreamWriter> _debugWriters = new ();
    private static readonly Dictionary<string, ulong> _lastLogTicks = new ();
    private static string? _logDirectory;
    private static bool _debugInitialized;

    // Main game log file
    private static StreamWriter? _mainLogWriter;
    private static string? _mainLogPath;
    private static readonly object _mainLogLock = new ();

    /// <summary>
    /// Gets the path to the main log file.
    /// </summary>
    public static string? MainLogPath => _mainLogPath;

    /// <summary>
    /// Print a message prefixed with the current tick.
    /// Outputs to both Godot console and log file.
    /// </summary>
    public static void Print(string message)
    {
        var formatted = $"[{GameController.CurrentTick}] {message}";
        GD.Print(formatted);
        WriteToMainLog("INFO", formatted);
    }

    /// <summary>
    /// Print an error message prefixed with the current tick.
    /// Outputs to both Godot console and log file.
    /// </summary>
    public static void Error(string message)
    {
        var formatted = $"[{GameController.CurrentTick}] {message}";
        GD.PushError(formatted);
        WriteToMainLog("ERROR", formatted);
    }

    /// <summary>
    /// Print a warning message prefixed with the current tick.
    /// Outputs to both Godot console and log file.
    /// </summary>
    public static void Warn(string message)
    {
        var formatted = $"[{GameController.CurrentTick}] {message}";
        GD.PushWarning(formatted);
        WriteToMainLog("WARN", formatted);
    }

    /// <summary>
    /// Print a rich text message prefixed with the current tick.
    /// Supports BBCode formatting. Outputs to both Godot console and log file.
    /// </summary>
    public static void PrintRich(string message)
    {
        var formatted = $"[{GameController.CurrentTick}] {message}";
        GD.PrintRich(formatted);

        // Strip BBCode for file output
        WriteToMainLog("INFO", formatted);
    }

    private static void WriteToMainLog(string level, string message)
    {
        InitializeMainLog();

        if (_mainLogWriter == null)
        {
            return;
        }

        lock (_mainLogLock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                _mainLogWriter.WriteLine($"[{timestamp}] [{level}] {message}");
                _mainLogWriter.Flush();
            }
            catch
            {
                // Ignore write errors to prevent infinite loops
            }
        }
    }

    private static void InitializeMainLog()
    {
        if (_mainLogWriter != null)
        {
            return;
        }

        lock (_mainLogLock)
        {
            if (_mainLogWriter != null)
            {
                return;
            }

            try
            {
                var logDir = ProjectSettings.GlobalizePath("user://logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                _mainLogPath = Path.Combine(logDir, "game.log");

                // Truncate log file on startup
                _mainLogWriter = new StreamWriter(_mainLogPath, append: false);
                _mainLogWriter.WriteLine($"=== Veil of Ages Log ===");
                _mainLogWriter.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _mainLogWriter.WriteLine(new string('=', 50));
                _mainLogWriter.WriteLine();
                _mainLogWriter.Flush();
            }
            catch (Exception e)
            {
                GD.PushError($"Failed to initialize main log file: {e.Message}");
            }
        }
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
    /// Close all log files. Call on game exit.
    /// </summary>
    public static void Shutdown()
    {
        lock (_mainLogLock)
        {
            CloseWriter(_mainLogWriter);
            _mainLogWriter = null;
        }

        foreach (var writer in _debugWriters.Values)
        {
            CloseWriter(writer);
        }

        _debugWriters.Clear();
        _lastLogTicks.Clear();
        _debugInitialized = false;
    }

    private static void CloseWriter(StreamWriter? writer)
    {
        if (writer == null)
        {
            return;
        }

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
}
