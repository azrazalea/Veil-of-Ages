using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities.Traits;

public class VillagerTrait : BeingTrait
{
    // Villager behavior properties
    public float WanderProbability { get; set; } = 0.1f;
    public float VisitBuildingProbability { get; set; } = 0.05f;
    public uint IdleTime { get; set; } = 20;

    // State machine
    private enum VillagerState
    {
        IdleAtHome,
        IdleAtSquare,
        VisitingBuilding,
        Sleeping
    }

    private VillagerState _currentState = VillagerState.IdleAtHome;

    // Village knowledge
    private Vector2I _homePosition;
    private Vector2I _squarePosition;
    private readonly List<Building> _knownBuildings = new ();
    private Building? _currentDestinationBuilding;

    // Home building for this villager
    private Building? _home;
    public Building? Home => _home;

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Find home position (where they spawned)
        _homePosition = _spawnPosition; // this is just an alias

        // Find village square (assume it's near the center of the map)
        if (owner?.GridArea != null)
        {
            _squarePosition = new Vector2I(owner.GridArea.GridSize.X / 2, owner.GridArea.GridSize.Y / 2);
        }

        // Discover buildings in the world
        DiscoverBuildings();

        if (owner == null || owner.Health == null)
        {
            return;
        }

        // Add LivingTrait to handle basic living needs - priority -1 so it initializes before consumption trait
        _owner?.SelfAsEntity().AddTraitToQueue<LivingTrait>(-1, initQueue);

        // Add InventoryTrait so villager can carry items
        _owner?.SelfAsEntity().AddTraitToQueue<InventoryTrait>(-1, initQueue);

        // Add ItemConsumptionBehaviorTrait for hunger
        // Uses item system: checks inventory then home storage for "food" tagged items
        var consumptionTrait = new ItemConsumptionBehaviorTrait(
            needId: "hunger",
            foodTag: "food",
            getHome: () => _home,
            restoreAmount: 60f,
            consumptionDuration: 244);

        // Add the consumption trait with a priority just below this trait
        _owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);

        _currentState = VillagerState.IdleAtHome;
        Log.Print($"{_owner?.Name}: Villager trait initialized fully");
        IsInitialized = true;
    }

    private void DiscoverBuildings()
    {
        // Find all buildings in the scene
        if (_owner?.GetTree().GetFirstNodeInGroup("World") is not World world)
        {
            return;
        }

        var entitiesNode = world.GetNode<Node>("Entities");
        foreach (Node child in entitiesNode.GetChildren())
        {
            if (child is Building building)
            {
                _knownBuildings.Add(building);
            }
        }

        Log.Print($"{_owner?.Name}: Discovered {_knownBuildings.Count} buildings");
    }

    /// <summary>
    /// Set the home building for this villager.
    /// Called by VillageGenerator when spawning villagers.
    /// </summary>
    public void SetHome(Building home)
    {
        _home = home;
        Log.Print($"{_owner?.Name}: Home set to {home.BuildingName}");
    }

    /// <summary>
    /// Set the pathfinder goal to navigate home.
    /// </summary>
    private void SetHomeGoal()
    {
        if (_owner == null)
        {
            return;
        }

        if (_home != null && GodotObject.IsInstanceValid(_home))
        {
            MyPathfinder.SetBuildingGoal(_owner, _home);
        }
        else
        {
            // Fallback to spawn position if no valid home building
            MyPathfinder.SetPositionGoal(_owner, _homePosition);
        }
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Only process AI if movement is complete
        if (_owner.IsMoving())
        {
            return null;
        }

        // Schedule-based behavior: check time of day
        var gameTime = GameTime.FromTicks(GameController.CurrentTick);
        bool shouldSleep = gameTime.CurrentDayPhase is DayPhaseType.Night or
                           DayPhaseType.Dusk;

        // If nighttime and not already heading home or sleeping, go home
        if (shouldSleep && _currentState != VillagerState.Sleeping &&
            _currentState != VillagerState.IdleAtHome)
        {
            _currentState = VillagerState.IdleAtHome;
            _stateTimer = 0;
        }

        // If daytime and sleeping, wake up
        if (!shouldSleep && _currentState == VillagerState.Sleeping)
        {
            _currentState = VillagerState.IdleAtHome;
            _stateTimer = IdleTime;
        }

        // Decrement timers
        if (_stateTimer > 0)
        {
            _stateTimer--;
        }

        // Process current state
        switch (_currentState)
        {
            case VillagerState.IdleAtHome:
                return ProcessIdleAtHomeState(shouldSleep);
            case VillagerState.IdleAtSquare:
                return ProcessIdleAtSquareState();
            case VillagerState.VisitingBuilding:
                return ProcessVisitingBuildingState();
            case VillagerState.Sleeping:
                return ProcessSleepingState();
            default:
                return new IdleAction(_owner, this);
        }
    }

    private EntityAction? ProcessIdleAtHomeState(bool shouldSleep = false)
    {
        if (_owner == null)
        {
            return null;
        }

        // Set home as goal and check if we're there
        SetHomeGoal();
        if (!MyPathfinder.IsGoalReached(_owner))
        {
            // Not at home yet, navigate there
            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        // We're at home - should we sleep?
        if (shouldSleep)
        {
            _currentState = VillagerState.Sleeping;
            Log.Print($"{_owner.Name}: Going to sleep");
            var sleepActivity = new SleepActivity(priority: 0);
            return new StartActivityAction(_owner, this, sleepActivity, priority: 0);
        }

        // Daytime behavior
        if (_stateTimer == 0)
        {
            // Chance to go to the village square
            if (_rng.Randf() < WanderProbability)
            {
                _currentState = VillagerState.IdleAtSquare;
                _stateTimer = (uint)_rng.RandiRange(100, 200);

                // Set goal to go to village square - lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, _squarePosition);

                Log.Print($"{_owner.Name}: Going to village square");
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
            }

            // Chance to visit a building
            else if (_rng.Randf() < VisitBuildingProbability && _knownBuildings.Count > 0)
            {
                _currentState = VillagerState.VisitingBuilding;
                _currentDestinationBuilding = _knownBuildings[_rng.RandiRange(0, _knownBuildings.Count - 1)];

                if (_currentDestinationBuilding != null)
                {
                    // Set goal to go to building - lazy path calculation
                    Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();
                    MyPathfinder.SetPositionGoal(_owner, buildingPos);

                    _stateTimer = (uint)_rng.RandiRange(80, 150);
                    Log.Print($"{_owner.Name}: Going to visit {_currentDestinationBuilding.BuildingType}");

                    return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
                }
            }
            else
            {
                // Just reset the timer
                _stateTimer = IdleTime;
            }
        }

        return new IdleAction(_owner, this, priority: 1);
    }

    private EntityAction? ProcessIdleAtSquareState()
    {
        if (_owner == null)
        {
            return null;
        }

        if (_stateTimer == 0)
        {
            // Time to go back home
            _currentState = VillagerState.IdleAtHome;

            // Set goal to go home - navigate to building interior if possible
            SetHomeGoal();

            _stateTimer = (uint)_rng.RandiRange(150, 300);
            Log.Print($"{_owner.Name}: Going back home");

            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        // Check if we're already at the square
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        if (currentPos.DistanceTo(_squarePosition) > 3)
        {
            // We're not at the square, so let's go there using lazy path calculation
            MyPathfinder.SetPositionGoal(_owner, _squarePosition);
            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        // If we're at the square, just idle or wander slightly
        if (_rng.Randf() < 0.2f)
        {
            // Small random movement within the square
            int dx = _rng.RandiRange(-1, 1);
            int dy = _rng.RandiRange(-1, 1);
            Vector2I targetPos = currentPos + new Vector2I(dx, dy);

            if (_owner.GetGridArea()?.IsCellWalkable(targetPos) == true)
            {
                return new MoveAction(_owner, this, targetPos);
            }
        }

        return new IdleAction(_owner, this);
    }

    private EntityAction? ProcessVisitingBuildingState()
    {
        if (_owner == null)
        {
            return null;
        }

        if (_stateTimer == 0 || _currentDestinationBuilding == null)
        {
            // Time to go back home
            _currentState = VillagerState.IdleAtHome;

            // Set goal to go home - navigate to building interior if possible
            SetHomeGoal();

            _stateTimer = (uint)_rng.RandiRange(150, 300);
            Log.Print($"{_owner.Name}: Finished visiting, going home");

            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        if (_currentDestinationBuilding == null)
        {
            return null;
        }

        // Check if we're at the building
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();

        // We consider "at the building" when within 2 tiles of its perimeter
        bool atBuilding = false;
        Vector2I buildingSize = _currentDestinationBuilding.GridSize;

        for (int x = -1; x <= buildingSize.X && !atBuilding; x++)
        {
            for (int y = -1; y <= buildingSize.Y && !atBuilding; y++)
            {
                if (currentPos == buildingPos + new Vector2I(x, y))
                {
                    atBuilding = true;
                }
            }
        }

        if (!atBuilding)
        {
            // We're not at the building, so let's go there using lazy path calculation
            MyPathfinder.SetPositionGoal(_owner, buildingPos);
            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        // If we're at the building, just idle
        return new IdleAction(_owner, this);
    }

    private EntityAction? ProcessSleepingState()
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if sleep activity is still running
        if (_owner.GetCurrentActivity() is SleepActivity)
        {
            // Let the activity handle things
            return null;
        }

        // Activity completed or was interrupted - transition back to idle at home
        _currentState = VillagerState.IdleAtHome;
        _stateTimer = IdleTime;
        return new IdleAction(_owner, this, priority: 1);
    }

    public override string InitialDialogue(Being speaker)
    {
        return $"Hello there {speaker.Name}";
    }
}
