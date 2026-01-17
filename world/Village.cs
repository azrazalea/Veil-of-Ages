using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges;

/// <summary>
/// Represents a village settlement with shared knowledge about buildings and landmarks.
/// Entities that are residents share access to this village's knowledge by reference.
/// </summary>
public partial class Village : Node
{
    public string VillageName { get; private set; } = string.Empty;
    public Vector2I Center { get; private set; }

    /// <summary>
    /// Gets shared knowledge for this village, passed by reference to all residents.
    /// </summary>
    public SharedKnowledge Knowledge { get; private set; } = null!;

    private readonly List<Being> _residents = new ();
    private readonly List<Building> _buildings = new ();

    public IReadOnlyList<Being> Residents => _residents;
    public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary>
    /// Initialize the village with a name and center position.
    /// </summary>
    public void Initialize(string name, Vector2I center)
    {
        VillageName = name;
        Name = name; // Godot node name
        Center = center;

        Knowledge = new SharedKnowledge(
            id: $"village_{name.ToLowerInvariant().Replace(" ", "_")}",
            name: name,
            scopeType: "village");

        // Set the village center as a landmark
        Knowledge.SetLandmark("center", center);
        Knowledge.SetLandmark("square", center);
        Knowledge.SetFact("name", name);
    }

    /// <summary>
    /// Add a building to this village. Registers it in shared knowledge.
    /// </summary>
    public void AddBuilding(Building building)
    {
        if (_buildings.Contains(building))
        {
            return;
        }

        _buildings.Add(building);
        Knowledge.RegisterBuilding(building);
    }

    /// <summary>
    /// Remove a building from this village.
    /// </summary>
    public void RemoveBuilding(Building building)
    {
        _buildings.Remove(building);
        Knowledge.UnregisterBuilding(building);
    }

    /// <summary>
    /// Add a resident to this village. Gives them access to village knowledge.
    /// </summary>
    public void AddResident(Being being)
    {
        if (_residents.Contains(being))
        {
            return;
        }

        _residents.Add(being);
        being.AddSharedKnowledge(Knowledge);
    }

    /// <summary>
    /// Remove a resident from this village. Removes their access to village knowledge.
    /// </summary>
    public void RemoveResident(Being being)
    {
        _residents.Remove(being);
        being.RemoveSharedKnowledge(Knowledge);
    }

    /// <summary>
    /// Clean up invalid building references periodically.
    /// </summary>
    public void CleanupInvalidReferences()
    {
        _buildings.RemoveAll(b => !GodotObject.IsInstanceValid(b));
        _residents.RemoveAll(r => !GodotObject.IsInstanceValid(r));
        Knowledge.CleanupInvalidReferences();
    }
}
