using System.Linq;
using Godot;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Beings
{
    public partial class MindlessSkeleton : Being
    {
        public override BeingAttributes DefaultAttributes { get; } = new(
            12.0f,
            12.0f,
            16.0f,
            4.0f,
            10.0f,
            4.0f,
            1.0f
        );


        public override void _Ready()
        {

            // Configure skeleton specific properties
            _totalMoveTicks = 6; // Skeletons are faster than zombies but slower than living beings

            // Add skeleton traits
            selfAsEntity().AddTrait<UndeadTrait>();
            selfAsEntity().AddTrait<MindlessTrait>();

            // Unlike zombies, skeletons don't have hunger
            // They are animated purely by magic

            // Configure mindless trait settings for skeletons
            if (selfAsEntity().GetTrait<MindlessTrait>() is MindlessTrait mindlessTrait)
            {
                mindlessTrait.WanderProbability = 0.2f;
                mindlessTrait.WanderRange = 10f;
                mindlessTrait.IdleTime = 10; // Skeletons stand idle longer
            }

            // Configure undead trait specifics for skeletons
            if (selfAsEntity().GetTrait<UndeadTrait>() is UndeadTrait undeadTrait)
            {
                // Skeletons are more vulnerable to bludgeoning damage
                // This would be handled if we had a combat system
            }

            base._Ready();

            GD.Print("Skeleton initialized with traits");
        }

        public override void Initialize(Grid.Area gridArea, Vector2I startGridPos, BeingAttributes attributes = null)
        {
            base.Initialize(gridArea, startGridPos, attributes);

            // Any skeleton-specific initialization after base initialization
            GD.Print($"Skeleton spawned at {startGridPos}");
        }

        protected override void InitializeBodyStructure()
        {
            // Create base structure
            Health.InitializeHumanoidBodyStructure();

            // Then modify for skeleton specifics
            ModifyForSkeletalStructure();
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

        private void ModifyForSkeletalStructure()
        {
            // Remove soft tissues from all groups
            Health.RemoveSoftTissuesAndOrgans();

            // Strengthen bone parts
            foreach (var group in _bodyPartGroups.Values)
            {
                foreach (var part in group.Parts)
                {
                    // Enhance bone durability
                    if (part.IsBonePart())
                    {
                        // Skeletons have stronger bones
                        part.ScaleMaxHealth(1.5f);
                    }
                }
            }
        }
    }
}
