using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.UI;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Core;

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
    [Export]
    private VBoxContainer? _commandQueueContainer;
    [Export]
    private Label? _activityLabel;
    [Export]
    private Label? _timeLabel;
    [Export]
    private Label? _dateLabel;
    [Export]
    private Label? _speedLabel;
    private EntityCommand? _pendingCommand;
    private Being? _commandTarget;
    private Vector2I _contextGridPos;
    private bool _awaitingLocationSelection;
    private TransitionPoint? _contextTransitionPoint;
    private IFacilityInteractable? _contextFacilityInteractable;

    public override void _Ready()
    {
        _gameController = GetNode<GameController>("/root/World/GameController");
        _player = GetNode<Player>("/root/World/Entities/Player");

        if (_gameController == null || _player == null)
        {
            Log.Error("PlayerInputController: Failed to find required nodes!");
        }
    }

    [Export]
    private ProgressBar? _energyBar;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        var hungerNeed = _player?.NeedsSystem?.GetNeed("hunger");
        var energyNeed = _player?.NeedsSystem?.GetNeed("energy");

        if (_nameLabel != null && _nameLabel.Text != _player?.Name)
        {
            _nameLabel.Text = _player?.Name;
        }

        // Show current activity (not just assigned command)
        if (_activityLabel != null)
        {
            var activity = _player?.GetCurrentActivity();
            if (activity != null)
            {
                _activityLabel.Text = activity.DisplayName;
            }
            else
            {
                var command = _player?.GetAssignedCommand();
                _activityLabel.Text = command?.DisplayName ?? Tr("ui.hud.IDLE");
            }
        }

        if (_hungerBar != null && hungerNeed != null)
        {
            _hungerBar.Value = hungerNeed.Value;
        }

        if (_energyBar != null && energyNeed != null)
        {
            _energyBar.Value = energyNeed.Value;
        }

        UpdateTimeDisplay();
        PopulateCommandList();
    }

    private void UpdateTimeDisplay()
    {
        if (_gameController == null)
        {
            return;
        }

        var gameTime = _gameController.CurrentGameTime;

        // Update time of day label (e.g., "Early Morning", "Dusk", "Midnight")
        if (_timeLabel != null)
        {
            _timeLabel.Text = CapitalizeFirst(gameTime.GetTimeOfDayDescription());
        }

        // Update date label (e.g., "Day 5, Seedweave")
        if (_dateLabel != null)
        {
            _dateLabel.Text = L.TrFmt("ui.hud.DAY_DATE", gameTime.Day, gameTime.LocalizedMonthName);
        }

        // Update speed label
        if (_speedLabel != null)
        {
            if (_gameController.SimulationPaused())
            {
                _speedLabel.Text = Tr("ui.hud.PAUSED");
            }
            else
            {
                float scale = _gameController.TimeScale;
                _speedLabel.Text = scale >= 1.0f ? $"{scale:0.#}x" : $"{scale:0.##}x";
            }
        }
    }

    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return char.ToUpperInvariant(input[0]) + input[1..];
    }

    public override void _Input(InputEvent @event)
    {
        // Skip if simulation is paused or essential references are missing
        if (_gameController == null || _player == null)
        {
            return;
        }

        // Interaction key
        if (@event.IsActionPressed("interact"))
        {
            // no-op
        }

        // UI navigation
        else if (@event.IsActionPressed("exit"))
        {
            if (_dialogueUI?.Visible == true)
            {
                _dialogueUI?.Close();
            }
            else
            {
                CancelCurrentPlayerCommand();
            }
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

        // Automation toggle
        else if (@event.IsActionPressed("toggle_automation"))
        {
            var automationTrait = _player?.SelfAsEntity().GetTrait<AutomationTrait>();
            automationTrait?.Toggle();
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
        if (_chooseLocationPrompt != null)
        {
            _chooseLocationPrompt.Visible = true;
        }
    }

    public Vector2I GetCurrentMouseGridPosition()
    {
        // Get mouse position and convert to world space
        Godot.Vector2 worldPos = GetViewport().GetCamera2D().GetGlobalMousePosition();

        // Convert to grid position
        return Utils.WorldToGrid(worldPos);
    }

    // Handle left clicks for movement and interaction
    private void HandleLeftClick()
    {
        if (_player == null)
        {
            return;
        }

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
                if (didStartDialogue != true)
                {
                    return;
                }

                if (_minimap != null && _quickActions != null)
                {
                    _minimap.Visible = false;
                    _quickActions.Visible = false;
                }

                Log.Print($"Interacting with {entity.Name}");
            }
            else
            {
                // Create and assign a command to approach the entity
                ApproachEntity(entity);
            }
        }
        else if (_player.GetGridArea()?.IsCellWalkable(gridPos) == true)
        {
            // Create and assign a movement command
            var moveCommand = new MoveToCommand(_player, _player);
            moveCommand.WithParameter("targetPos", gridPos);
            _player.QueueCommand(moveCommand);
            Log.Print($"Moving to position {gridPos}");
        }
    }

    // Enhanced context menu with more options
    private enum ContextAction
    {
        TalkTo,
        Examine,
        MoveHere,
        Enter,
        UseFacility,
        BuildHere,
        Cancel
    }

    private void AddContextItem(string label, ContextAction action)
    {
        int idx = _contextMenu!.ItemCount;
        _contextMenu.AddItem(label);
        _contextMenu.SetItemMetadata(idx, (int)action);
    }

    public void ShowContextMenu(InputEventMouseButton @event)
    {
        if (_contextMenu == null)
        {
            return;
        }

        _contextGridPos = GetCurrentMouseGridPosition();
        _contextTransitionPoint = null;
        _contextFacilityInteractable = null;

        // Determine what's at the clicked position
        var entity = GetEntityAtPosition(_contextGridPos);
        bool isWalkable = _player?.GetGridArea()?.IsCellWalkable(_contextGridPos) == true;
        var gridArea = _player?.GetGridArea();

        _contextMenu.Position = (Vector2I)@event.Position;
        _contextMenu.Clear();

        // Build options based on what's at the clicked position
        if (entity != null && entity != _player)
        {
            // Entity options
            AddContextItem(L.TrFmt("ui.context.TALK_TO", entity.Name), ContextAction.TalkTo);
            AddContextItem(L.TrFmt("ui.context.EXAMINE", entity.Name), ContextAction.Examine);
        }
        else
        {
            if (isWalkable)
            {
                AddContextItem(Tr("ui.context.MOVE_HERE"), ContextAction.MoveHere);
            }

            // Check for transition point at this position
            if (gridArea != null)
            {
                _contextTransitionPoint = gridArea.GetTransitionPointAt(_contextGridPos);
                if (_contextTransitionPoint?.LinkedPoint != null)
                {
                    AddContextItem(L.TrFmt("ui.context.ENTER", _contextTransitionPoint.Label), ContextAction.Enter);
                }
            }

            // Check for interactable facility at this position
            _contextFacilityInteractable = FindInteractableFacilityAtPosition(_contextGridPos);
            if (_contextFacilityInteractable != null)
            {
                AddContextItem(L.TrFmt("ui.context.USE", _contextFacilityInteractable.FacilityDisplayName), ContextAction.UseFacility);
            }

            if (isWalkable && IsValidBuildLocation(_contextGridPos))
            {
                AddContextItem(Tr("ui.context.BUILD_HERE"), ContextAction.BuildHere);
            }
        }

        // Always add cancel option
        AddContextItem(Tr("ui.context.CANCEL"), ContextAction.Cancel);

        _contextMenu.Visible = true;
    }

    private IFacilityInteractable? FindInteractableFacilityAtPosition(Vector2I position)
    {
        var gridArea = _player?.GetGridArea();
        if (gridArea == null)
        {
            return null;
        }

        // Search all buildings in the player's current area
        foreach (Node child in gridArea.GetChildren())
        {
            if (child is Building building && building.ContainsPosition(position))
            {
                var interactable = building.GetInteractableFacilityAt(position);
                if (interactable != null)
                {
                    return interactable;
                }
            }
        }

        return null;
    }

    private void HandleContextMenuSelection(long itemId)
    {
        if (_contextMenu == null || _player == null)
        {
            return;
        }

        var action = (ContextAction)_contextMenu.GetItemMetadata((int)itemId).AsInt32();
        var gridPos = _contextGridPos;

        switch (action)
        {
            case ContextAction.MoveHere:
                if (_player.GetGridArea()?.IsCellWalkable(gridPos) == true)
                {
                    var moveCommand = new MoveToCommand(_player, _player);
                    moveCommand.WithParameter("targetPos", gridPos);
                    _player.QueueCommand(moveCommand);
                    Log.Print($"Moving to position {gridPos}");
                }

                break;

            case ContextAction.TalkTo:
                var entity = GetEntityAtPosition(gridPos);
                if (entity != null && entity != _player)
                {
                    Vector2I playerPos = _player.GetCurrentGridPosition();
                    bool isAdjacent = Math.Abs(playerPos.X - gridPos.X) <= 1 &&
                                     Math.Abs(playerPos.Y - gridPos.Y) <= 1;

                    if (isAdjacent)
                    {
                        var didStartDialogue = _dialogueUI?.ShowDialogue(_player, entity);
                        if (didStartDialogue != true)
                        {
                            return;
                        }

                        if (_minimap != null && _quickActions != null)
                        {
                            _minimap.Visible = false;
                            _quickActions.Visible = false;
                        }

                        Log.Print($"Interacting with {entity.Name}");
                    }
                    else
                    {
                        ApproachEntity(entity);
                    }
                }

                break;

            case ContextAction.Enter:
                if (_contextTransitionPoint != null)
                {
                    var transitionActivity = new GoToTransitionActivity(_contextTransitionPoint);
                    _player.SetCurrentActivity(transitionActivity);
                    Log.Print($"Heading to {_contextTransitionPoint.Label}");
                }

                break;

            case ContextAction.UseFacility:
                if (_contextFacilityInteractable != null && _dialogueUI != null)
                {
                    var facility = _contextFacilityInteractable.Facility;
                    var activity = new InteractWithFacilityActivity(
                        facility.Owner, facility.Id, _contextFacilityInteractable, _dialogueUI);
                    _player.SetCurrentActivity(activity);
                }

                break;

            case ContextAction.Examine:
                Log.Print("Examine functionality not yet implemented");
                break;

            case ContextAction.BuildHere:
                Log.Print("Building functionality not yet implemented");
                break;

            case ContextAction.Cancel:
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

                Log.Print($"Command target location set to {gridPos}");
            }
        }
        else
        {
            Log.Print("Invalid location selected");
        }

        // Clear selection state
        _pendingCommand = null;
        _commandTarget = null;
        _awaitingLocationSelection = false;
        if (_chooseLocationPrompt != null)
        {
            _chooseLocationPrompt.Visible = false;
        }
    }

    private bool IsValidBuildLocation(Vector2I position)
    {
        // This will be expanded later with more sophisticated checks
        var gridArea = _player?.GetGridArea();
        return gridArea != null && gridArea.IsCellWalkable(position);
    }

    // Cancel the player's current command and activity (when in manual mode)
    private void CancelCurrentPlayerCommand()
    {
        if (_player == null)
        {
            return;
        }

        _player.AssignCommand(null);

        // In manual mode, also cancel the current activity so the player stops completely
        var automationTrait = _player.SelfAsEntity().GetTrait<AutomationTrait>();
        if (automationTrait != null && !automationTrait.IsAutomated)
        {
            _player.SetCurrentActivity(null);
        }

        Log.Print("Canceled current player command");
    }

    public void PopulateCommandList()
    {
        if (_commandQueueContainer == null)
        {
            return;
        }

        for (int i = _commandQueueContainer.GetChildCount() - 1; i >= 0; i--)
        {
            var child = _commandQueueContainer.GetChild(i);
            _commandQueueContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (_player?.GetCommandQueue().Count == 0)
        {
            var label = new Label
            {
                Text = Tr("ui.hud.EMPTY"),
            };
            label.AddThemeFontSizeOverride("font_size", 10);

            _commandQueueContainer.AddChild(label);
            return;
        }

        LinkedListNode<EntityCommand>? node = _player?.GetCommandQueue().First();
        while (node != null)
        {
            var label = new Label
            {
                Text = node?.Value.DisplayName ?? string.Empty,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText = true,
            };

            label.AddThemeFontSizeOverride("font_size", 10);

            _commandQueueContainer.AddChild(label);
            node = node?.Next;
        }
    }

    public void ApproachEntity(Being entity)
    {
        if (_player == null)
        {
            return;
        }

        var approachCommand = new MoveToCommand(_player, _player);
        approachCommand.WithParameter("targetEntity", entity);
        _player.QueueCommand(approachCommand);
        Log.Print($"Moving to approach {entity.Name}");
    }
}
