using Godot;
using System.Collections.Generic;

namespace NecromancerKingdom.Entities.Sensory
{
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
        public Dictionary<SenseType, float> DetectionDifficulties { get; }
        Vector2I GetGridPosition();
        SensableType GetSensableType();
        float GetDetectionDifficulty(SenseType senseType)
        {
            return DetectionDifficulties.TryGetValue(senseType, out var difficulty) ? difficulty : 1.0f;
        }
    }

}
