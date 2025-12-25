using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VeilOfAges.Entities.Sensory;

public enum EventType
{
    Sound,
    Visual,
    Smell,
    Environmental
}

public abstract class WorldEvent
{
    public Vector2I Position { get; }
    public float Radius { get; }
    public float Intensity { get; }
    public EventType Type { get; }

    public WorldEvent(Vector2I position, EventType type, float radius, float intensity)
    {
        Position = position;
        Type = type;
        Radius = radius;
        Intensity = intensity;
    }
}

public class EventSystem
{
    private readonly List<WorldEvent> _events = new ();

    public void AddEvent(WorldEvent evt)
    {
        _events.Add(evt);
    }

    public IReadOnlyList<WorldEvent> GetEventsInRange(Vector2I center, uint range)
    {
        return _events
            .Where(e => e.Position.DistanceTo(center) <= range + e.Radius)
            .ToList()
            .AsReadOnly();
    }

    public void ClearEvents()
    {
        _events.Clear();
    }
}
