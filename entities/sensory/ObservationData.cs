using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Sensory;

public class ObservationData
{
    public ObservationGrid Grid { get; }
    public IReadOnlyList<WorldEvent> Events { get; }

    public ObservationData(
        ObservationGrid grid,
        IReadOnlyList<WorldEvent> events)
    {
        Grid = grid;
        Events = events;
    }
}
