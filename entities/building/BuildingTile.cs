using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// Represents an individual tile within a building structure.
/// </summary>
public class BuildingTile
{
    // Core properties
    public TileType Type { get; set; }
    public string Material { get; set; }
    public string Variant { get; set; }
    public bool IsWalkable { get; set; }
    public int Durability { get; set; }
    public int MaxDurability { get; set; }

    // Visual properties
    public Vector2I AtlasCoords { get; set; }
    public int SourceId { get; set; }

    // References
    public Building ParentBuilding { get; set; }
    public Vector2I GridPosition { get; set; }

    // Optional properties
    public string? Name { get; set; }
    public Dictionary<SenseType, float> DetectionDifficulties { get; private set; } = [];

    public BuildingTile(
        TileType type,
        string material,
        string variantName,
        bool isWalkable,
        int durability,
        Vector2I atlasCoords,
        int sourceId,
        Building parentBuilding,
        Vector2I gridPosition)
    {
        Type = type;
        Material = material;
        Variant = variantName;
        IsWalkable = isWalkable;
        Durability = durability;
        MaxDurability = durability;
        AtlasCoords = atlasCoords;
        SourceId = sourceId;
        ParentBuilding = parentBuilding;
        GridPosition = gridPosition;

        // Default detection difficulties based on tile type
        InitializeDetectionDifficulties();
    }

    private void InitializeDetectionDifficulties()
    {
        // Set default detection difficulties based on tile type and material
        DetectionDifficulties = new Dictionary<SenseType, float>();

        switch (Type)
        {
            case TileType.Wall:
                DetectionDifficulties[SenseType.Sight] = 1.0f;
                DetectionDifficulties[SenseType.Hearing] = 0.5f;
                DetectionDifficulties[SenseType.Smell] = 0.8f;
                break;

            case TileType.Floor:
                DetectionDifficulties[SenseType.Sight] = 0.0f;
                DetectionDifficulties[SenseType.Hearing] = 0.1f;
                DetectionDifficulties[SenseType.Smell] = 0.0f;
                break;

            case TileType.Door:
                DetectionDifficulties[SenseType.Sight] = 0.9f;
                DetectionDifficulties[SenseType.Hearing] = 0.3f;
                DetectionDifficulties[SenseType.Smell] = 0.5f;
                break;

            case TileType.Window:
                DetectionDifficulties[SenseType.Sight] = 0.2f;
                DetectionDifficulties[SenseType.Hearing] = 0.6f;
                DetectionDifficulties[SenseType.Smell] = 0.7f;
                break;

            default:
                DetectionDifficulties[SenseType.Sight] = 0.0f;
                DetectionDifficulties[SenseType.Hearing] = 0.0f;
                DetectionDifficulties[SenseType.Smell] = 0.0f;
                break;
        }

        // Adjust based on material
        AdjustDetectionDifficultiesByMaterial();
    }

    private void AdjustDetectionDifficultiesByMaterial()
    {
        // Multipliers for different materials
        float sightMultiplier = 1.0f;
        float soundMultiplier = 1.0f;
        float smellMultiplier = 1.0f;

        switch (Material.ToLower())
        {
            case "wood":
                soundMultiplier = 0.8f;
                break;
            case "stone":
                soundMultiplier = 1.2f;
                break;
            case "metal":
                soundMultiplier = 1.5f;
                smellMultiplier = 0.5f;
                break;

                // Add other materials as needed
        }

        // Apply multipliers
        if (DetectionDifficulties.ContainsKey(SenseType.Sight))
        {
            DetectionDifficulties[SenseType.Sight] *= sightMultiplier;
        }

        if (DetectionDifficulties.ContainsKey(SenseType.Hearing))
        {
            DetectionDifficulties[SenseType.Hearing] *= soundMultiplier;
        }

        if (DetectionDifficulties.ContainsKey(SenseType.Smell))
        {
            DetectionDifficulties[SenseType.Smell] *= smellMultiplier;
        }
    }

    /// <summary>
    /// Apply damage to this building tile.
    /// </summary>
    /// <param name="amount">Amount of damage to apply.</param>
    /// <returns>True if the tile was destroyed, false otherwise.</returns>
    public bool TakeDamage(int amount)
    {
        Durability -= amount;
        if (Durability <= 0)
        {
            Durability = 0;

            // Tile has been destroyed, might change its type or behavior
            return true;
        }

        return false;
    }

    /// <summary>
    /// Repair this building tile.
    /// </summary>
    /// <param name="amount">Amount of durability to restore.</param>
    public void Repair(int amount)
    {
        Durability += amount;
        if (Durability > MaxDurability)
        {
            Durability = MaxDurability;
        }
    }

    /// <summary>
    /// Check if this tile blocks a specific sense type.
    /// </summary>
    /// <returns></returns>
    public bool BlocksSense(SenseType senseType)
    {
        return DetectionDifficulties.ContainsKey(senseType) &&
               DetectionDifficulties[senseType] >= 0.9f;
    }

    /// <summary>
    /// Get the current condition of the tile as a percentage.
    /// </summary>
    /// <returns></returns>
    public float GetConditionPercentage()
    {
        return (float)Durability / MaxDurability;
    }
}

/// <summary>
/// Enumeration of building tile types.
/// </summary>
public enum TileType
{
    Wall,
    Crop,
    Floor,
    Door,
    Window,
    Stairs,
    Roof,
    Column,
    Fence,
    Gate,
    Foundation,
    Furniture, // Generic furniture (specific furniture types could be subclasses or have additional properties)
    Decoration // Decorative elements
}
