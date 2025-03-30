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
        [Export]
        private Dialogue? _dialogueUI;
        [Export]
        private PanelContainer? _quickActions;
        [Export]
        private PanelContainer? _minimap;
        [Export]
        private PanelContainer? _chooseLocationPrompt;
        [Export]
        private PopupMenu? _contextMenu;
        [Export]
        private RichTextLabel? _nameLabel;
        [Export]
        private ProgressBar? _hungerBar;
        private EntityCommand? _pendingCommand = null;
        private Being? _commandTarget = null;
        private bool _awaitingLocationSelection = false;

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
            base._PhysicsProcess(delta);
            var hungerNeed = _player?.NeedsSystem?.GetNeed("hunger");

            if (_nameLabel != null && _nameLabel.Text != _player?.Name) _nameLabel.Text = _player?.Name;
            if (_hungerBar != null && hungerNeed != null) _hungerBar.Value = hungerNeed.Value;
        }

        public override void _Input(InputEvent @event)
        {
            // Skip if simulation is paused or essential references are missing
            if (_gameController == null || _player == null) return;

            // Interaction key
            if (@event.IsActionPressed("interact"))
            {
                // no-op
            }
            // UI navigation
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
            // Cancel current command
            else if (@event.IsActionPressed("ui_cancel"))
            {
                CancelCurrentPlayerCommand();
            }
            // Right-click context menu
            else if (@event.IsActionPressed("context_menu") && @event is InputEventMouseButton contextMouseEvent)
            {
                ShowContextMenu(contextMouseEvent);
            }
            // Left-click for movement and interaction
            else if (_dialogueUI?.Visible == false && @event is InputEventMouseButton mouseEvent &&
                     mouseEvent.ButtonIndex == MouseButton.Left &&
                     mouseEvent.Pressed)
            {
                // Handle location selection for commands if active
                if (_awaitingLocationSelection)
                {
                    HandleLocationSelection(mouseEvent);
                }
                else
                {
                    HandleLeftClick();
                }
            }
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

        public Vector2I GetCurrentMouseGridPosition()
        {
            // Get mouse position and convert to world space
            Vector2 worldPos = GetViewport().GetCamera2D().GetGlobalMousePosition();
            // Convert to grid position
            return Grid.Utils.WorldToGrid(worldPos);
        }

        // Handle left clicks for movement and interaction
        private void HandleLeftClick()
        {
            if (_player == null) return;

            Vector2I gridPos = GetCurrentMouseGridPosition();

            // Check if there's an entity at the clicked position
            var entity = GetEntityAtPosition(gridPos);

            if (entity != null && entity != _player)
            {
                // Check if player is already adjacent to entity
                Vector2I playerPos = _player.GetCurrentGridPosition();
                bool isAdjacent = Math.Abs(playerPos.X - gridPos.X) <= 1 &&
                                  Math.Abs(playerPos.Y - gridPos.Y) <= 1;

                if (isAdjacent)
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
                else
                {
                    // Cancel any existing command
                    CancelCurrentPlayerCommand();

                    // Create and assign a command to approach the entity
                    var approachCommand = new MoveToCommand(_player, _player);
                    var pathfinder = _player.GetPathfinder();

                    if (pathfinder != null)
                    {
                        // Let the pathfinder handle finding a path to the entity with proximity of 1
                        pathfinder.SetEntityProximityGoal(_player, entity, 1);
                        approachCommand.WithParameter("usePathfinder", true);
                        _player.AssignCommand(approachCommand);
                        GD.Print($"Moving to approach {entity.Name}");
                    }
                }
            }
            else if (_player.GetGridArea()?.IsCellWalkable(gridPos) == true)
            {
                // Cancel any existing command
                CancelCurrentPlayerCommand();

                // Create and assign a movement command
                var moveCommand = new MoveToCommand(_player, _player);
                moveCommand.WithParameter("targetPos", gridPos);
                _player.AssignCommand(moveCommand);
                GD.Print($"Moving to position {gridPos}");
            }
        }

        // Enhanced context menu with more options
        public void ShowContextMenu(InputEventMouseButton @event)
        {
            if (_contextMenu == null) return;

            var gridPos = GetCurrentMouseGridPosition();

            // Determine what's at the clicked position
            var entity = GetEntityAtPosition(gridPos);
            bool isWalkable = _player?.GetGridArea()?.IsCellWalkable(gridPos) == true;

            _contextMenu.Position = (Vector2I)@event.Position;
            _contextMenu.Clear();

            // Build options based on what's at the clicked position
            if (entity != null && entity != _player)
            {
                // Entity options
                _contextMenu.AddItem("Talk to " + entity.Name);
                _contextMenu.AddItem("Examine " + entity.Name);
            }
            else if (isWalkable)
            {
                // Empty tile options
                _contextMenu.AddItem("Move here");

                // Add build option if appropriate
                if (IsValidBuildLocation(gridPos))
                {
                    _contextMenu.AddItem("Build here");
                }
            }

            // Always add cancel option
            _contextMenu.AddItem("Cancel");

            _contextMenu.Visible = true;
        }

        private void HandleContextMenuSelection(long itemId)
        {
            if (_contextMenu == null || _player == null) return;

            string itemText = _contextMenu.GetItemText((int)itemId);
            Vector2I gridPos = GetCurrentMouseGridPosition();

            switch (itemText)
            {
                case "Move here":
                    if (_player.GetGridArea()?.IsCellWalkable(gridPos) == true)
                    {
                        // Cancel any existing command
                        CancelCurrentPlayerCommand();

                        // Create and assign a movement command
                        var moveCommand = new MoveToCommand(_player, _player);
                        moveCommand.WithParameter("targetPos", gridPos);
                        _player.AssignCommand(moveCommand);
                        GD.Print($"Moving to position {gridPos}");
                    }
                    break;

                case var s when s.StartsWith("Talk to "):
                    var entity = GetEntityAtPosition(gridPos);
                    if (entity != null)
                    {
                        Vector2I playerPos = _player.GetCurrentGridPosition();
                        bool isAdjacent = Math.Abs(playerPos.X - gridPos.X) <= 1 &&
                                         Math.Abs(playerPos.Y - gridPos.Y) <= 1;

                        if (isAdjacent)
                        {
                            // Interact directly
                            var didStartDialogue = _dialogueUI?.ShowDialogue(_player, entity);
                            if (didStartDialogue != true) return;

                            if (_minimap != null && _quickActions != null)
                            {
                                _minimap.Visible = false;
                                _quickActions.Visible = false;
                            }
                            GD.Print($"Interacting with {entity.Name}");
                        }
                        else
                        {
                            // Cancel any existing command
                            CancelCurrentPlayerCommand();

                            // Create and assign a command to approach the entity
                            var approachCommand = new MoveToCommand(_player, _player);
                            var pathfinder = _player.GetPathfinder();

                            if (pathfinder != null)
                            {
                                // Let the pathfinder handle finding a path to the entity with proximity of 1
                                pathfinder.SetEntityProximityGoal(_player, entity, 1);
                                approachCommand.WithParameter("usePathfinder", true);
                                _player.AssignCommand(approachCommand);
                                GD.Print($"Moving to approach {entity.Name}");
                            }
                        }
                    }
                    break;

                case var s when s.StartsWith("Command "):
                    // Future implementation for command menu
                    GD.Print("Command functionality not yet implemented");
                    break;

                case var s when s.StartsWith("Examine "):
                    // Future implementation for examine functionality
                    GD.Print("Examine functionality not yet implemented");
                    break;

                case "Build here":
                    // Future implementation for building
                    GD.Print("Building functionality not yet implemented");
                    break;

                case "Direct control":
                    // Future implementation for direct control
                    GD.Print("Direct control functionality not yet implemented");
                    break;

                case "Cancel":
                    // Do nothing, just close the menu
                    break;
            }

            _contextMenu.Visible = false;
        }

        // Handle selection of a location for commands like MoveToCommand or GuardCommand
        private void HandleLocationSelection(InputEventMouseButton mouseEvent)
        {
            Vector2I gridPos = GetCurrentMouseGridPosition();

            // Check if the position is valid
            var gridArea = _commandTarget?.GetGridArea();
            if (gridArea != null && gridArea.IsCellWalkable(gridPos))
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

        private bool IsValidBuildLocation(Vector2I position)
        {
            // This will be expanded later with more sophisticated checks
            var gridArea = _player?.GetGridArea();
            return gridArea != null && gridArea.IsCellWalkable(position);
        }

        // Cancel the player's current command if any
        private void CancelCurrentPlayerCommand()
        {
            if (_player == null) return;

            // Create and assign a cancel command
            var cancelCommand = new CancelCommand(_player, _player);
            _player.AssignCommand(cancelCommand);
            GD.Print("Canceled current player command");
        }
    }
}
