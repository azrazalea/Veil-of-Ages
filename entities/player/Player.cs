using Godot;
using System;

namespace NecromancerKingdom.Entities
{
    public partial class Player : Being
    {
        public override void _PhysicsProcess(double delta)
        {
            if (_gridSystem == null)
                return;

            if (Input.IsKeyPressed(Key.D))
            {
                _gridSystem.DebugGridCellStatus(_currentGridPos);
            }

            // If we're at the target position, check for new input
            if (_moveProgress >= 1.0f)
            {
                _direction = Vector2.Zero;

                if (Input.IsActionPressed("ui_right"))
                    _direction = Vector2.Right;
                else if (Input.IsActionPressed("ui_left"))
                    _direction = Vector2.Left;
                else if (Input.IsActionPressed("ui_down"))
                    _direction = Vector2.Down;
                else if (Input.IsActionPressed("ui_up"))
                    _direction = Vector2.Up;

                // If we have a new direction, try to move to the next tile
                if (_direction != Vector2.Zero)
                {
                    Vector2I targetGridPos = _currentGridPos + new Vector2I(
                        (int)_direction.X,
                        (int)_direction.Y
                    );

                    TryMoveToGridPosition(targetGridPos);
                }
                else
                {
                    _animatedSprite.Play("idle");
                }
            }

            // Update position based on movement progress
            UpdateMovement(delta);
        }

        // Called when the player interacts with objects
        public void Interact()
        {
            // Get the grid position in front of the player
            Vector2I interactGridPos = _currentGridPos + new Vector2I(
                (int)_direction.X,
                (int)_direction.Y
            );

            // Check if there's an interactable object at that position
            // This would be implemented later
            GD.Print($"Attempting to interact with object at grid position {interactGridPos}");
        }
    }
}
