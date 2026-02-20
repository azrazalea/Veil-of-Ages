using System.Collections.Generic;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Skills;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Entities.WorkOrders;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Entities;

/// <summary>
/// Interaction handler for the necromancy altar facility.
/// Provides "Get Corpse" and "Raise Zombie" options with context-sensitive
/// enabled/disabled states and reasons.
/// </summary>
public class NecromancyAltarInteraction : IFacilityInteractable
{
    private readonly Facility _facility;

    public string FacilityDisplayName => "Necromancy Altar";
    public Facility Facility => _facility;

    public NecromancyAltarInteraction(Facility facility)
    {
        _facility = facility;
    }

    public List<FacilityDialogueOption> GetInteractionOptions(Being interactor)
    {
        var options = new List<FacilityDialogueOption>();
        var gameTime = interactor.GameController?.CurrentGameTime ?? new GameTime(0);
        bool isNight = gameTime.CurrentDayPhase == DayPhaseType.Night;

        // === Get Corpse option ===
        options.Add(CreateGetCorpseOption(interactor, isNight));

        // === Raise Zombie option ===
        options.Add(CreateRaiseZombieOption(interactor, isNight));

        // === Cancel ===
        options.Add(new FacilityDialogueOption("Cancel"));

        return options;
    }

    private FacilityDialogueOption CreateGetCorpseOption(Being interactor, bool isNight)
    {
        // Check conditions and build disabled reason
        if (!isNight)
        {
            return new FacilityDialogueOption("Get Corpse", null, false, "Only at night");
        }

        // Check if altar already has a corpse (check storage)
        var altarStorage = _facility.SelfAsEntity().GetTrait<StorageTrait>();
        if (altarStorage != null && altarStorage.FindItemByTag("corpse") != null)
        {
            return new FacilityDialogueOption("Get Corpse", null, false, "Corpse already on altar");
        }

        // Check if the entity remembers the graveyard being empty
        // If they have a recent memory of the graveyard AND it had no corpses, disable
        // If they have no memory or expired memory, enable (they'll go check)
        bool remembersGraveyardEmpty = false;
        if (interactor.Memory != null)
        {
            // Find graveyard buildings and check if any remembered storage has corpses
            var graveyardBuildings = interactor.GetAllBuildingsOfType("Graveyard");
            bool hasAnyGraveyardMemory = false;
            bool remembersCorpses = false;

            foreach (var graveyardRef in graveyardBuildings)
            {
                if (graveyardRef.Building == null)
                {
                    continue;
                }

                // Storage observations are keyed by Facility — look up the storage facility first
                var storageFacility = graveyardRef.Building.GetDefaultRoom()?.GetStorageFacility();
                if (storageFacility == null)
                {
                    continue;
                }

                var observation = interactor.Memory.RecallStorageContents(storageFacility);
                if (observation != null)
                {
                    hasAnyGraveyardMemory = true;
                    if (observation.HasItemWithTag("corpse"))
                    {
                        remembersCorpses = true;
                        break;
                    }
                }
            }

            // Only disable if we have RECENT memory confirming empty
            if (hasAnyGraveyardMemory && !remembersCorpses)
            {
                remembersGraveyardEmpty = true;
            }
        }

        if (remembersGraveyardEmpty)
        {
            return new FacilityDialogueOption("Get Corpse", null, false, "You remember the graveyard being empty");
        }

        // Enabled - create FetchCorpseCommand when selected
        return new FacilityDialogueOption("Get Corpse", facilityAction: entity =>
        {
            var command = new FetchCorpseCommand(entity, entity);
            command.WithParameter("altarFacility", _facility);
            entity.AssignCommand(command);
        });
    }

    private FacilityDialogueOption CreateRaiseZombieOption(Being interactor, bool isNight)
    {
        if (!isNight)
        {
            return new FacilityDialogueOption("Raise Zombie", null, false, "Only at night");
        }

        // Check for corpse on altar
        var altarStorage = _facility.SelfAsEntity().GetTrait<StorageTrait>();
        if (altarStorage == null || altarStorage.FindItemByTag("corpse") == null)
        {
            return new FacilityDialogueOption("Raise Zombie", null, false, "No corpse on altar");
        }

        // Check necromancy skill level
        var necromancySkill = interactor.SkillSystem?.GetSkill("necromancy");
        if (necromancySkill == null || necromancySkill.Level < 1)
        {
            return new FacilityDialogueOption("Raise Zombie", null, false, "Need necromancy skill level 1");
        }

        // Check for active work order
        if (_facility.ActiveWorkOrder != null)
        {
            var progress = _facility.ActiveWorkOrder.GetProgressString();
            return new FacilityDialogueOption("Raise Zombie", null, false, $"Already raising a zombie ({progress})");
        }

        // Enabled - create RaiseZombieWorkOrder and start it on the altar facility
        return new FacilityDialogueOption("Raise Zombie", facilityAction: entity =>
        {
            var workOrder = new RaiseZombieWorkOrder
            {
                SpawnFacility = _facility
            };
            _facility.StartWorkOrder(workOrder);
        });
    }
}
