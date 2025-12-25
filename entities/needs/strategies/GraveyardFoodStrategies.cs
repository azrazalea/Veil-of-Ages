using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Needs.Strategies;

// Graveyard food source identifier for zombies
public class GraveyardSourceIdentifier : IFoodSourceIdentifier
{
    public Building? IdentifyFoodSource(Being owner, Perception perception)
    {
        if (owner?.GridArea == null)
        {
            return null;
        }

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
    private readonly PathFinder _pathfinder = new ();

    public EntityAction? GetAcquisitionAction(Being owner, Building foodSource)
    {
        // Set position goal for pathfinding
        _pathfinder.SetBuildingGoal(owner, foodSource);
        return new MoveAlongPathAction(owner, this, _pathfinder, 0);
    }

    public bool IsAtFoodSource(Being owner, Building foodSource)
    {
        return _pathfinder.IsGoalReached(owner);
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

        Log.Print($"{owner.Name}: *groans with satisfaction* - Brains consumed at graveyard, hunger level restored to {need.Value}");
    }
}

// Critical hunger handler for zombies
public class ZombieCriticalHungerHandler : ICriticalStateHandler
{
    private readonly RandomNumberGenerator _rng = new ();

    public ZombieCriticalHungerHandler()
    {
        _rng.Randomize();
    }

    public EntityAction? HandleCriticalState(Being owner, Need need)
    {
        // Zombies become more aggressive when critically hungry
        if (_rng.Randf() < 0.05f)
        {
            Log.Print($"{owner.Name}: *growls ferociously* Desperately needs brains!");

            if (owner is MindlessZombie zombie)
            {
                zombie.CallDeferred("PlayZombieGroan");
            }
        }

        // No special action, they'll keep looking for a graveyard or prey
        return null;
    }
}
