using Godot;

namespace NecromancerKingdom.Entities.Sensory
{
    public enum SensableType
    {
        Entity
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
        Vector2I GetGridPosition();
        SensableType GetSensableType();
        float GetDetectionDifficulty(SenseType senseType);
    }

}
