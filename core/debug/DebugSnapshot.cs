using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Godot;

namespace VeilOfAges.Core.Debug;

/// <summary>
/// Full game state snapshot for debug server.
/// </summary>
public class GameStateSnapshot
{
    /// <summary>
    /// Gets or sets current tick number.
    /// </summary>
    [JsonPropertyName("tick")]
    public uint Tick { get; set; }

    /// <summary>
    /// Gets or sets formatted game time string.
    /// </summary>
    [JsonPropertyName("gameTime")]
    public string GameTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether whether simulation is paused.
    /// </summary>
    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; set; }

    /// <summary>
    /// Gets or sets number of entities.
    /// </summary>
    [JsonPropertyName("entityCount")]
    public int EntityCount { get; set; }

    /// <summary>
    /// Gets or sets all entity snapshots.
    /// </summary>
    [JsonPropertyName("entities")]
    public List<EntitySnapshot> Entities { get; set; } = [];

    /// <summary>
    /// Gets or sets grid visualization data.
    /// </summary>
    [JsonPropertyName("grid")]
    public GridSnapshot? Grid { get; set; }
}

/// <summary>
/// Single entity state snapshot for debug server.
/// </summary>
public class EntitySnapshot
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("definitionId")]
    public string? DefinitionId { get; set; }

    [JsonPropertyName("position")]
    public Vector2I Position { get; set; }

    [JsonPropertyName("activity")]
    public string Activity { get; set; } = "Idle";

    [JsonPropertyName("activityDisplayName")]
    public string? ActivityDisplayName { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("needs")]
    public Dictionary<string, float> Needs { get; set; } = [];

    [JsonPropertyName("traits")]
    public List<string> Traits { get; set; } = [];

    [JsonPropertyName("skills")]
    public List<SkillSnapshot> Skills { get; set; } = [];

    [JsonPropertyName("attributes")]
    public AttributeSnapshot? Attributes { get; set; }

    [JsonPropertyName("health")]
    public HealthSnapshot? Health { get; set; }

    [JsonPropertyName("inventory")]
    public List<ItemSnapshot> Inventory { get; set; } = [];

    [JsonPropertyName("village")]
    public string? Village { get; set; }

    [JsonPropertyName("isMoving")]
    public bool IsMoving { get; set; }

    [JsonPropertyName("isInQueue")]
    public bool IsInQueue { get; set; }

    [JsonPropertyName("isHidden")]
    public bool IsHidden { get; set; }

    [JsonPropertyName("autonomyRules")]
    public List<AutonomyRuleSnapshot>? AutonomyRules { get; set; }
}

/// <summary>
/// Skill state snapshot.
/// </summary>
public class SkillSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("currentXp")]
    public float CurrentXp { get; set; }

    [JsonPropertyName("xpToNextLevel")]
    public float XpToNextLevel { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }
}

/// <summary>
/// Attribute values snapshot.
/// </summary>
public class AttributeSnapshot
{
    [JsonPropertyName("strength")]
    public float Strength { get; set; }

    [JsonPropertyName("dexterity")]
    public float Dexterity { get; set; }

    [JsonPropertyName("constitution")]
    public float Constitution { get; set; }

    [JsonPropertyName("intelligence")]
    public float Intelligence { get; set; }

    [JsonPropertyName("willpower")]
    public float Willpower { get; set; }

    [JsonPropertyName("wisdom")]
    public float Wisdom { get; set; }

    [JsonPropertyName("charisma")]
    public float Charisma { get; set; }
}

/// <summary>
/// Health state snapshot.
/// </summary>
public class HealthSnapshot
{
    [JsonPropertyName("percentage")]
    public float Percentage { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("efficiency")]
    public float Efficiency { get; set; }
}

/// <summary>
/// Inventory item snapshot.
/// </summary>
public class ItemSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

/// <summary>
/// Grid visualization snapshot for debug server.
/// </summary>
public class GridSnapshot
{
    /// <summary>
    /// Gets or sets grid width.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets grid height.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets room snapshots from villages in the active area.
    /// </summary>
    [JsonPropertyName("rooms")]
    public List<RoomSnapshot> Rooms { get; set; } = [];

    /// <summary>
    /// Gets or sets set of water positions for ASCII visualization.
    /// Not serialized - used internally.
    /// </summary>
    [JsonIgnore]
    public HashSet<Vector2I> WaterPositions { get; set; } = [];

    /// <summary>
    /// Set of structural positions for ASCII visualization.
    /// Not serialized - computed from room tile positions.
    /// </summary>
    [JsonIgnore]
    private HashSet<Vector2I>? _structurePositions;

    /// <summary>
    /// Generate ASCII visualization of the grid.
    /// Characters used:
    /// - '.' = empty/grass
    /// - '#' = structure (room tile)
    /// - '@' = idle entity
    /// - 'W' = walking entity
    /// - 'H' = hidden entity
    /// - 'Q' = queued entity
    /// - '~' = water.
    /// </summary>
    /// <param name="entities">List of entity snapshots to place on the grid.</param>
    /// <returns>ASCII string representation of the grid.</returns>
    public string ToAscii(List<EntitySnapshot> entities)
    {
        // Build structure positions set if not already done
        if (_structurePositions == null)
        {
            _structurePositions = [];
            foreach (var room in Rooms)
            {
                foreach (var tile in room.TilePositions)
                {
                    _structurePositions.Add(tile);
                }
            }
        }

        // Build entity position lookup
        var entityPositions = new Dictionary<Vector2I, EntitySnapshot>();
        foreach (var entity in entities)
        {
            // Later entities overwrite earlier ones at same position
            entityPositions[entity.Position] = entity;
        }

        var sb = new StringBuilder();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var pos = new Vector2I(x, y);

                // Check for entity first (entities appear on top)
                if (entityPositions.TryGetValue(pos, out var entity))
                {
                    // Priority: Hidden > Queued > Walking > Idle
                    if (entity.IsHidden)
                    {
                        sb.Append('H');
                    }
                    else if (entity.IsInQueue)
                    {
                        sb.Append('Q');
                    }
                    else if (entity.IsMoving)
                    {
                        sb.Append('W');
                    }
                    else
                    {
                        sb.Append('@');
                    }
                }
                else if (_structurePositions.Contains(pos))
                {
                    sb.Append('#');
                }
                else if (WaterPositions.Contains(pos))
                {
                    sb.Append('~');
                }
                else
                {
                    sb.Append('.');
                }
            }

            // Add newline after each row (except the last)
            if (y < Height - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Autonomy rule snapshot for debug server.
/// </summary>
public class AutonomyRuleSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("traitType")]
    public string TraitType { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("activeDuringPhases")]
    public string[] ? ActiveDuringPhases { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// Room info snapshot for debug server.
/// Rooms are the primary structural unit (buildings no longer exist as entities).
/// </summary>
public class RoomSnapshot
{
    /// <summary>
    /// Gets or sets room identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets room name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets room type (e.g., "House", "Farm", "Graveyard").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets room purpose (e.g., "Living", "Kitchen", "Workshop").
    /// </summary>
    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this room is secret.
    /// </summary>
    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }

    /// <summary>
    /// Gets or sets the number of residents assigned to this room.
    /// </summary>
    [JsonPropertyName("residentCount")]
    public int ResidentCount { get; set; }

    /// <summary>
    /// Gets or sets the room capacity (max residents, 0 = unlimited).
    /// </summary>
    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    /// <summary>
    /// Gets or sets facilities contained in this room.
    /// </summary>
    [JsonPropertyName("facilities")]
    public List<FacilitySnapshot> Facilities { get; set; } = [];

    /// <summary>
    /// Gets or sets absolute tile positions for this room.
    /// Used internally for ASCII grid rendering.
    /// </summary>
    [JsonIgnore]
    public List<Vector2I> TilePositions { get; set; } = [];
}

/// <summary>
/// Facility info snapshot for debug server.
/// </summary>
public class FacilitySnapshot
{
    /// <summary>
    /// Gets or sets facility identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute grid position of this facility's primary tile.
    /// </summary>
    [JsonPropertyName("position")]
    public Vector2I Position { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether entities can walk through this facility.
    /// </summary>
    [JsonPropertyName("isWalkable")]
    public bool IsWalkable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this facility has a storage trait.
    /// </summary>
    [JsonPropertyName("hasStorage")]
    public bool HasStorage { get; set; }

    /// <summary>
    /// Gets or sets the items stored in this facility's storage, if any.
    /// </summary>
    [JsonPropertyName("storageContents")]
    public List<ItemSnapshot>? StorageContents { get; set; }
}
