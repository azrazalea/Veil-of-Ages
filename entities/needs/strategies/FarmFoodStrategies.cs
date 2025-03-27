using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Needs.Strategies
{
    // Farm food source identifier
    public class FarmSourceIdentifier : IFoodSourceIdentifier
    {
        public Building? IdentifyFoodSource(Being owner, Perception perception)
        {
            // Look for farms in the world
            var world = owner.GetTree().GetFirstNodeInGroup("World") as World;
            if (world == null) return null;

            var entitiesNode = world.GetNode<Node2D>("Entities");
            foreach (Node child in entitiesNode.GetChildren())
            {
                if (child is Building building && building.BuildingType == "Farm")
                {
                    return building;
                }
            }

            return null;
        }
    }

    // Farm food acquisition strategy
    public class FarmAcquisitionStrategy : IFoodAcquisitionStrategy
    {
        private PathFinder _pathfinder = new PathFinder();

        public EntityAction? GetAcquisitionAction(Being owner, Building foodSource)
        {
            Vector2I farmPos = foodSource.GetCurrentGridPosition();

            // Set position goal for pathfinding
            _pathfinder.SetPositionGoal(owner, farmPos);
            return new MoveAlongPathAction(owner, this, _pathfinder, 0);
        }

        public bool IsAtFoodSource(Being owner, Building foodSource)
        {
            Vector2I ownerPos = owner.GetCurrentGridPosition();
            Vector2I farmPos = foodSource.GetCurrentGridPosition();

            // Consider within 1 tile as "at the farm"
            return ownerPos.DistanceTo(farmPos) <= 1;
        }
    }

    // Farm consumption effect
    public class FarmConsumptionEffect : IConsumptionEffect
    {
        public void Apply(Being owner, Need need, Building foodSource)
        {
            // Restore the need value
            need.Restore(60f);

            GD.Print($"{owner.Name}: Satisfied {need.DisplayName} at farm, level restored to {need.Value}");
        }
    }

    // Critical hunger handler for villagers
    public class VillagerCriticalHungerHandler : ICriticalStateHandler
    {
        private RandomNumberGenerator _rng = new RandomNumberGenerator();

        public VillagerCriticalHungerHandler()
        {
            _rng.Randomize();
        }

        public EntityAction? HandleCriticalState(Being owner, Need need)
        {
            // Villagers just complain when critically hungry
            if (_rng.Randf() < 0.05f)
            {
                GD.Print($"{owner.Name}: Desperately hungry but can't find a farm!");
            }

            // No special action, they'll just keep trying to find a farm
            return null;
        }
    }
}
