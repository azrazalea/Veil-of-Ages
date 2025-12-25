// Represents raw sensory data in a grid format
using System;
using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Entities.Sensory;

public class ObservationGrid
{
    private Vector2I _center;
    private readonly int _range;
    private readonly Dictionary<Vector2I, List<ISensable>> _gridContents = new ();

    public ObservationGrid(Vector2I center, int range)
    {
        _center = center;
        _range = range;
    }

    public void AddSensable(Vector2I position, ISensable sensable)
    {
        if (!_gridContents.TryGetValue(position, out var list))
        {
            list = new List<ISensable>();
            _gridContents[position] = list;
        }

        list.Add(sensable);
    }

    // Get all sensables at a specific position
    public IReadOnlyList<ISensable> GetAtPosition(Vector2I position)
    {
        if (_gridContents.TryGetValue(position, out var list))
        {
            return list.AsReadOnly();
        }

        return Array.Empty<ISensable>();
    }

    // Get all grid positions in this observation area
    public IEnumerable<Vector2I> GetCoveredPositions()
    {
        for (int x = _center.X - _range; x <= _center.X + _range; x++)
        {
            for (int y = _center.Y - _range; y <= _center.Y + _range; y++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    // Check if a position is in this grid
    public bool IsInRange(Vector2I position)
    {
        return Math.Abs(position.X - _center.X) <= _range &&
               Math.Abs(position.Y - _center.Y) <= _range;
    }
}
