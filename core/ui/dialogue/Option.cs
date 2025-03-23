

using System;
using VeilOfAges.Entities;

namespace VeilOfAges.UI
{
    public class DialogueOption
    {
        public string Text { get; private set; }
        public EntityCommand? Command { get; private set; }
        private string _defaultSuccessResponse;
        private string _defaultFailureResponse;

        public DialogueOption(string text, EntityCommand? command = null, string successResponse = "Okay", string failureResponse = "No")
        {
            Text = text;
            Command = command;
            _defaultSuccessResponse = successResponse;
            _defaultFailureResponse = failureResponse;
        }

        public bool IsAvailableFor(Being entity)
        {
            return entity.IsOptionAvailable(this);
        }

        public string SuccessResponse(Being entity)
        {
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
}
