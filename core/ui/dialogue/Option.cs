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

    public DialogueOption(string text, EntityCommand? command = null, string successResponse = "Okay", string failureResponse = "No",
                            bool isSimpleOption = false)
    {
        Text = text;
        Command = command;
        _defaultSuccessResponse = successResponse;
        _defaultFailureResponse = failureResponse;
        _isSimpleOption = isSimpleOption;
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
