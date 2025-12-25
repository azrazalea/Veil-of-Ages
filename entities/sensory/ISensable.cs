using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Entities.Sensory;

public enum SensableType
{
    Being,
    Building
}

public enum SenseType
{
    Hearing,
    Sight,
    Smell,
}

// Interface for anything that can be sensed
public interface ISensable
{
    Dictionary<SenseType, float> DetectionDifficulties { get; }
    Vector2I GetCurrentGridPosition();
    SensableType GetSensableType();
    float GetDetectionDifficulty(SenseType senseType)
    {
        return DetectionDifficulties.TryGetValue(senseType, out var difficulty) ? difficulty : 1.0f;
    }
}
