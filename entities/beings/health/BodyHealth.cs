using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings.Health;

public class BodyHealth(Being owner)
{
    public Dictionary<string, BodyPartGroup> BodyPartGroups { get; private set; } = [];
    public Dictionary<BodySystemType, BodySystem> BodySystems { get; private set; } = [];

    public bool BodyStructureInitialized;

    /// <summary>
    /// Gets body parts that are considered soft tissues and organs.
    /// Parts with the "organ" flag in the JSON definition are automatically added to this list.
    /// </summary>
    public List<string> SoftTissuesAndOrgans { get; private set; } = [];

    private readonly Being _owner = owner;

    public void AddBodyPartToGroup(string groupName, BodyPart bodyPart)
    {
        if (BodyPartGroups.TryGetValue(groupName, out BodyPartGroup? value))
        {
            value.AddPart(bodyPart);
        }
        else
        {
            Log.Warn($"Failed to add body part {bodyPart.Name} to group {groupName} as group did not exist");
        }
    }

    public void RemoveBodyPartFromGroup(string groupName, string bodyPartName)
    {
        if (BodyPartGroups.TryGetValue(groupName, out BodyPartGroup? value))
        {
            value.RemovePart(bodyPartName);
        }
        else
        {
            Log.Warn($"Failed to remove body part {bodyPartName} from group {groupName} as group did not exist");
        }
    }

    public void RemoveSoftTissuesAndOrgans()
    {
        foreach (var group in BodyPartGroups.Values)
        {
            group.Parts.RemoveAll(p => SoftTissuesAndOrgans.Contains(p.Name));
        }
    }

    public void DisableBodySystem(BodySystemType systemType)
    {
        if (BodySystems.TryGetValue(systemType, out var system))
        {
            system.Disable();
        }
        else
        {
            Log.Warn($"Failed to disable body system {systemType}");
        }
    }

    public float GetSystemEfficiency(BodySystemType systemType)
    {
        if (systemType == BodySystemType.Pain)
        {
            return CalculatePain();
        }

        if (!BodySystems.TryGetValue(systemType, out var system))
        {
            return 0.0f;
        }

        if (system.Disabled)
        {
            return 1.0f;
        }

        float totalEfficiency = 0.0f;
        float totalWeight = 0.0f;

        foreach (var contributor in system.GetContributors())
        {
            string partName = contributor.Key;
            float weight = contributor.Value;

            // Find the part in body part groups
            BodyPart? part = FindBodyPart(partName);

            if (part != null && part.Status != BodyPartStatus.Destroyed && part.Status != BodyPartStatus.Missing)
            {
                totalEfficiency += part.GetEfficiency() * weight;
                totalWeight += weight;
            }
        }

        // Return efficiency as a percentage where 1.0 = 100%
        return totalWeight > 0 ? totalEfficiency / totalWeight : 0.0f;
    }

    private float CalculatePain()
    {
        if (BodySystems[BodySystemType.Pain].Disabled)
        {
            return 0.0f;
        }

        float totalPain = 0.0f;
        float maxPossiblePain = 0.0f;

        foreach (var group in BodyPartGroups.Values)
        {
            foreach (var part in group.Parts)
            {
                // Pain increases as health decreases, multiplied by pain sensitivity
                float healthLoss = 1.0f - (part.CurrentHealth / part.MaxHealth);
                totalPain += healthLoss * part.PainSensitivity * part.Importance;
                maxPossiblePain += part.PainSensitivity * part.Importance;
            }
        }

        // Cap pain at 1.0 (100%)
        return Math.Min(1.0f, totalPain / maxPossiblePain);
    }

    // Helper method to find a body part by name
    private BodyPart? FindBodyPart(string partName)
    {
        foreach (var group in BodyPartGroups.Values)
        {
            foreach (var part in group.Parts)
            {
                if (part.Name == partName)
                {
                    return part;
                }
            }
        }

        return null;
    }

    // Get a description of a system's status
    public string GetSystemStatus(BodySystemType systemType)
    {
        float efficiency = GetSystemEfficiency(systemType);

        if (BodySystems[systemType].Disabled)
        {
            return "Disabled";
        }

        if (systemType == BodySystemType.Pain)
        {
            if (efficiency >= 0.8f)
            {
                return "Extreme pain";
            }

            if (efficiency >= 0.6f)
            {
                return "Severe pain";
            }

            if (efficiency >= 0.4f)
            {
                return "Significant pain";
            }

            if (efficiency >= 0.2f)
            {
                return "Moderate pain";
            }

            if (efficiency > 0.0f)
            {
                return "Minor pain";
            }

            return "No pain";
        }

        if (efficiency <= 0.0f)
        {
            return "None";
        }

        if (efficiency < 0.25f)
        {
            return "Extremely poor";
        }

        if (efficiency < 0.5f)
        {
            return "Poor";
        }

        if (efficiency < 0.75f)
        {
            return "Weakened";
        }

        if (efficiency < 1.0f)
        {
            return "Slightly impaired";
        }

        if (efficiency == 1.0f)
        {
            return "Normal";
        }

        return "Enhanced"; // For values above 100%
    }

    public void PrintSystemStatuses()
    {
        Log.Print($"Health status for {_owner.Name}");
        foreach (var system in BodySystems.Values)
        {
            Log.Print($"{system.Name} => {GetSystemStatus(system.Type)}");
        }
    }

    /// <summary>
    /// Initialize body structure and systems from a JSON definition.
    /// </summary>
    /// <param name="definition">The body structure definition loaded from JSON.</param>
    public void InitializeFromDefinition(BodyStructureDefinition definition)
    {
        // Clear any existing data
        BodyPartGroups.Clear();
        BodySystems.Clear();
        SoftTissuesAndOrgans.Clear();

        // Create body part groups
        if (definition.Groups != null)
        {
            foreach (var (groupName, groupDef) in definition.Groups)
            {
                var group = new BodyPartGroup(groupName);

                if (groupDef.Parts != null)
                {
                    foreach (var partDef in groupDef.Parts)
                    {
                        if (partDef.Name == null)
                        {
                            throw new InvalidOperationException($"Body part in group '{groupName}' has null name");
                        }

                        var part = new BodyPart(
                            partDef.Name,
                            partDef.MaxHealth,
                            partDef.Importance,
                            partDef.PainSensitivity);
                        group.AddPart(part);

                        // Track organs for RemoveSoftTissuesAndOrgans
                        if (partDef.HasFlag("organ") && !SoftTissuesAndOrgans.Contains(partDef.Name))
                        {
                            SoftTissuesAndOrgans.Add(partDef.Name);
                        }
                    }
                }

                BodyPartGroups[groupName] = group;
            }
        }

        // Create body systems
        if (definition.Systems != null)
        {
            foreach (var (systemName, systemDef) in definition.Systems)
            {
                // Parse the system type from the name
                if (!Enum.TryParse<BodySystemType>(systemName, out var systemType))
                {
                    throw new InvalidOperationException($"Unknown body system type: {systemName}");
                }

                var system = new BodySystem(systemType, systemName, systemDef.Fatal);

                if (systemDef.Contributors != null)
                {
                    foreach (var (partName, weight) in systemDef.Contributors)
                    {
                        system.AddContributor(partName, weight);
                    }
                }

                BodySystems[systemType] = system;
            }
        }

        BodyStructureInitialized = true;
    }

    /// <summary>
    /// Initialize the humanoid body structure from JSON definition.
    /// NO FALLBACK: If the definition cannot be loaded, the game will crash.
    /// </summary>
    public void InitializeHumanoidBodyStructure()
    {
        var definition = BodyStructureResourceManager.Instance.GetDefinition("humanoid");
        InitializeFromDefinition(definition);
    }
}
