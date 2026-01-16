using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.UI;

public partial class Dialogue : CanvasLayer
{
    [Export]
    private RichTextLabel? _nameLabel;
    [Export]
    private RichTextLabel? _dialogueText;
    [Export]
    private GridContainer? _optionsContainer;
    [Export]
    private PanelContainer? _minimap;
    [Export]
    private PanelContainer? _quickActions;

    private Being? _currentTarget;
    private Being? _currentSpeaker;
    private List<DialogueOption> _currentOptions = new ();

    // DialogueController is used via static methods, instance not needed
    // private readonly DialogueController _dialogueController = new ();
    public override void _Ready()
    {
        Visible = false; // Start hidden
    }

    public bool ShowDialogue(Being speaker, Being target)
    {
        if (target.WillRefuseCommand(new TalkCommand(target, speaker)))
        {
            Log.Print($"{target.Name} does not want to talk right now.");
            return false; // The target doesn't want to talk right now

            // TODO: Tell the player about this response.
        }

        _currentTarget = target;
        _currentSpeaker = speaker;

        // Get the target's attention
        _currentTarget.BeginDialogue(_currentSpeaker);

        // Update name label
        if (_nameLabel != null)
        {
            _nameLabel.Text = target.Name;
        }

        // Generate initial dialogue text based on entity type and status
        if (_dialogueText != null)
        {
            _dialogueText.Text = target.GenerateInitialDialogue(speaker);
        }

        // Generate dialogue options based on the target
        _currentOptions = DialogueController.GenerateOptionsFor(speaker, target);

        // Create option buttons
        RefreshOptions();

        // Show the dialogue UI
        Visible = true;

        return true;
    }

    private void RefreshOptions()
    {
        // Clear existing options
        foreach (Node child in _optionsContainer?.GetChildren() ?? [])
        {
            child.QueueFree();
        }

        // Create new option buttons
        foreach (var option in _currentOptions)
        {
            Button button = new ()
            {
                Text = option.Text
            };

            // Disable commands that the entity will refuse
            if (option.Command != null && _currentTarget?.WillRefuseCommand(option.Command) != false)
            {
                button.Disabled = true;
                button.TooltipText = "I will refuse this command.";
            }

            button.Pressed += () => OnOptionSelected(option);
            _optionsContainer?.AddChild(button);
        }
    }

    /// <summary>
    /// Process the selected option
    /// Possible outcomes for dialogue text:
    /// 1. If data isn't present then we do nothing.
    // 2. The command associated with the option is refused, command is not assigned, and we return the failure response
    // 3. The command associated with the option is accepted, command is assigned, and we return success response
    // 4. There is no command associated with the option, so we just return the successful response.
    //
    // Possible outcomes for dialogue choices:
    // 1. If GenerateFollowUpOptions returns nothing then we close the Dialog (this typically happens with the player says goodbye)
    // 2. Otherwise we refresh the options with whatever GenerateFollowupOptions returns.
    // </summary>
    // <param name="option">The option that was selected</param>
    private void OnOptionSelected(DialogueOption option)
    {
        if (_currentSpeaker == null || _currentTarget == null)
        {
            return;
        }

        // Check if command is valid for entity and process it
        bool accepted = option.Command == null ||
                       DialogueController.ProcessCommand(_currentTarget, option.Command);

        // Handle commands that need location selection
        if (option.Command is MoveToCommand or GuardCommand)
        {
            // Get player input controller
            var inputController = GetNode<PlayerInputController>("/root/World/HUD/PlayerInputController");
            if (inputController != null)
            {
                inputController.StartLocationSelection(option.Command, _currentTarget);

                Close();
                return;
            }
        }

        // End dialogue if we've assigned a command
        if (option.Command != null && accepted)
        {
            Close();
            return;
        }

        // Update dialogue text based on acceptance
        if (_dialogueText != null)
        {
            _dialogueText.Text = accepted ? option.SuccessResponse(_currentTarget) : option.FailureResponse(_currentTarget);
        }

        // Generate new options based on the new state
        _currentOptions = DialogueController.GenerateFollowUpOptions(_currentSpeaker, _currentTarget, option);

        if (_currentOptions.Count == 0)
        {
            Close();
            return;
        }

        // Refresh the option buttons
        RefreshOptions();
    }

    public void Close()
    {
        _currentTarget?.EndDialogue(_currentSpeaker);

        _currentTarget = null;
        _currentSpeaker = null;
        Visible = false;
        if (_minimap != null && _quickActions != null)
        {
            _minimap.Visible = true;
            _quickActions.Visible = true;
        }
    }
}
