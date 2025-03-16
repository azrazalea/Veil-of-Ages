using Godot;
using System;
using NecromancerKingdom.Entities;
using NecromancerKingdom.Entities.Traits;

namespace NecromancerKingdom.Entities.Beings
{
    public partial class MindlessSkeleton : Being
    {
        public override void _Ready()
        {
            base._Ready();

            // Configure skeleton specific properties
            MoveSpeed = 0.4f; // Skeletons are faster than zombies but slower than living beings

            // Add skeleton traits
            AddTrait<UndeadTrait>();
            AddTrait<MindlessTrait>();

            // Unlike zombies, skeletons don't have hunger
            // They are animated purely by magic

            // Configure mindless trait settings for skeletons
            if (GetTrait<MindlessTrait>() is MindlessTrait mindlessTrait)
            {
                mindlessTrait.WanderProbability = 0.2f;
                mindlessTrait.WanderRange = 10f;
                mindlessTrait.IdleTime = 3.0f; // Skeletons stand idle longer
            }

            // Configure undead trait specifics for skeletons
            if (GetTrait<UndeadTrait>() is UndeadTrait undeadTrait)
            {
                // Skeletons are more vulnerable to bludgeoning damage
                // This would be handled if we had a combat system
            }

            GD.Print("Skeleton initialized with traits");
        }

        public override void Initialize(GridSystem gridSystem, Vector2I startGridPos)
        {
            base.Initialize(gridSystem, startGridPos);

            // Any skeleton-specific initialization after base initialization
            GD.Print($"Skeleton spawned at {startGridPos}");
        }

        // Custom skeleton behavior overrides (if needed)
        public override void _PhysicsProcess(double delta)
        {
            // Let the traits and base class handle most behavior
            base._PhysicsProcess(delta);

            // Add any skeleton-specific behavior here if needed
            // For example, we could make skeletons occasionally make bone rattling sounds
            if (IsMoving() && new RandomNumberGenerator().RandfRange(0f, 1f) < 0.01f)
            {
                GD.Print($"{Name}: *bones rattle*");
            }
        }
    }
}
