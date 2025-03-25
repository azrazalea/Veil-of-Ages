using Godot;
using System;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities;
using VeilOfAges.UI;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Core
{
    public partial class PlayerInputController : Node
    {
        private GameController? _gameController;
        private Player? _player;
        private Dialogue? _dialogueUI;
        private PanelContainer? _quickActions;
        private PanelContainer? _minimap;
        private PanelContainer? _chooseLocationPrompt;
        private EntityCommand? _pendingCommand = null;
        private Being? _commandTarget = null;
        private bool _awaitingLocationSelection = false;

        public override void _Ready()
        {
            _gameController = GetNode<GameController>("/root/World/GameController");
            _player = GetNode<Player>("/root/World/Entities/Player");
            _dialogueUI = GetNode<Dialogue>("/root/World/HUD/Dialogue");
            _quickActions = GetNode<PanelContainer>("/root/World/HUD/Quick Actions Container");
            _minimap = GetNode<PanelContainer>("/root/World/HUD/Minimap Container");
            _chooseLocationPrompt = GetNode<PanelContainer>("/root/World/HUD/Choose Location Prompt");

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
                _gameController.QueuePlayerAction(new MoveAction(_player, this, targetPos));
            }
            else if (Input.IsActionPressed("ui_left"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(-1, 0);
                _gameController.QueuePlayerAction(new MoveAction(_player, this, targetPos));
            }
            else if (Input.IsActionPressed("ui_down"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(0, 1);
                _gameController.QueuePlayerAction(new MoveAction(_player, this, targetPos));
            }
            else if (Input.IsActionPressed("ui_up"))
            {
                Vector2I currentPos = _player.GetCurrentGridPosition();
                Vector2I targetPos = currentPos + new Vector2I(0, -1);
                _gameController.QueuePlayerAction(new MoveAction(_player, this, targetPos));
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

            if (_awaitingLocationSelection && @event is InputEventMouseButton mouseEvent &&
    mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                // Get mouse position and convert to world space
                Vector2 worldPos = GetViewport().GetCamera2D().GetGlobalMousePosition();
                // Convert to grid position
                Vector2I gridPos = Grid.Utils.WorldToGrid(worldPos);

                // Check if the position is valid
                var gridArea = _commandTarget?.GetGridArea();
                if (gridArea != null && gridPos.X >= 0 && gridPos.Y >= 0 &&
                    gridPos.X < gridArea.GridSize.X && gridPos.Y < gridArea.GridSize.Y)
                {
                    // Add position parameter to command
                    if (_pendingCommand != null)
                    {
                        if (_pendingCommand is MoveToCommand)
                        {
                            _pendingCommand.WithParameter("targetPos", gridPos);
                        }
                        else if (_pendingCommand is GuardCommand)
                        {
                            _pendingCommand.WithParameter("guardPos", gridPos);
                        }

                        // Resume simulation
                        _gameController?.ResumeSimulation();

                        GD.Print($"Command target location set to {gridPos}");
                    }
                }
                else
                {
                    GD.Print("Invalid location selected");
                }

                // Clear selection state
                _pendingCommand = null;
                _commandTarget = null;
                _awaitingLocationSelection = false;
                if (_chooseLocationPrompt != null) _chooseLocationPrompt.Visible = false;
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

        public void StartLocationSelection(EntityCommand command, Being target)
        {
            _pendingCommand = command;
            _commandTarget = target;
            _awaitingLocationSelection = true;

            // Optionally pause simulation
            _gameController?.PauseSimulation();

            // Notify player
            if (_chooseLocationPrompt != null) _chooseLocationPrompt.Visible = true;
        }
    }
}
