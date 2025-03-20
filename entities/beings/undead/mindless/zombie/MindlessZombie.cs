using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities.Traits;
using System.Linq;

namespace VeilOfAges.Entities.Beings
{
    public partial class MindlessZombie : Being
    {
        public override BeingAttributes DefaultAttributes { get; } = new(
            12.0f,
            6.0f,
            14.0f,
            3.0f,
            8.0f,
            4.0f,
            2.0f
        );
        public override void _Ready()
        {

            // Configure zombie specific properties
            _totalMoveTicks = 10; // Zombies are slow

            // Add zombie traits
            selfAsEntity().AddTrait<UndeadTrait>();
            selfAsEntity().AddTrait<MindlessTrait>();

            // Optional: You can now customize specific settings for each trait
            if (selfAsEntity().GetTrait<MindlessTrait>() is MindlessTrait mindlessTrait)
            {
                mindlessTrait.WanderProbability = 0.3f; // Zombies wander more often
                mindlessTrait.WanderRange = 15f; // And further from spawn
            }

            base._Ready();
            GD.Print("Zombie initialized with traits");
        }

        public override void Initialize(Grid.Area gridArea, Vector2I startGridPos, BeingAttributes attributes = null)
        {
            base.Initialize(gridArea, startGridPos, attributes);
            ApplyRandomDecayDamage();

            // Any zombie-specific initialization after base initialization
            GD.Print($"Zombie spawned at {startGridPos}");
            Health.PrintSystemStatuses();
        }

        // Custom zombie behavior overrides (if needed)
        public override void _PhysicsProcess(double delta)
        {
            // Let the traits and base class handle most behavior
            base._PhysicsProcess(delta);

            // Add any zombie-specific behavior here if needed
            // For example, we could make zombies occasionally groan
            if (IsMoving() == true && new RandomNumberGenerator().RandfRange(0f, 1f) < 0.01f)
            {
                GD.Print($"{Name}: *groans hungrily*");
            }
        }

        private void ApplyRandomDecayDamage()
        {
            // Simulate zombie decay with random damage
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            // Number of parts to damage
            int partsToAffect = rng.RandiRange(2, 5);
            int affected = 0;

            // List of eligible groups/parts
            var candidates = new List<(string groupName, string partName)>();

            // Collect all body parts
            foreach (var group in _bodyPartGroups)
            {
                foreach (var part in group.Value.Parts)
                {
                    // Skip vital parts like brain and heart for playability
                    if (part.Name != "Brain" && part.Name != "Heart" && part.Name != "Spine")
                    {
                        candidates.Add((group.Key, part.Name));
                    }
                }
            }

            // Shuffle candidates
            for (int i = 0; i < candidates.Count; i++)
            {
                int swapIndex = rng.RandiRange(0, candidates.Count - 1);
                var temp = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temp;
            }

            // Apply damage to random parts
            foreach (var (groupName, partName) in candidates)
            {
                if (affected >= partsToAffect) break;

                // Get the part
                if (_bodyPartGroups.TryGetValue(groupName, out var group))
                {
                    var part = group.Parts.FirstOrDefault(p => p.Name == partName);
                    if (part != null)
                    {
                        // Apply random amount of decay damage
                        float damageAmount = part.MaxHealth * rng.RandfRange(0.3f, 0.7f);
                        DamageBodyPart(groupName, partName, damageAmount);

                        GD.Print($"Zombie {Name}: {partName} shows decay damage");
                        affected++;
                    }
                }
            }
        }
    }
}
