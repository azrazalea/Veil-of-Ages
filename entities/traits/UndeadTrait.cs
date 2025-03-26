using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class UndeadTrait : BeingTrait
    {
        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            health.DisableBodySystem(BodySystemType.Pain);
            DisableLivingBodySystems(health);
            GD.Print($"{owner.Name}: Undead trait initialized");
        }

        public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;
            return new IdleAction(_owner, this, 0);
        }

        private static void DisableLivingBodySystems(BodyHealth health)
        {
            health.DisableBodySystem(BodySystemType.Breathing);
            health.DisableBodySystem(BodySystemType.BloodPumping);
            health.DisableBodySystem(BodySystemType.BloodFiltration);
            health.DisableBodySystem(BodySystemType.Digestion);
            health.DisableBodySystem(BodySystemType.Sight);
            health.DisableBodySystem(BodySystemType.Hearing);
        }
    }
}
