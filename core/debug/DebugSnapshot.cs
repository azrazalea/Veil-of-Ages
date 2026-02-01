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
    /// <summary>
    /// Gets or sets entity name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets entity type (class name).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets grid position.
    /// </summary>
    [JsonPropertyName("position")]
    public Vector2I Position { get; set; }

    /// <summary>
    /// Gets or sets current activity description (or "Idle").
    /// </summary>
    [JsonPropertyName("activity")]
    public string Activity { get; set; } = "Idle";

    /// <summary>
    /// Gets or sets need name to value (0-1) mapping.
    /// </summary>
    [JsonPropertyName("needs")]
    public Dictionary<string, float> Needs { get; set; } = [];

    /// <summary>
    /// Gets or sets list of trait names.
    /// </summary>
    [JsonPropertyName("traits")]
    public List<string> Traits { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether whether entity is currently moving.
    /// </summary>
    [JsonPropertyName("isMoving")]
    public bool IsMoving { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether whether entity is in a queue.
    /// </summary>
    [JsonPropertyName("isInQueue")]
    public bool IsInQueue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether whether entity was blocked last tick.
    /// </summary>
    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }
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
    /// Gets or sets building locations.
    /// </summary>
    [JsonPropertyName("buildings")]
    public List<BuildingSnapshot> Buildings { get; set; } = [];

    /// <summary>
    /// Gets or sets set of water positions for ASCII visualization.
    /// Not serialized - used internally.
    /// </summary>
    [JsonIgnore]
    public HashSet<Vector2I> WaterPositions { get; set; } = [];

    /// <summary>
    /// Set of building positions for ASCII visualization.
    /// Not serialized - computed from Buildings.
    /// </summary>
    [JsonIgnore]
    private HashSet<Vector2I>? _buildingPositions;

    /// <summary>
    /// Generate ASCII visualization of the grid.
    /// Characters used:
    /// - '.' = empty/grass
    /// - '#' = building
    /// - '@' = idle entity
    /// - 'W' = walking entity
    /// - 'B' = blocked entity
    /// - 'Q' = queued entity
    /// - '~' = water.
    /// </summary>
    /// <param name="entities">List of entity snapshots to place on the grid.</param>
    /// <returns>ASCII string representation of the grid.</returns>
    public string ToAscii(List<EntitySnapshot> entities)
    {
        // Build building positions set if not already done
        if (_buildingPositions == null)
        {
            _buildingPositions = [];
            foreach (var building in Buildings)
            {
                for (int bx = 0; bx < building.Size.X; bx++)
                {
                    for (int by = 0; by < building.Size.Y; by++)
                    {
                        _buildingPositions.Add(new Vector2I(building.Position.X + bx, building.Position.Y + by));
                    }
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
                    // Priority: Blocked > Queued > Walking > Idle
                    if (entity.IsBlocked)
                    {
                        sb.Append('B');
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
                else if (_buildingPositions.Contains(pos))
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
/// Building info snapshot for debug server.
/// </summary>
public class BuildingSnapshot
{
    /// <summary>
    /// Gets or sets building name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets building type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets grid position.
    /// </summary>
    [JsonPropertyName("position")]
    public Vector2I Position { get; set; }

    /// <summary>
    /// Gets or sets building size in tiles.
    /// </summary>
    [JsonPropertyName("size")]
    public Vector2I Size { get; set; }
}
