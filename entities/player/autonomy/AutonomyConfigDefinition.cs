using System.Collections.Generic;

namespace VeilOfAges.Entities.Autonomy;

/// <summary>
/// JSON model for an autonomy config file.
/// Loaded from resources/entities/autonomy/configs/*.json.
/// References rules by ID.
/// </summary>
public class AutonomyConfigDefinition
{
    public string? Id { get; set; }

    public List<string> Rules { get; set; } = [];
}
