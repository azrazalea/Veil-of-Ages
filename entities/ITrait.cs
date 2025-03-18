using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities
{
    // Base interface for all traits
    public interface ITrait
    {
        // Initialize the trait with its owner
        void Initialize(Being owner, BodyHealth health) { }
        void Initialize(IEntity owner) { }

        // Process trait behavior (called every frame/tick)
        void Process(double delta);

        // Optional method for handling events
        void OnEvent(string eventName, params object[] args);

        EntityAction SuggestAction(Vector2 currentOwnerPosition, Perception currentPerception);
    }
}
