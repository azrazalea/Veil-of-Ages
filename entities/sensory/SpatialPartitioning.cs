using Godot;
using System;
using System.Collections.Generic;

namespace VeilOfAges.Entities.Sensory
{
    // Efficient spatial lookup system
    public class SpatialPartitioning
    {
        private Dictionary<Vector2I, List<ISensable>> _grid = new();

        public void Add(ISensable sensable)
        {
            var pos = sensable.GetGridPosition();

            if (!_grid.TryGetValue(pos, out var list))
            {
                list = new List<ISensable>();
                _grid[pos] = list;
            }

            list.Add(sensable);
        }

        public void Clear()
        {
            _grid.Clear();
        }

        public IReadOnlyList<ISensable> GetAtPosition(Vector2I position)
        {
            if (_grid.TryGetValue(position, out var list))
                return list.AsReadOnly();
            return Array.Empty<ISensable>();
        }

        public IReadOnlyList<ISensable> GetInArea(Vector2I center, int range)
        {
            var result = new List<ISensable>();

            for (int x = center.X - range; x <= center.X + range; x++)
            {
                for (int y = center.Y - range; y <= center.Y + range; y++)
                {
                    var pos = new Vector2I(x, y);
                    result.AddRange(GetAtPosition(pos));
                }
            }

            return result.AsReadOnly();
        }
    }
}
