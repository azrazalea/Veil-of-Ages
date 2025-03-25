using Godot;
using System;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities;
using VeilOfAges.UI;

namespace VeilOfAges.Core
{
    public partial class PlayerInputController : Node
    {
        private GameController? _gameController;
        private Player? _player;
        private Dialogue? _dialogueUI;
        private PanelContainer? _quickActions;
        private PanelContainer? _minimap;

        public override void _Ready()
        {
            _gameController = GetNode<GameController>("/root/World/GameController");
            _player = GetNode<Player>("/root/World/Entities/Player");
            _dialogueUI = GetNode<Dialogue>("/root/World/HUD/Dialogue");
            _quickActions = GetNode<PanelContainer>("/root/World/HUD/Quick Actions Container");
            _minimap = GetNode<PanelContainer>("/root/World/HUD/Minimap Container");

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
            if (_gameController == null || _player == null) return;

            // Interaction
            else if (@event.IsActionPressed("interact"))
            {
                TryInteractWithNearbyEntity();
            }
            else if (@event.IsActionPressed("exit"))
            {
                _dialogueUI?.Close();
            }

            // Simulation controls
            else if (@event.IsActionPressed("toggle_simulation_pause"))
            {
                _gameController.ToggleSimulationPause();
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

        private void TryInteractWithNearbyEntity()
        {
            if (_player == null) return;

            // Get player's current position and facing direction
            Vector2I playerPos = _player.GetCurrentGridPosition();
            Vector2I facingDir = _player.GetFacingDirection();
            Vector2I interactPos = playerPos + facingDir;

            // Check if there's an entity at the interaction position
            if (GetEntityAtPosition(interactPos) is Being entity)
            {
                // Interact with the entity by showing dialogue
                var didStartDialogue = _dialogueUI?.ShowDialogue(_player, entity);
                if (didStartDialogue != true) return;

                if (_minimap != null && _quickActions != null)
                {
                    _minimap.Visible = false;
                    _quickActions.Visible = false;
                }
                GD.Print($"Interacting with {entity.Name}");
            }
        }

        private bool CanProcessMovementInput()
        {
            return !_player?.IsMoving() ?? false;
        }

        private Being? GetEntityAtPosition(Vector2I position)
        {
            // Get all entities from the world
            if (GetTree().GetFirstNodeInGroup("World") is World world)
            {
                var entity = world.ActiveGridArea?.EntitiesGridSystem.GetCell(position);
                if (entity is Being being)
                {
                    return being;
                }
            }

            return null;
        }
    }
}
