using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings.Health;

/// <summary>
/// JSON-serializable body structure definition.
/// </summary>
public class BodyStructureDefinition : IResourceDefinition
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets body part groups (e.g., "Head", "Torso", "LeftArm").
    /// </summary>
    public Dictionary<string, BodyPartGroupDefinition>? Groups { get; set; }

    /// <summary>
    /// Gets or sets body systems (e.g., "Consciousness", "BloodPumping").
    /// </summary>
    public Dictionary<string, BodySystemDefinition>? Systems { get; set; }

    public static BodyStructureDefinition LoadFromJson(string path)
    {
        string json = File.ReadAllText(path);
        var definition = JsonSerializer.Deserialize<BodyStructureDefinition>(json, JsonOptions.Default) ?? throw new System.InvalidOperationException($"Failed to deserialize body structure from: {path}");

        return definition;
    }

    public bool Validate()
    {
        try
        {
            ValidateStrict();
            return true;
        }
        catch (System.InvalidOperationException e)
        {
            Log.Error(e.Message);
            return false;
        }
    }

    public void ValidateStrict()
    {
        if (string.IsNullOrEmpty(Id))
        {
            throw new System.InvalidOperationException("BodyStructureDefinition: Missing Id");
        }

        if (Groups == null || Groups.Count == 0)
        {
            throw new System.InvalidOperationException($"BodyStructureDefinition '{Id}': No body groups defined");
        }

        if (Systems == null || Systems.Count == 0)
        {
            throw new System.InvalidOperationException($"BodyStructureDefinition '{Id}': No body systems defined");
        }

        // Validate each group has parts
        foreach (var (groupName, group) in Groups)
        {
            if (group.Parts == null || group.Parts.Count == 0)
            {
                throw new System.InvalidOperationException($"BodyStructureDefinition '{Id}': Group '{groupName}' has no parts defined");
            }

            // Validate each part has a name
            foreach (var part in group.Parts)
            {
                if (string.IsNullOrEmpty(part.Name))
                {
                    throw new System.InvalidOperationException($"BodyStructureDefinition '{Id}': Group '{groupName}' contains a part with no name");
                }
            }
        }

        // Validate each system
        foreach (var (systemName, system) in Systems)
        {
            // Pain system is special - it doesn't need contributors
            if (systemName == "Pain")
            {
                continue;
            }

            if (system.Contributors == null || system.Contributors.Count == 0)
            {
                throw new System.InvalidOperationException($"BodyStructureDefinition '{Id}': System '{systemName}' has no contributors defined");
            }
        }
    }
}

public class BodyPartGroupDefinition
{
    public List<BodyPartDefinition>? Parts { get; set; }
}

public class BodyPartDefinition
{
    public string? Name { get; set; }
    public int MaxHealth { get; set; } = 50;
    public float Importance { get; set; } = 0.5f;
    public float PainSensitivity { get; set; } = 0.5f;
    public List<string>? Flags { get; set; } // "vital", "organ", "bone", "sensory"

    public bool HasFlag(string flag) => Flags?.Contains(flag) ?? false;
}

public class BodySystemDefinition
{
    /// <summary>
    /// Gets or sets body parts that contribute to this system, with their weights.
    /// </summary>
    public Dictionary<string, float>? Contributors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether if true, system failure is fatal.
    /// </summary>
    public bool Fatal { get; set; }

    /// <summary>
    /// Gets or sets minimum efficiency before system fails (0.0-1.0).
    /// </summary>
    public float MinEfficiency { get; set; }
}
