using System.Collections.Generic;
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
            new ("Tell me about yourself.", null, GetEntityDescription(entity), isSimpleOption: true),

            new ("Follow me.", new FollowCommand(entity, speaker)),
            new ("Move to a location.", new MoveToCommand(entity, speaker)),
            new ("Guard an area.", new GuardCommand(entity, speaker)),
            new ("Return home.", new ReturnHomeCommand(entity, speaker)),

            // Close dialogue option
            new ("Goodbye.", null, "Farewell.", "Farewell.")
        };

        entity.AddDialogueOptions(speaker, options);

        return options;
    }

    // Generate follow-up options after a command
    public static List<DialogueOption> GenerateFollowUpOptions(Being speaker, Being entity, DialogueOption previousOption)
    {
        // If the previous command was "Goodbye", return an empty list to close the dialogue
        if (previousOption.Text == "Goodbye.")
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
