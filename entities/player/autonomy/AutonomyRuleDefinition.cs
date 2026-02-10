using System.Collections.Generic;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Autonomy;

/// <summary>
/// JSON model for a single autonomy rule file.
/// Loaded from resources/entities/autonomy/rules/*.json.
/// </summary>
public class AutonomyRuleDefinition
{
    public string? Id { get; set; }

    public string? DisplayName { get; set; }

    public string? TraitType { get; set; }

    public int Priority { get; set; }

    public DayPhaseType[] ? ActiveDuringPhases { get; set; }

    public Dictionary<string, object?> Parameters { get; set; } = [];
}
