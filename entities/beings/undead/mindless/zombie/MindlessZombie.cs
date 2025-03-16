using Godot;
using System;
using NecromancerKingdom.Entities.Traits;

namespace NecromancerKingdom.Entities.Beings
{
    public partial class MindlessZombie : Being
    {
        public override void _Ready()
        {
            base._Ready();

            // Configure zombie specific properties
            MoveSpeed = 0.5f; // Zombies are slow

            // Add zombie traits
            AddTrait<UndeadTrait>();
            AddTrait<MindlessTrait>();

            // Optional: You can now customize specific settings for each trait
            if (GetTrait<MindlessTrait>() is MindlessTrait mindlessTrait)
            {
                mindlessTrait.WanderProbability = 0.3f; // Zombies wander more often
                mindlessTrait.WanderRange = 15f; // And further from spawn
            }

            GD.Print("Zombie initialized with traits");
        }

        public override void Initialize(GridSystem gridSystem, Vector2I startGridPos)
        {
            base.Initialize(gridSystem, startGridPos);

            // Any zombie-specific initialization after base initialization
            GD.Print($"Zombie spawned at {startGridPos}");
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
    }
}
