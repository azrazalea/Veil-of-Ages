using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.UI
{
    public class DialogueController
    {
        // Generate initial dialogue options based on the entity type
        public List<DialogueOption> GenerateOptionsFor(Being speaker, Being entity)
        {
            var options = new List<DialogueOption>
            {
                // Common options for all entities
                new DialogueOption("Tell me about yourself.", null, GetEntityDescription(entity));

                // Close dialogue option
                new DialogueOption("Goodbye.", new CancelCommand(entity, speaker), "Farewell.", "Farewell.")
            };

            entity.AddDialogueOptions(speaker, options);

            return options;
        }

        // Generate follow-up options after a command
        public List<DialogueOption> GenerateFollowUpOptions(Being speaker, Being entity, DialogueOption previousOption, bool wasAccepted)
        {
            // If the previous command was "Goodbye", return an empty list to close the dialogue
            if (previousOption.Command?.Type == EntityCommand.CommandType.Cancel)
            {
                return new List<DialogueOption>();
            }

            // Otherwise, regenerate options (possibly with context from the previous option)
            return GenerateOptionsFor(speaker, entity);
        }

        // Process a command for an entity
        public bool ProcessCommand(Being speaker, Being entity, EntityCommand command)
        {
            return entity.AssignCommand(command);
        }

        private string GetEntityDescription(Being entity)
        {
            return entity.GetDialogueDescription();
        }

        // Command implementation methods
        private bool AssignFollowAction(Being entity)
        {
            // Find player entity
            Player player = null;
            if (entity.GetTree().GetFirstNodeInGroup("World") is World world)
            {
                player = world.GetNode<Player>("Entities/Player");
            }

            if (player == null)
            {
                return false;
            }


            // Add FollowTrait to entity or update existing trait
            if (entity.CurrentCommand)
            {
                var followTrait = entity.selfAsEntity().GetTrait<FollowTrait>();
                followTrait.SetTarget(player);
                followTrait.Activate();
            }
            else
            {
                var followTrait = new FollowTrait();
                entity.selfAsEntity().AddTrait(followTrait);
                followTrait.Initialize(entity, entity.Health);
                followTrait.SetTarget(player);
                followTrait.Activate();
            }

            return true;
        }

        private bool AssignGuardAction(Being entity, Vector2I position)
        {
            // Add or update GuardTrait
            if (entity.selfAsEntity().HasTrait<GuardTrait>())
            {
                var guardTrait = entity.selfAsEntity().GetTrait<GuardTrait>();
                guardTrait.SetPosition(position);
                guardTrait.Activate();
            }
            else
            {
                var guardTrait = new GuardTrait();
                entity.selfAsEntity().AddTrait(guardTrait);
                guardTrait.Initialize(entity, entity.Health);
                guardTrait.SetPosition(position);
                guardTrait.Activate();
            }

            return true;
        }

        private bool AssignMoveAction(Being entity, Vector2I position)
        {
            // Create an EntityAction for movement
            // This will be a one-time action rather than a persistent trait
            if (entity.GetTree().GetFirstNodeInGroup("World") is World world)
            {
                var gameController = world.GetNode<Core.GameController>("GameController");
                if (gameController != null)
                {
                    // Create move action and queue it
                    var moveAction = new MoveToPositionAction(entity, position);

                    // If entity has a trait that needs to be deactivated, do so
                    if (entity.selfAsEntity().HasTrait<FollowTrait>())
                    {
                        entity.selfAsEntity().GetTrait<FollowTrait>().Deactivate();
                    }

                    if (entity.selfAsEntity().HasTrait<GuardTrait>())
                    {
                        entity.selfAsEntity().GetTrait<GuardTrait>().Deactivate();
                    }

                    // Tell the entity thinking system to execute this action
                    entity.SetNextAction(moveAction);

                    return true;
                }
            }

            return false;
        }

        private bool AssignReturnHomeAction(Being entity)
        {
            // For undead, return to spawn position
            // For villagers, return to their home building
            Vector2I homePosition;

            if (entity.selfAsEntity().HasTrait<UndeadTrait>() &&
                entity.selfAsEntity().HasTrait<MindlessTrait>())
            {
                // Get spawn position from trait
                var trait = entity.selfAsEntity().GetTrait<MindlessTrait>();
                homePosition = trait.GetSpawnPosition();
            }
            else if (entity.selfAsEntity().HasTrait<VillagerTrait>())
            {
                // Get home position from trait
                var trait = entity.selfAsEntity().GetTrait<VillagerTrait>();
                homePosition = trait.GetHomePosition();
            }
            else
            {
                // Default to current position if no home is defined
                homePosition = entity.GetCurrentGridPosition();
            }

            // Create move action to the home position
            return AssignMoveAction(entity, homePosition);
        }

    }
}
