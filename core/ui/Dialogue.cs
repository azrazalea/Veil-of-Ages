using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities;

namespace VeilOfAges.UI
{
    public partial class Dialogue : PanelContainer
    {
        [Export] private RichTextLabel? _nameLabel;
        [Export] private RichTextLabel? _dialogueText;
        [Export] private HFlowContainer? _optionsContainer;
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

        /// <summary>
        /// Process the selected option
        /// Possible outcomes for dialogue text:
        /// 1. If data isn't present then we do nothing
        //  2. The command associated with the option is refused, command is not assigned, and we return the failure response
        /// 3. The command associated with the option is accepted, command is assigned, and we return success response
        /// 4. There is no command associated with the option, so we just return the successful response.
        /// 
        /// Possible outcomes for dialogue choices:
        /// 1. If GenerateFollowUpOptions returns nothing then we close the Dialog (this typically happens with the player says goodbye)
        /// 2. Otherwise we refresh the options with whatever GenerateFollowupOptions returns.
        /// </summary>
        /// <param name="option">The option that was selected</param>
        private void OnOptionSelected(DialogueOption option)
        {
            if (_currentSpeaker == null || _currentTarget == null) return;
            // Check if command is valid for entity and process it
            bool accepted = option.Command == null ||
                           _dialogueController.ProcessCommand(_currentTarget, option.Command);

            // Update dialogue text based on acceptance
            if (_dialogueText != null) _dialogueText.Text = accepted ? option.SuccessResponse(_currentTarget) : option.FailureResponse(_currentTarget);

            // Generate new options based on the new state
            _currentOptions = _dialogueController.GenerateFollowUpOptions(_currentSpeaker, _currentTarget, option);

            if (_currentOptions.Count == 0)
            {
                Close();
            }

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
