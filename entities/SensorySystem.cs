using NecromancerKingdom.Core.Lib;
using System.Collections.Generic;
using NecromancerKingdom.Entities.Beings;
using NecromancerKingdom.Entities.Traits;
using Godot;

namespace NecromancerKingdom.Entities.Sensory
{
    public class SensorySystem
    {
        private World _world;
        private SpatialPartitioning _spatialHash;

        // Cached Dijkstra maps shared between entities
        private Dictionary<DijkstraGoalType, DijkstraMap> _dijkstraMaps = new();

        // Cache observation data per frame to avoid recalculating
        private Dictionary<Vector2I, ObservationGrid> _observationCache = new();

        private uint currentDijkstraTick = 0;
        private uint maxDijkstraTick = 5;

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

            // Get relevant Dijkstra maps for this entity type
            var maps = GetRelevantDijkstraMaps(entity);

            // Get relevant events in range
            var events = _world.GetEventSystem().GetEventsInRange(position, senseRange);

            // Return all observation data as read-only
            return new ObservationData(grid, maps, events);
        }

        private ObservationGrid GetObservationGrid(Vector2I center, uint range)
        {
            // Return cached grid if available
            if (_observationCache.TryGetValue(center, out var cachedGrid))
                return cachedGrid;

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

        // Get appropriate Dijkstra maps for this entity
        private IReadOnlyList<DijkstraMap> GetRelevantDijkstraMaps(Being entity)
        {
            var relevantMaps = new List<DijkstraMap>();

            // Add maps based on entity traits and needs
            if (entity.HasTrait<UndeadTrait>() && entity is MindlessZombie)
            {
                // Zombies care about living beings (food)
                relevantMaps.Add(GetDijkstraMap(DijkstraGoalType.LivingBeings));
            }
            else if (!entity.HasTrait<UndeadTrait>())
            {
                // Living beings might care about safety from undead
                relevantMaps.Add(GetDijkstraMap(DijkstraGoalType.DistanceFromUndead));
            }

            return relevantMaps;
        }

        // Get or create a Dijkstra map
        private DijkstraMap GetDijkstraMap(DijkstraGoalType goalType)
        {
            if (!_dijkstraMaps.TryGetValue(goalType, out var map))
            {
                map = new DijkstraMap(_world, goalType);
                _dijkstraMaps[goalType] = map;
            }
            return map;
        }

        // Update all sensory data for this tick
        public void PrepareForTick()
        {
            // Clear last frame's cache
            _observationCache.Clear();

            // Update spatial partitioning
            _spatialHash.Clear();
            foreach (var sensable in _world.GetEntities())
            {
                _spatialHash.Add(sensable);
            }

            currentDijkstraTick++;
            // Update Dijkstra maps (not necessarily every tick)
            if (currentDijkstraTick == maxDijkstraTick)  // Every 5 ticks
            {
                foreach (var map in _dijkstraMaps.Values)
                {
                    map.Recalculate();
                }
            }

            if (currentDijkstraTick >= maxDijkstraTick) currentDijkstraTick = 0;
        }
    }
}
