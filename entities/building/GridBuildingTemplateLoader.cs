using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities;

/// <summary>
/// Loads building templates from the directory-based GridFab format.
/// Each building is a directory containing building.json, palette.json, and .grid files.
/// </summary>
public static class GridBuildingTemplateLoader
{
    private const string BUILDINGJSON = "building.json";
    private const string PALETTEJSON = "palette.json";
    private const string EMPTYALIAS = ".";

    /// <summary>
    /// Load a building template from a GridFab directory.
    /// </summary>
    /// <param name="dirPath">Absolute path to the building directory.</param>
    /// <param name="palettesBasePath">Absolute path to shared palettes directory.</param>
    /// <returns>A BuildingTemplate, or null on failure.</returns>
    public static BuildingTemplate? LoadFromDirectory(string dirPath, string palettesBasePath)
    {
        string dirName = Path.GetFileName(dirPath);

        // Load building.json (metadata)
        string buildingJsonPath = Path.Combine(dirPath, BUILDINGJSON);
        if (!File.Exists(buildingJsonPath))
        {
            Log.Error($"GridBuildingTemplateLoader: Missing {BUILDINGJSON} in {dirName}");
            return null;
        }

        BuildingTemplate? template;
        try
        {
            string json = File.ReadAllText(buildingJsonPath);
            template = JsonSerializer.Deserialize<BuildingTemplate>(json, WithVector2I);
        }
        catch (Exception e)
        {
            Log.Error($"GridBuildingTemplateLoader: Error parsing {BUILDINGJSON} in {dirName}: {e.Message}");
            return null;
        }

        if (template == null)
        {
            Log.Error($"GridBuildingTemplateLoader: Failed to deserialize {BUILDINGJSON} in {dirName}");
            return null;
        }

        // Load and resolve palette
        var palette = LoadPalette(dirPath, palettesBasePath, dirName);
        if (palette == null)
        {
            return null;
        }

        // Parse all .grid files into tiles
        var tiles = new List<BuildingTileData>();
        foreach (var gridFile in Directory.GetFiles(dirPath, "*.grid"))
        {
            var gridTiles = ParseGridFile(gridFile, palette, dirName);
            if (gridTiles == null)
            {
                return null;
            }

            tiles.AddRange(gridTiles);
        }

        if (tiles.Count == 0)
        {
            Log.Error($"GridBuildingTemplateLoader: No tiles parsed from .grid files in {dirName}");
            return null;
        }

        template.Tiles = tiles;
        return template;
    }

    private static Dictionary<string, PaletteEntry>? LoadPalette(
        string dirPath, string palettesBasePath, string dirName)
    {
        string palettePath = Path.Combine(dirPath, PALETTEJSON);
        if (!File.Exists(palettePath))
        {
            Log.Error($"GridBuildingTemplateLoader: Missing {PALETTEJSON} in {dirName}");
            return null;
        }

        PaletteFile? paletteFile;
        try
        {
            string json = File.ReadAllText(palettePath);
            paletteFile = JsonSerializer.Deserialize<PaletteFile>(json, Default);
        }
        catch (Exception e)
        {
            Log.Error($"GridBuildingTemplateLoader: Error parsing {PALETTEJSON} in {dirName}: {e.Message}");
            return null;
        }

        if (paletteFile == null)
        {
            Log.Error($"GridBuildingTemplateLoader: Failed to deserialize {PALETTEJSON} in {dirName}");
            return null;
        }

        // Resolve inheritance chain
        var merged = new Dictionary<string, PaletteEntry>();

        if (paletteFile.Inherits != null)
        {
            foreach (string parentName in paletteFile.Inherits)
            {
                var parentAliases = LoadSharedPalette(parentName, palettesBasePath);
                if (parentAliases == null)
                {
                    Log.Error($"GridBuildingTemplateLoader: Failed to load inherited palette '{parentName}' for {dirName}");
                    return null;
                }

                // Merge parent aliases (later parents override earlier)
                foreach (var kvp in parentAliases)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
        }

        // Per-building aliases override inherited ones
        if (paletteFile.Aliases != null)
        {
            foreach (var kvp in paletteFile.Aliases)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }

    private static Dictionary<string, PaletteEntry>? LoadSharedPalette(
        string paletteName, string palettesBasePath)
    {
        string palettePath = Path.Combine(palettesBasePath, paletteName + ".json");
        if (!File.Exists(palettePath))
        {
            Log.Error($"GridBuildingTemplateLoader: Shared palette not found: {palettePath}");
            return null;
        }

        PaletteFile? paletteFile;
        try
        {
            string json = File.ReadAllText(palettePath);
            paletteFile = JsonSerializer.Deserialize<PaletteFile>(json, Default);
        }
        catch (Exception e)
        {
            Log.Error($"GridBuildingTemplateLoader: Error parsing shared palette '{paletteName}': {e.Message}");
            return null;
        }

        if (paletteFile == null)
        {
            return null;
        }

        // Recursively resolve inherited palettes
        var merged = new Dictionary<string, PaletteEntry>();

        if (paletteFile.Inherits != null)
        {
            foreach (string parentName in paletteFile.Inherits)
            {
                var parentAliases = LoadSharedPalette(parentName, palettesBasePath);
                if (parentAliases == null)
                {
                    Log.Error($"GridBuildingTemplateLoader: Failed to load inherited palette '{parentName}' from shared palette '{paletteName}'");
                    return null;
                }

                foreach (var kvp in parentAliases)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
        }

        if (paletteFile.Aliases != null)
        {
            foreach (var kvp in paletteFile.Aliases)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }

    private static List<BuildingTileData>? ParseGridFile(
        string gridFilePath, Dictionary<string, PaletteEntry> palette, string dirName)
    {
        string fileName = Path.GetFileName(gridFilePath);
        string[] lines;
        try
        {
            lines = File.ReadAllLines(gridFilePath);
        }
        catch (Exception e)
        {
            Log.Error($"GridBuildingTemplateLoader: Error reading {fileName} in {dirName}: {e.Message}");
            return null;
        }

        var tiles = new List<BuildingTileData>();

        for (int row = 0; row < lines.Length; row++)
        {
            string line = lines[row].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string[] aliases = line.Split((char[] ?)null, StringSplitOptions.RemoveEmptyEntries);

            for (int col = 0; col < aliases.Length; col++)
            {
                string alias = aliases[col];

                if (alias == EMPTYALIAS)
                {
                    continue;
                }

                if (!palette.TryGetValue(alias, out var entry))
                {
                    Log.Error($"GridBuildingTemplateLoader: Unknown alias '{alias}' at row {row}, col {col} in {fileName} ({dirName})");
                    return null;
                }

                if (string.Equals(entry.Type, "Empty", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var tileData = new BuildingTileData
                {
                    Position = new Vector2I(col, row),
                    Type = entry.Type,
                    Material = entry.Material,
                    Category = entry.Category,
                    Variant = entry.Variant,
                    Layer = entry.Layer,
                    Tint = entry.Tint
                };

                tiles.Add(tileData);
            }
        }

        return tiles;
    }

    /// <summary>
    /// JSON model for a palette file (both shared and per-building).
    /// </summary>
    private sealed class PaletteFile
    {
        public List<string>? Inherits { get; set; }
        public Dictionary<string, PaletteEntry>? Aliases { get; set; }
    }

    /// <summary>
    /// A single palette alias entry mapping to BuildingTileData fields.
    /// </summary>
    private sealed class PaletteEntry
    {
        public string? Type { get; set; }
        public string? Material { get; set; }
        public string? Category { get; set; }
        public string? Variant { get; set; }
        public string? Layer { get; set; }
        public string? Tint { get; set; }
    }
}
