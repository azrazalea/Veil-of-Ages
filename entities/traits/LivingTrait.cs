using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;

namespace VeilOfAges.Entities.Traits
{
    public class LivingTrait : BeingTrait
    {
        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            // Initialize the hunger need in the needs system
            var needsSystem = owner.NeedsSystem;
            if (needsSystem != null)
            {
                var hunger = new Need("hunger", "Hunger", 75f, 5f, 15f, 40f, 90f);
                needsSystem.AddNeed(hunger);
            }

            GD.Print($"{owner.Name}: Living trait initialized with hunger need");
        }
    }
}
