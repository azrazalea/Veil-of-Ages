using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities
{
    public partial class Player : Being
    {
        public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;

        private EntityAction _nextAction = null;

        public void SetNextAction(EntityAction action)
        {
            _nextAction = action;
        }

        public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes attributes = null)
        {
            _baseMoveTicks = 3;
            base.Initialize(gridArea, startGridPos, attributes);
        }

        public override EntityAction Think(Vector2 currentPosition, ObservationData currentObservation)
        {
            // Return the queued action, or default to base behavior
            if (_nextAction != null)
            {
                var action = _nextAction;
                _nextAction = null;
                return action;
            }

            return new IdleAction(this);
        }

        public Vector2I GetFacingDirection()
        {
            // Determine facing direction based on sprite orientation
            if (_animatedSprite.FlipH)
                return new Vector2I(-1, 0); // Facing left
            else
                return new Vector2I(1, 0);  // Facing right
        }
    }
}
