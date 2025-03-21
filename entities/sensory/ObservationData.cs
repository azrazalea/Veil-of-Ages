using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Sensory
{
    public class ObservationData
    {
        public IReadOnlyList<DijkstraMap> DijkstraMaps { get; }
        public ObservationGrid Grid { get; }
        public IReadOnlyList<WorldEvent> Events { get; }

        public ObservationData(
            ObservationGrid grid,
            IReadOnlyList<DijkstraMap> dijkstraMaps,
            IReadOnlyList<WorldEvent> events)
        {
            Grid = grid;
            DijkstraMaps = dijkstraMaps;
            Events = events;
        }
    }
}
