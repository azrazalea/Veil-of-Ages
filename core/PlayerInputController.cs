using Godot;
using System;
using NecromancerKingdom.Entities.Actions;
using NecromancerKingdom.Entities;

namespace NecromancerKingdom.Core
{
    public partial class PlayerInputController : Node
    {
        private GameController _gameController;
        private Player _player;

        public override void _Ready()
        {
            _gameController = GetNode<GameController>("/root/World/GameController");
            _player = GetNode<Player>("/root/World/Entities/Player");

            if (_gameController == null || _player == null)
            {
                GD.PrintErr("PlayerInputController: Failed to find required nodes!");
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            // Skip if controller or player is not available
            if (_gameController == null || _player == null) return;
            if (!CanProcessMovementInput()) return;
            if (!_gameController.CanQueuePlayerAction()) return;

            // Check for held movement keys each frame
            if (Input.IsActionPressed("ui_right"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(1, 0);
                _gameController.QueuePlayerAction(new MoveAction(_player, targetPos));
            }
            else if (Input.IsActionPressed("ui_left"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(-1, 0);
                _gameController.QueuePlayerAction(new MoveAction(_player, targetPos));
            }
            else if (Input.IsActionPressed("ui_down"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(0, 1);
                _gameController.QueuePlayerAction(new MoveAction(_player, targetPos));
            }
            else if (Input.IsActionPressed("ui_up"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(0, -1);
                _gameController.QueuePlayerAction(new MoveAction(_player, targetPos));
            }
        }

        public override void _Input(InputEvent @event)
        {
            // Skip if simulation is paused
            if (_gameController == null) return;

            // Interaction
            else if (@event.IsActionPressed("interact"))
            {
                Vector2I interactPos = _player.GetCurrentGridPosition() + _player.GetFacingDirection();
                _gameController.QueuePlayerAction(new InteractAction(_player, interactPos));
            }

            // Simulation controls
            else if (@event.IsActionPressed("pause_simulation"))
            {
                _gameController.PauseSimulation();
            }
            else if (@event.IsActionPressed("resume_simulation"))
            {
                _gameController.ResumeSimulation();
            }
            else if (@event.IsActionPressed("speed_up"))
            {
                _gameController.SetTimeScale(_gameController.TimeScale * 2f);
            }
            else if (@event.IsActionPressed("slow_down"))
            {
                _gameController.SetTimeScale(_gameController.TimeScale * 0.5f);
            }
        }

        private bool CanProcessMovementInput()
        {
            return !_player.IsMoving();
        }
    }
}
