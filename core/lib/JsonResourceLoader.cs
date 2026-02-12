using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Utility class for loading JSON resource files from the project.
/// Provides methods for loading single files and bulk loading from directories.
/// </summary>
public static class JsonResourceLoader
{
    /// <summary>
    /// Load a single JSON file and deserialize it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="absolutePath">The absolute path to the JSON file.</param>
    /// <param name="options">Optional JsonSerializerOptions. Defaults to JsonOptions.Default.</param>
    /// <returns>The deserialized object, or null if loading fails.</returns>
    public static T? Load<T>(string absolutePath, JsonSerializerOptions? options = null)
        where T : class
    {
        options ??= JsonOptions.Default;

        try
        {
            if (!File.Exists(absolutePath))
            {
                Log.Error($"JsonResourceLoader: File not found: {absolutePath}");
                return null;
            }

            string jsonContent = File.ReadAllText(absolutePath);
            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }
        catch (JsonException e)
        {
            Log.Error($"JsonResourceLoader: JSON parse error in {absolutePath}: {e.Message}");
            return null;
        }
        catch (Exception e)
        {
            Log.Error($"JsonResourceLoader: Error loading {absolutePath}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the base directory for external resource files.
    /// In the editor, this is the project root. In exported builds, this is the
    /// directory containing the executable (or the directory containing the .app on macOS).
    /// </summary>
    public static string GetGameBaseDirectory()
    {
        if (OS.HasFeature("editor"))
        {
            return ProjectSettings.GlobalizePath("res://");
        }

        string exePath = OS.GetExecutablePath();
        string? exeDir = Path.GetDirectoryName(exePath);

        // macOS .app bundle: executable is at Foo.app/Contents/MacOS/Foo
        // We need the directory containing the .app bundle
        if (exeDir != null && exeDir.Contains(".app/Contents/MacOS"))
        {
            // Walk up from Contents/MacOS to the directory containing the .app
            var appDir = new DirectoryInfo(exeDir);
            while (appDir != null && !appDir.Name.EndsWith(".app", StringComparison.Ordinal))
            {
                appDir = appDir.Parent;
            }

            if (appDir?.Parent != null)
            {
                return appDir.Parent.FullName;
            }
        }

        return exeDir ?? ProjectSettings.GlobalizePath("res://");
    }

    /// <summary>
    /// Resolves a res:// path to a filesystem path, using the game base directory
    /// for exported builds so external resource files (for modding) are found
    /// next to the executable rather than inside a .pck or .app bundle.
    /// </summary>
    private static string ResolveResPath(string resPath)
    {
        string relativePath = resPath.Replace("res://", string.Empty);
        return Path.Combine(GetGameBaseDirectory(), relativePath);
    }

    /// <summary>
    /// Load all JSON files from a res:// directory and return a dictionary keyed by ID.
    /// </summary>
    /// <typeparam name="T">The type to deserialize each file to.</typeparam>
    /// <param name="resPath">The res:// path to the directory (e.g., "res://resources/items").</param>
    /// <param name="getId">Function to extract the ID from each loaded object. Return null to skip the item.</param>
    /// <param name="validate">Optional validation function. Items that fail validation are skipped.</param>
    /// <param name="options">Optional JsonSerializerOptions. Defaults to JsonOptions.Default.</param>
    /// <returns>Dictionary mapping IDs to loaded objects. Empty dictionary if directory not found.</returns>
    public static Dictionary<string, T> LoadAllFromDirectory<T>(
        string resPath,
        Func<T, string?> getId,
        Func<T, bool>? validate = null,
        JsonSerializerOptions? options = null)
        where T : class
    {
        var result = new Dictionary<string, T>();
        options ??= JsonOptions.Default;

        string projectPath = ResolveResPath(resPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Error($"JsonResourceLoader: Directory not found: {projectPath}");
            return result;
        }

        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            try
            {
                string jsonContent = File.ReadAllText(file);
                var item = JsonSerializer.Deserialize<T>(jsonContent, options);

                if (item == null)
                {
                    Log.Error($"JsonResourceLoader: Failed to deserialize: {file}");
                    continue;
                }

                // Get the ID
                string? id = getId(item);
                if (string.IsNullOrEmpty(id))
                {
                    Log.Error($"JsonResourceLoader: Item has null or empty ID: {file}");
                    continue;
                }

                // Validate if a validator was provided
                if (validate != null && !validate(item))
                {
                    Log.Error($"JsonResourceLoader: Validation failed for: {file}");
                    continue;
                }

                result[id] = item;
                Log.Print($"JsonResourceLoader: Loaded {typeof(T).Name}: {id}");
            }
            catch (JsonException e)
            {
                Log.Error($"JsonResourceLoader: JSON parse error in {file}: {e.Message}");
            }
            catch (Exception e)
            {
                Log.Error($"JsonResourceLoader: Error loading {file}: {e.Message}");
            }
        }

        Log.Print($"JsonResourceLoader: Loaded {result.Count} {typeof(T).Name} items from {resPath}");
        return result;
    }
}
