using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VeilOfAges.Entities.Beings.Health;

public enum BodySystemType
{
    Pain,            // Special - this is inverse (0% is ideal)
    Consciousness,
    Sight,
    Hearing,
    Smell,           // Added smell system
    Moving,
    Manipulation,
    Talking,
    Communication,
    Breathing,
    BloodFiltration,
    BloodPumping,
    Digestion
}

// Body part status enum
public enum BodyPartStatus
{
    Healthy,
    Injured,
    SeverelyInjured,
    Destroyed,
    Missing
}

public class BodySystem(BodySystemType type, string name, bool isFatalIfLost)
{
    public BodySystemType Type { get; private set; } = type;
    public string Name { get; private set; } = name;
    public bool IsFatalIfLost { get; private set; } = isFatalIfLost;
    public bool Enabled { get; private set; } = true;
    public bool Disabled
    {
        get => !Enabled;
    }

    // Dictionary mapping body parts to their contribution percentage
    private readonly Dictionary<string, float> _contributors = [];

    public void Disable()
    {
        Enabled = false;
    }

    public void AddContributor(string bodyPartName, float contributionWeight)
    {
        _contributors[bodyPartName] = contributionWeight;
    }

    public Dictionary<string, float> GetContributors()
    {
        return new Dictionary<string, float>(_contributors);
    }

    public bool HasContributors()
    {
        return _contributors.Count > 0;
    }
}

// Body part class
public class BodyPart(string name, float maxHealth, float importance = 0.5f, float painSensitivity = 0.5f)
{
    public string Name { get; private set; } = name;
    public float MaxHealth { get; private set; } = maxHealth;
    public float CurrentHealth { get; private set; } = maxHealth;
    public BodyPartStatus Status { get; private set; } = BodyPartStatus.Healthy;
    public float Importance { get; private set; } = importance;
    public float PainSensitivity { get; private set; } = painSensitivity;

    public void TakeDamage(float amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        UpdateStatus();
    }

    public void Heal(float amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        UpdateStatus();
    }

    public float GetEfficiency()
    {
        return CurrentHealth / MaxHealth;
    }

    public void ScaleMaxHealth(float factor)
    {
        float healthPercentage = CurrentHealth / MaxHealth;
        MaxHealth *= factor;
        CurrentHealth = MaxHealth * healthPercentage;
        UpdateStatus();
    }

    public void RemovePart()
    {
        Status = BodyPartStatus.Missing;
    }

    public void DisablePain()
    {
        PainSensitivity = 0.0f;
    }

    public bool IsBonePart()
    {
        // Check if a part name represents a bone
        return Name.Contains("bone") || Name == "Skull" ||
               Name == "Ribs" || Name == "Spine" ||
               Name == "Pelvis" || Name == "Sternum" ||
               Name == "Femurs" || Name == "Tibiae" ||
               Name == "Humeri" || Name == "Radii" ||
               Name == "Jaw" || Name.Contains("Fingers") ||
               Name == "Toes" || Name == "Clavicles";
    }

    private void UpdateStatus()
    {
        float healthPercent = CurrentHealth / MaxHealth;

        if (healthPercent <= 0)
        {
            Status = BodyPartStatus.Destroyed;
        }
        else if (healthPercent < 0.25f)
        {
            Status = BodyPartStatus.SeverelyInjured;
        }
        else if (healthPercent < 0.75f)
        {
            Status = BodyPartStatus.Injured;
        }
        else
        {
            Status = BodyPartStatus.Healthy;
        }
    }
}

// Group of related body parts
public class BodyPartGroup(string name)
{
    public string Name { get; private set; } = name;
    public List<BodyPart> Parts { get; private set; } = new List<BodyPart>();

    public void AddPart(BodyPart part)
    {
        Parts.Add(part);
    }

    public void RemovePart(string bodyPartName)
    {
        Parts.RemoveAll(part => part.Name == bodyPartName);
    }

    public void AddParts(IEnumerable<BodyPart> parts)
    {
        Parts.AddRange(parts);
    }

    public float GetAverageHealth()
    {
        if (Parts.Count == 0)
        {
            return 0;
        }

        float totalHealth = 0;
        float totalImportance = 0;

        foreach (var part in Parts)
        {
            totalHealth += (part.CurrentHealth / part.MaxHealth) * part.Importance;
            totalImportance += part.Importance;
        }

        return totalImportance > 0 ? totalHealth / totalImportance : 0;
    }
}
