using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings;

namespace VeilOfAges.Entities.Needs.Strategies
{
    // Graveyard food source identifier for zombies
    public class GraveyardSourceIdentifier : IFoodSourceIdentifier
    {
        public Building? IdentifyFoodSource(Being owner, Perception perception)
        {
            if (owner?.GridArea == null) return null;

            foreach (Node child in owner.GridArea.Entities)
            {
                if (child is Building building && building.BuildingType == "Graveyard")
                {
                    return building;
                }
            }

            return null;
        }
    }

    // Graveyard feeding acquisition strategy
    public class GraveyardAcquisitionStrategy : IFoodAcquisitionStrategy
    {
        private PathFinder _pathfinder = new PathFinder();

        public EntityAction? GetAcquisitionAction(Being owner, Building foodSource)
        {
            Vector2I graveyardPos = foodSource.GetCurrentGridPosition();

            // Set position goal for pathfinding
            _pathfinder.SetPositionGoal(owner, graveyardPos);
            return new MoveAlongPathAction(owner, this, _pathfinder, 0);
        }

        public bool IsAtFoodSource(Being owner, Building foodSource)
        {
            Vector2I ownerPos = owner.GetCurrentGridPosition();
            Vector2I graveyardPos = foodSource.GetCurrentGridPosition();

            // Consider within 1 tile as "at the graveyard"
            return ownerPos.DistanceTo(graveyardPos) <= 1;
        }
    }

    // Zombie consumption effect
    public class ZombieConsumptionEffect : IConsumptionEffect
    {
        public void Apply(Being owner, Need need, Building foodSource)
        {
            // Restore the need value
            need.Restore(70f);

            // Play zombie groan if it's a MindlessZombie
            if (owner is MindlessZombie zombie)
            {
                zombie.CallDeferred("PlayZombieGroan");
            }

            GD.Print($"{owner.Name}: *groans with satisfaction* - Brains consumed at graveyard, hunger level restored to {need.Value}");
        }
    }

    // Critical hunger handler for zombies
    public class ZombieCriticalHungerHandler : ICriticalStateHandler
    {
        private RandomNumberGenerator _rng = new RandomNumberGenerator();

        public ZombieCriticalHungerHandler()
        {
            _rng.Randomize();
        }

        public EntityAction? HandleCriticalState(Being owner, Need need)
        {
            // Zombies become more aggressive when critically hungry
            if (_rng.Randf() < 0.05f)
            {
                GD.Print($"{owner.Name}: *growls ferociously* Desperately needs brains!");

                if (owner is MindlessZombie zombie)
                {
                    zombie.PlayZombieGroan();
                }
            }

            // No special action, they'll keep looking for a graveyard or prey
            return null;
        }
    }
}
