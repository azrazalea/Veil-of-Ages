using System;
using VeilOfAges.Entities;

namespace VeilOfAges.UI;

public class DialogueOption
{
    /// <summary>
    /// Gets text to display on the dialogue button.
    /// </summary>
    public string Text { get; private set; }

    /// <summary>
    /// Gets what command should be assigned to the Being spoken to upon clicking the button.
    /// </summary>
    public EntityCommand? Command { get; private set; }

    /// <summary>
    /// A default successful response for this option in case the Being does not have a specific one.
    /// </summary>
    private readonly string _defaultSuccessResponse;

    /// <summary>
    /// A default failed response for this option in case the Being does not have a specific one.
    /// </summary>
    private readonly string _defaultFailureResponse;

    /// <summary>
    /// Is this option one that does not require entity.GetSuccessReponse and entity.GetFailureResponse to be called?
    /// This is typically used when we can get the information for the response ourselves.
    /// </summary>
    private readonly bool _isSimpleOption;

    /// <summary>
    /// Gets the reason this option is disabled, if any. Shown as tooltip text.
    /// </summary>
    public string? DisabledReason { get; private set; }

    /// <summary>
    /// Gets a value indicating whether gets whether this option is explicitly disabled (e.g., facility conditions not met).
    /// </summary>
    public bool IsExplicitlyDisabled { get; private set; }

    /// <summary>
    /// Gets the callback action for facility options that don't use the standard command system.
    /// </summary>
    public Action<Being>? FacilityAction { get; private set; }

    public DialogueOption(string text, EntityCommand? command = null, string successResponse = "Okay", string failureResponse = "No",
                            bool isSimpleOption = false)
    {
        Text = text;
        Command = command;
        _defaultSuccessResponse = successResponse;
        _defaultFailureResponse = failureResponse;
        _isSimpleOption = isSimpleOption;
    }

    /// <summary>
    /// Create a dialogue option with explicit disabled state and reason.
    /// Used for facility interaction options.
    /// </summary>
    public static DialogueOption CreateFacilityOption(string text, EntityCommand? command = null,
        bool enabled = true, string? disabledReason = null, Action<Being>? facilityAction = null)
    {
        return new DialogueOption(text, command, isSimpleOption: true)
        {
            IsExplicitlyDisabled = !enabled,
            DisabledReason = disabledReason,
            FacilityAction = facilityAction
        };
    }

    public bool IsAvailableFor(Being entity)
    {
        return entity.IsOptionAvailable(this);
    }

    public string SuccessResponse(Being entity)
    {
        if (_isSimpleOption)
        {
            return _defaultSuccessResponse;
        }

        if (Command != null)
        {
            return entity.GetSuccessResponse(Command) ?? _defaultSuccessResponse;
        }
        else
        {
            return entity.GetSuccessResponse(Text) ?? _defaultSuccessResponse;
        }
    }

    public string FailureResponse(Being entity)
    {
        if (_isSimpleOption)
        {
            return _defaultFailureResponse;
        }

        if (Command != null)
        {
            return entity.GetFailureResponse(Command) ?? _defaultFailureResponse;
        }
        else
        {
            return entity.GetFailureResponse(Text) ?? _defaultSuccessResponse;
        }
    }
}
