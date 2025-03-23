using System;
using System.Collections.Generic;
using Godot;
using System.Linq;

namespace VeilOfAges.Entities.Beings.Health
{
    public class BodyHealth(Being owner)
    {
        public Dictionary<string, BodyPartGroup> BodyPartGroups { get; private set; } = [];
        public Dictionary<BodySystemType, BodySystem> BodySystems { get; private set; } = [];

        public bool BodyStructureInitialized = false;
        public string[] SoftTissuesAndOrgans = [
            "Stomach", "Heart", "Lungs", "Kidneys", "Liver", "Eyes", "Nose", "Gonads", "Genitals"
        ];

        private readonly Being _owner = owner;

        public void AddBodyPartToGroup(string groupName, BodyPart bodyPart)
        {
            if (BodyPartGroups.TryGetValue(groupName, out BodyPartGroup? value))
            {
                value.AddPart(bodyPart);
            }
            else
            {
                GD.Print($"Failed to add body part {bodyPart.Name} to group {groupName} as group did not exist");
            }
        }

        public void RemoveBodyPartFromGroup(string groupName, string bodyPartName)
        {
            if (BodyPartGroups.TryGetValue(groupName, out BodyPartGroup? value))
            {
                value.RemovePart(bodyPartName);
            }
            else
            {
                GD.Print($"Failed to remove body part {bodyPartName} from group {groupName} as group did not exist");
            }
        }

        public void RemoveSoftTissuesAndOrgans()
        {
            foreach (var group in BodyPartGroups.Values)
            {
                group.Parts.RemoveAll(p => SoftTissuesAndOrgans.Contains(p.Name));
            }
        }

        public void DisableBodySystem(BodySystemType systemType)
        {
            if (BodySystems.TryGetValue(systemType, out var system))
            {
                system.Disable();
            }
            else
            {
                GD.Print($"Failed to disable body system {systemType}");
            }
        }

        public float GetSystemEfficiency(BodySystemType systemType)
        {
            if (systemType == BodySystemType.Pain)
            {
                return CalculatePain();
            }

            if (!BodySystems.TryGetValue(systemType, out var system))
            {
                return 0.0f;
            }

            if (system.Disabled)
            {
                return 1.0f;
            }

            float totalEfficiency = 0.0f;
            float totalWeight = 0.0f;

            foreach (var contributor in system.GetContributors())
            {
                string partName = contributor.Key;
                float weight = contributor.Value;

                // Find the part in body part groups
                BodyPart? part = FindBodyPart(partName);

                if (part != null && part.Status != BodyPartStatus.Destroyed && part.Status != BodyPartStatus.Missing)
                {
                    totalEfficiency += part.GetEfficiency() * weight;
                    totalWeight += weight;
                }
            }

            // Return efficiency as a percentage where 1.0 = 100%
            return totalWeight > 0 ? totalEfficiency / totalWeight : 0.0f;
        }

        private float CalculatePain()
        {
            if (BodySystems[BodySystemType.Pain].Disabled)
            {
                return 0.0f;
            }

            float totalPain = 0.0f;
            float maxPossiblePain = 0.0f;

            foreach (var group in BodyPartGroups.Values)
            {
                foreach (var part in group.Parts)
                {
                    // Pain increases as health decreases, multiplied by pain sensitivity
                    float healthLoss = 1.0f - (part.CurrentHealth / part.MaxHealth);
                    totalPain += healthLoss * part.PainSensitivity * part.Importance;
                    maxPossiblePain += part.PainSensitivity * part.Importance;
                }
            }

            // Cap pain at 1.0 (100%)
            return Math.Min(1.0f, totalPain / maxPossiblePain);
        }

        // Helper method to find a body part by name
        private BodyPart? FindBodyPart(string partName)
        {
            foreach (var group in BodyPartGroups.Values)
            {
                foreach (var part in group.Parts)
                {
                    if (part.Name == partName)
                    {
                        return part;
                    }
                }
            }

            return null;
        }

        // Get a description of a system's status
        public string GetSystemStatus(BodySystemType systemType)
        {
            float efficiency = GetSystemEfficiency(systemType);

            if (BodySystems[systemType].Disabled)
            {
                return "Disabled";
            }

            if (systemType == BodySystemType.Pain)
            {
                if (efficiency >= 0.8f) return "Extreme pain";
                if (efficiency >= 0.6f) return "Severe pain";
                if (efficiency >= 0.4f) return "Significant pain";
                if (efficiency >= 0.2f) return "Moderate pain";
                if (efficiency > 0.0f) return "Minor pain";
                return "No pain";
            }

            if (efficiency <= 0.0f) return "None";
            if (efficiency < 0.25f) return "Extremely poor";
            if (efficiency < 0.5f) return "Poor";
            if (efficiency < 0.75f) return "Weakened";
            if (efficiency < 1.0f) return "Slightly impaired";
            if (efficiency == 1.0f) return "Normal";
            return "Enhanced"; // For values above 100%
        }

        public void PrintSystemStatuses()
        {
            GD.Print($"Health status for {_owner.Name}");
            foreach (var system in BodySystems.Values)
            {
                GD.Print($"{system.Name} => {GetSystemStatus(system.Type)}");
            }
        }

        public void InitializeBodySystems()
        {
            // Define all body systems
            var consciousness = new BodySystem(BodySystemType.Consciousness, "Consciousness", true);
            consciousness.AddContributor("Brain", 1.0f);

            var sight = new BodySystem(BodySystemType.Sight, "Sight", false);
            sight.AddContributor("Eyes", 1.0f);

            var hearing = new BodySystem(BodySystemType.Hearing, "Hearing", false);
            hearing.AddContributor("Ears", 1.0f);

            var smell = new BodySystem(BodySystemType.Smell, "Smell", false);
            smell.AddContributor("Nose", 1.0f);

            var moving = new BodySystem(BodySystemType.Moving, "Moving", false);
            moving.AddContributor("Legs", 0.7f);
            moving.AddContributor("Feet", 0.3f);

            var manipulation = new BodySystem(BodySystemType.Manipulation, "Manipulation", false);
            manipulation.AddContributor("Arms", 0.4f);
            manipulation.AddContributor("Hands", 0.6f);

            var talking = new BodySystem(BodySystemType.Talking, "Talking", false);
            talking.AddContributor("Jaw", 0.7f);
            talking.AddContributor("Neck", 0.3f);

            var communication = new BodySystem(BodySystemType.Communication, "Communication", false);
            communication.AddContributor("Jaw", 0.5f);
            communication.AddContributor("Neck", 0.5f);

            var breathing = new BodySystem(BodySystemType.Breathing, "Breathing", true);
            breathing.AddContributor("Lungs", 1.0f);

            var bloodFiltration = new BodySystem(BodySystemType.BloodFiltration, "Blood Filtration", true);
            bloodFiltration.AddContributor("Kidneys", 0.7f);
            bloodFiltration.AddContributor("Liver", 0.3f);

            var bloodPumping = new BodySystem(BodySystemType.BloodPumping, "Blood Pumping", true);
            bloodPumping.AddContributor("Heart", 1.0f);

            var digestion = new BodySystem(BodySystemType.Digestion, "Digestion", true);
            digestion.AddContributor("Stomach", 1.0f);

            var pain = new BodySystem(BodySystemType.Pain, "Pain", false);
            // Pain is calculated differently - all parts contribute based on their pain sensitivity

            // Add all systems to the dictionary
            BodySystems[BodySystemType.Consciousness] = consciousness;
            BodySystems[BodySystemType.Sight] = sight;
            BodySystems[BodySystemType.Hearing] = hearing;
            BodySystems[BodySystemType.Smell] = smell;
            BodySystems[BodySystemType.Moving] = moving;
            BodySystems[BodySystemType.Manipulation] = manipulation;
            BodySystems[BodySystemType.Talking] = talking;
            BodySystems[BodySystemType.Communication] = communication;
            BodySystems[BodySystemType.Breathing] = breathing;
            BodySystems[BodySystemType.BloodFiltration] = bloodFiltration;
            BodySystems[BodySystemType.BloodPumping] = bloodPumping;
            BodySystems[BodySystemType.Digestion] = digestion;
            BodySystems[BodySystemType.Pain] = pain;
        }

        public void InitializeHumanoidBodyStructure()
        {
            // Create torso group
            var torso = new BodyPartGroup("Torso");
            torso.AddParts([
                new BodyPart("Clavicles", 30, 0.3f),
                new BodyPart("Sternum", 30, 0.4f),
                new BodyPart("Ribs", 50, 0.6f),
                new BodyPart("Pelvis", 50, 0.7f),
                new BodyPart("Spine", 60, 0.9f),
                new BodyPart("Stomach", 40, 0.7f),
                new BodyPart("Heart", 30, 1.0f),
                new BodyPart("Lungs", 40, 0.8f),
                new BodyPart("Kidneys", 30, 0.6f),
                new BodyPart("Liver", 40, 0.7f),
                new BodyPart("Neck", 30, 0.8f),
                new BodyPart("Gonads", 20, 0.3f),
                new BodyPart("Genitals", 20, 0.3f)
            ]);

            // Create UpperHead group
            var upperHead = new BodyPartGroup("UpperHead");
            upperHead.AddParts([
                new BodyPart("Head", 40, 0.9f),
                new BodyPart("Skull", 50, 0.8f),
                new BodyPart("Brain", 30, 1.0f),
                new BodyPart("Ears", 20, 0.4f)
            ]);

            // Create FullHead group
            var fullHead = new BodyPartGroup("FullHead");
            fullHead.AddParts([
                new BodyPart("Head", 40, 0.9f),
                new BodyPart("Skull", 50, 0.8f),
                new BodyPart("Brain", 30, 1.0f),
                new BodyPart("Eyes", 20, 0.7f),
                new BodyPart("Ears", 20, 0.4f),
                new BodyPart("Nose", 15, 0.3f),
                new BodyPart("Jaw", 25, 0.5f)
            ]);

            // Create Shoulders group
            var shoulders = new BodyPartGroup("Shoulders");
            shoulders.AddPart(new BodyPart("Shoulders", 40, 0.5f));

            // Create Arms group
            var arms = new BodyPartGroup("Arms");
            arms.AddParts([
                new BodyPart("Arms", 40, 0.6f),
                new BodyPart("Hands", 30, 0.5f),
                new BodyPart("Humeri", 35, 0.5f),
                new BodyPart("Radii", 30, 0.4f)
            ]);

            // Create Hands group
            var hands = new BodyPartGroup("Hands");
            hands.AddParts([
                new BodyPart("Hands", 30, 0.5f),
                new BodyPart("Fingers", 20, 0.4f)
            ]);

            // Create LeftHand group
            var leftHand = new BodyPartGroup("LeftHand");
            leftHand.AddParts([
                new BodyPart("Left Hand", 30, 0.5f),
                new BodyPart("Left Hand Fingers", 20, 0.4f)
            ]);

            // Create RightHand group
            var rightHand = new BodyPartGroup("RightHand");
            rightHand.AddParts([
                new BodyPart("Right Hand", 30, 0.5f),
                new BodyPart("Right Hand Fingers", 20, 0.4f)
            ]);

            // Create Legs group
            var legs = new BodyPartGroup("Legs");
            legs.AddParts([
                new BodyPart("Legs", 50, 0.7f),
                new BodyPart("Feet", 30, 0.5f),
                new BodyPart("Femurs", 40, 0.6f),
                new BodyPart("Tibiae", 35, 0.5f)
            ]);

            // Create Feet group
            var feet = new BodyPartGroup("Feet");
            feet.AddParts([
                new BodyPart("Feet", 30, 0.5f),
                new BodyPart("Toes", 15, 0.3f)
            ]);

            // Add all groups to the dictionary
            BodyPartGroups["Torso"] = torso;
            BodyPartGroups["UpperHead"] = upperHead;
            BodyPartGroups["FullHead"] = fullHead;
            BodyPartGroups["Shoulders"] = shoulders;
            BodyPartGroups["Arms"] = arms;
            BodyPartGroups["Hands"] = hands;
            BodyPartGroups["LeftHand"] = leftHand;
            BodyPartGroups["RightHand"] = rightHand;
            BodyPartGroups["Legs"] = legs;
            BodyPartGroups["Feet"] = feet;
            BodyStructureInitialized = true;
        }
    }
}
