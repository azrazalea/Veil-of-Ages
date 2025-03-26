using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public abstract class UndeadBehaviorTrait : UndeadTrait
    {
        // Common properties for all undead behaviors
        public float WanderProbability { get; set; } = 0.2f;
        public float WanderRange { get; set; } = 10.0f;
        public uint IdleTime { get; set; } = 10;

        // Override this to implement different behavior states
        protected abstract EntityAction? ProcessState(Vector2I currentOwnerGridPosition, Perception currentPerception);

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);
            _stateTimer = IdleTime;
            GD.Print($"{owner.Name}: UndeadBehavior trait initialized");
        }

        public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            // Decrement the timer
            if (_stateTimer > 0)
                _stateTimer--;

            // Let derived classes handle their specific behaviors
            return ProcessState(currentOwnerGridPosition, currentPerception);
        }

        // Common method for wandering behavior used by all undead
        protected EntityAction? TryToWander()
        {
            return TryToWander(WanderRange);
        }

        // Check if current position is outside wander range
        protected bool IsOutsideWanderRange()
        {
            return IsOutsideWanderRange(WanderRange);
        }
    }
}
