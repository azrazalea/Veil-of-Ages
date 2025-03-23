using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities;

namespace VeilOfAges.UI
{
    public partial class Dialogue : PanelContainer
    {
        [Export] private Label? _nameLabel;
        [Export] private RichTextLabel? _dialogueText;
        [Export] private HBoxContainer? _optionsContainer;
        private Being? _currentTarget;
        private Being? _currentSpeaker;
        private List<DialogueOption> _currentOptions = new();
        private DialogueController _dialogueController = new();

        public override void _Ready()
        {
            Visible = false; // Start hidden
        }

        public void ShowDialogue(Being speaker, Being target)
        {
            _currentTarget = target;
            _currentSpeaker = speaker;

            // Update name label
            if (_nameLabel != null) _nameLabel.Text = target.Name;

            // Generate initial dialogue text based on entity type and status
            if (_dialogueText != null) _dialogueText.Text = target.GenerateInitialDialogue(speaker);

            // Generate dialogue options based on the target
            _currentOptions = _dialogueController.GenerateOptionsFor(speaker, target);

            // Create option buttons
            RefreshOptions();

            // Show the dialogue UI
            Visible = true;
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
                Button button = new()
                {
                    Text = option.Text
                };
                button.Pressed += () => OnOptionSelected(option);
                _optionsContainer?.AddChild(button);
            }
        }

        private void OnOptionSelected(DialogueOption option)
        {
            if (_currentSpeaker == null || _currentTarget == null) return;
            // Check if command is valid for entity and process it
            bool accepted = option.Command != null &&
                           _dialogueController.ProcessCommand(_currentSpeaker, _currentTarget, option.Command);

            // Update dialogue text based on acceptance
            if (_dialogueText != null) _dialogueText.Text = accepted ? option.SuccessResponse(_currentTarget) : option.FailureResponse(_currentTarget);

            // Generate new options based on the new state
            _currentOptions = _dialogueController.GenerateFollowUpOptions(_currentSpeaker, _currentTarget, option, accepted);

            // Refresh the option buttons
            RefreshOptions();
        }

        public void Close()
        {
            _currentTarget = null;
            _currentSpeaker = null;
            Visible = false;
        }
    }
}
