using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.UI;

public static class DialogueController
{
    // Generate initial dialogue options based on the entity type
    public static List<DialogueOption> GenerateOptionsFor(Being speaker, Being entity)
    {
        var options = new List<DialogueOption>
        {
            // Common options for all entities
            new (L.Tr("dialogue.TELL_ABOUT_YOURSELF"), null, GetEntityDescription(entity), isSimpleOption: true),

            new (L.Tr("dialogue.FOLLOW_ME"), new FollowCommand(entity, speaker)),
            new (L.Tr("dialogue.MOVE_TO_LOCATION"), new MoveToCommand(entity, speaker)),
            new (L.Tr("dialogue.GUARD_AREA"), new GuardCommand(entity, speaker)),
            new (L.Tr("dialogue.RETURN_HOME"), new ReturnHomeCommand(entity, speaker)),

            // Close dialogue option
            new (L.Tr("dialogue.GOODBYE"), null, L.Tr("dialogue.FAREWELL"), L.Tr("dialogue.FAREWELL"))
        };

        entity.AddDialogueOptions(speaker, options);

        return options;
    }

    // Generate follow-up options after a command
    public static List<DialogueOption> GenerateFollowUpOptions(Being speaker, Being entity, DialogueOption previousOption)
    {
        // If the previous command was "Goodbye", return an empty list to close the dialogue
        if (previousOption.Text == L.Tr("dialogue.GOODBYE"))
        {
            return [];
        }

        // Otherwise, regenerate options (possibly with context from the previous option)
        return GenerateOptionsFor(speaker, entity);
    }

    // Process a command for an entity
    public static bool ProcessCommand(Being entity, EntityCommand command)
    {
        return entity.AssignCommand(command);
    }

    private static string GetEntityDescription(Being entity)
    {
        return entity.GetDialogueDescription();
    }
}
