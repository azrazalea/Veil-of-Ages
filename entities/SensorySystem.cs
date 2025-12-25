using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Sensory;

public class SensorySystem
{
    private readonly World _world;
    private readonly SpatialPartitioning _spatialHash;

    // Cache observation data per frame to avoid recalculating
    private readonly Dictionary<Vector2I, ObservationGrid> _observationCache = new ();

    public SensorySystem(World world)
    {
        _world = world;
        _spatialHash = new SpatialPartitioning();
    }

    // Get sensory data for a specific entity's position
    public ObservationData GetObservationFor(Being entity)
    {
        Vector2I position = entity.GetCurrentGridPosition();
        uint senseRange = entity.MaxSenseRange;

        // Get observation grid for this position and range
        var grid = GetObservationGrid(position, senseRange);

        // Get relevant events in range
        var events = _world?.GetEventSystem()?.GetEventsInRange(position, senseRange) ?? [];

        // Return all observation data as read-only
        return new ObservationData(grid, events);
    }

    private ObservationGrid GetObservationGrid(Vector2I center, uint range)
    {
        // Return cached grid if available
        if (_observationCache.TryGetValue(center, out var cachedGrid))
        {
            return cachedGrid;
        }

        // Create new grid
        var grid = new ObservationGrid(center, (int)range);

        // Fill grid from spatial hash
        foreach (var pos in grid.GetCoveredPositions())
        {
            var sensables = _spatialHash.GetAtPosition(pos);
            foreach (var sensable in sensables)
            {
                grid.AddSensable(pos, sensable);
            }
        }

        // Cache grid
        _observationCache[center] = grid;
        return grid;
    }

    // Update all sensory data for this tick
    public void PrepareForTick()
    {
        // Clear last frame's cache
        _observationCache.Clear();

        // Update spatial partitioning
        _spatialHash.Clear();
        foreach (var sensable in _world.GetBeings())
        {
            _spatialHash.Add(sensable);
        }
    }
}
