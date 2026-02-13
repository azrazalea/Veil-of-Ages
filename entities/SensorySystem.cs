using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Sensory;

public class SensorySystem
{
    private readonly World _world;

    // Per-area spatial hashes to prevent cross-area perception
    private readonly Dictionary<Area, SpatialPartitioning> _areaSpatialHashes = new ();

    // Cache observation data per frame - keyed by (area, position) to prevent cross-area cache hits
    private readonly Dictionary<(Area, Vector2I), ObservationGrid> _observationCache = new ();

    public SensorySystem(World world)
    {
        _world = world;
    }

    // Get sensory data for a specific entity's position
    public ObservationData GetObservationFor(Being entity)
    {
        Vector2I position = entity.GetCurrentGridPosition();
        uint senseRange = entity.MaxSenseRange;
        var area = entity.GridArea;

        // Get observation grid for this position, range, and area
        var grid = GetObservationGrid(area, position, senseRange);

        // Get relevant events in range
        var events = _world?.GetEventSystem()?.GetEventsInRange(position, senseRange) ?? [];

        // Return all observation data as read-only
        return new ObservationData(grid, events);
    }

    private ObservationGrid GetObservationGrid(Area? area, Vector2I center, uint range)
    {
        // Use a default key for null area (shouldn't happen, but defensive)
        var cacheKey = (area!, center);

        // Return cached grid if available
        if (area != null && _observationCache.TryGetValue(cacheKey, out var cachedGrid))
        {
            return cachedGrid;
        }

        // Create new grid
        var grid = new ObservationGrid(center, (int)range);

        // Get the spatial hash for this area
        if (area != null && _areaSpatialHashes.TryGetValue(area, out var spatialHash))
        {
            // Fill grid from area-specific spatial hash
            foreach (var pos in grid.GetCoveredPositions())
            {
                var sensables = spatialHash.GetAtPosition(pos);
                foreach (var sensable in sensables)
                {
                    grid.AddSensable(pos, sensable);
                }
            }
        }

        // Cache grid
        if (area != null)
        {
            _observationCache[cacheKey] = grid;
        }

        return grid;
    }

    // Update all sensory data for this tick
    public void PrepareForTick()
    {
        // Clear last frame's cache
        _observationCache.Clear();

        // Clear and rebuild per-area spatial partitioning
        foreach (var hash in _areaSpatialHashes.Values)
        {
            hash.Clear();
        }

        foreach (var sensable in _world.GetBeings())
        {
            // Skip hidden entities - they can't be perceived
            if (sensable.IsHidden)
            {
                continue;
            }

            var area = sensable.GridArea;
            if (area == null)
            {
                continue;
            }

            if (!_areaSpatialHashes.TryGetValue(area, out var spatialHash))
            {
                spatialHash = new SpatialPartitioning();
                _areaSpatialHashes[area] = spatialHash;
            }

            spatialHash.Add(sensable);
        }
    }
}
