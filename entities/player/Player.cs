using Godot;
using NecromancerKingdom.Entities.Actions;

namespace NecromancerKingdom.Entities
{
    public partial class Player : Being
    {
        public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;

        private EntityAction _nextAction = null;

        public void SetNextAction(EntityAction action)
        {
            _nextAction = action;
        }

        public override EntityAction Think()
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

        public override void _PhysicsProcess(double delta)
        {
            // Do NOT directly process movement here anymore
            // Just handle debugging commands or other non-movement functionality
            if (_gridSystem == null)
                return;

            if (Input.IsKeyPressed(Key.D))
            {
                _gridSystem.DebugGridCellStatus(_currentGridPos);
            }

            // Update animation state based on movement
            UpdateAnimation();

            // Update movement position interpolation
            UpdateMovement(delta);
        }

        private void UpdateAnimation()
        {
            if (IsMoving())
            {
                _animatedSprite.Play("walk");
            }
            else
            {
                _animatedSprite.Play("idle");
            }
        }
    }
}
