using System.Text.Json;
using System.Text.Json.Serialization;
using VeilOfAges.Entities;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Provides cached JsonSerializerOptions instances for consistent JSON serialization.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Default options with case-insensitive property names and string enum support.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new ()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Options with Vector2I converter for Godot types.
    /// </summary>
    public static readonly JsonSerializerOptions WithVector2I = new ()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new Vector2IConverter() }
    };

    /// <summary>
    /// Options with Vector2I and Rect2I converters for Godot types.
    /// </summary>
    public static readonly JsonSerializerOptions WithGodotTypes = new ()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new Vector2IConverter(), new Rect2IJsonConverter() }
    };

    /// <summary>
    /// Options for writing indented JSON output with Vector2I support.
    /// </summary>
    public static readonly JsonSerializerOptions WriteIndented = new ()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new Vector2IConverter() }
    };
}
