using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;

using VeilOfAges.Entities;

namespace VeilOfAges.Entities.Sensory;

// Represents what an entity actually perceives after filtering
public class Perception
{
    private readonly Dictionary<Vector2I, List<ISensable>> _detectedSensables = new ();
    private readonly List<WorldEvent> _perceivedEvents = new ();
    private readonly List<EntityEvent> _entityEvents = new ();

    // Placeholder for future threat assessment system
    // private readonly Dictionary<Being, float> _threatLevels = new ();
    public void AddDetectedSensable(ISensable sensable, Vector2I position)
    {
        if (!_detectedSensables.TryGetValue(position, out var list))
        {
            list = new List<ISensable>();
            _detectedSensables[position] = list;
        }

        list.Add(sensable);
    }

    public void AddPerceivedEvent(WorldEvent evt)
    {
        _perceivedEvents.Add(evt);
    }

    /// <summary>
    /// Add an entity event (push, command, etc.) to this perception.
    /// Called by Being after processing events.
    /// </summary>
    public void AddEntityEvent(EntityEvent evt)
    {
        _entityEvents.Add(evt);
    }

    /// <summary>
    /// Get all entity events this tick.
    /// Activities use this to detect interruptions, pushes, etc.
    /// </summary>
    public IReadOnlyList<EntityEvent> GetEntityEvents() => _entityEvents;

    /// <summary>
    /// Check if any entity event of the specified type occurred this tick.
    /// </summary>
    public bool HasEntityEvent(EntityEventType type)
    {
        return _entityEvents.Any(e => e.Type == type);
    }

    /// <summary>
    /// Get entity events of a specific type.
    /// </summary>
    public IEnumerable<EntityEvent> GetEntityEventsOfType(EntityEventType type)
    {
        return _entityEvents.Where(e => e.Type == type);
    }

    // Helper methods for working with perceptions
    public List<(T entity, Vector2I position)> GetEntitiesOfType<T>()
        where T : Being
    {
        var result = new List<(T entity, Vector2I position)>();

        foreach (var pair in _detectedSensables)
        {
            foreach (var sensable in pair.Value)
            {
                if (sensable is T typedEntity)
                {
                    result.Add((typedEntity, pair.Key));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get all detected sensables with their positions.
    /// Used for creating perception-aware pathfinding grids.
    /// </summary>
    public IEnumerable<(ISensable sensable, Vector2I position)> GetAllDetected()
    {
        foreach (var pair in _detectedSensables)
        {
            foreach (var sensable in pair.Value)
            {
                yield return (sensable, pair.Key);
            }
        }
    }

    /// <summary>
    /// Check if any sensable is detected at a specific position.
    /// </summary>
    public bool HasSensableAt(Vector2I position)
    {
        return _detectedSensables.TryGetValue(position, out var list) && list.Count > 0;
    }

    /// <summary>
    /// Get sensables at a specific position.
    /// </summary>
    public IReadOnlyList<ISensable> GetSensablesAt(Vector2I position)
    {
        if (_detectedSensables.TryGetValue(position, out var list))
        {
            return list;
        }

        return [];
    }
}
