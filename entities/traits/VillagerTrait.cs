using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Items;
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
    private Vector2I _squarePosition;
    private readonly List<Building> _knownBuildings = [];
    private Building? _currentDestinationBuilding;

    // Home building for this villager
    private Building? _home;
    public Building? Home => _home;

    // Activity for navigating home
    private GoToBuildingActivity? _goHomeActivity;

    public VillagerTrait(Building? home = null)
    {
        _home = home;
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

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
        if (_home != null)
        {
            Log.Print($"{_owner?.Name}: Villager trait initialized with home {_home.BuildingName}");
        }
        else
        {
            Log.Warn($"{_owner?.Name}: Villager trait initialized WITHOUT a home");
        }

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
    /// Start navigating home using GoToBuildingActivity.
    /// </summary>
    private void StartGoingHome()
    {
        if (_owner == null || _home == null)
        {
            return;
        }

        _goHomeActivity = new GoToBuildingActivity(_home, Priority);
        _goHomeActivity.Initialize(_owner);
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
            ChangeState(VillagerState.IdleAtHome, "Night time, heading home");
            _stateTimer = 0;
        }

        // If daytime and sleeping, wake up
        if (!shouldSleep && _currentState == VillagerState.Sleeping)
        {
            ChangeState(VillagerState.IdleAtHome, "Woke up");
            _stateTimer = IdleTime;
        }

        // Periodic storage debug logging
        LogHomeStorage();

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

        // Start going home if we haven't already
        if (_goHomeActivity == null)
        {
            StartGoingHome();
        }

        // Still navigating home
        if (_goHomeActivity != null && _goHomeActivity.State == Activity.ActivityState.Running)
        {
            return _goHomeActivity.GetNextAction(_owner.GetCurrentGridPosition(), new Perception());
        }

        // We're at home - clear the activity
        _goHomeActivity = null;

        // Should we sleep?
        if (shouldSleep)
        {
            ChangeState(VillagerState.Sleeping, "Going to sleep");
            var sleepActivity = new SleepActivity(priority: 0);
            return new StartActivityAction(_owner, this, sleepActivity, priority: 0);
        }

        // Daytime behavior
        if (_stateTimer == 0)
        {
            // Chance to go to the village square
            if (_rng.Randf() < WanderProbability)
            {
                ChangeState(VillagerState.IdleAtSquare, "Going to village square");
                _stateTimer = (uint)_rng.RandiRange(100, 200);
                MyPathfinder.SetPositionGoal(_owner, _squarePosition);
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
            }

            // Chance to visit a building
            if (_rng.Randf() < VisitBuildingProbability && _knownBuildings.Count > 0)
            {
                _currentDestinationBuilding = _knownBuildings[_rng.RandiRange(0, _knownBuildings.Count - 1)];

                if (_currentDestinationBuilding != null)
                {
                    ChangeState(VillagerState.VisitingBuilding, $"Visiting {_currentDestinationBuilding.BuildingType}");
                    Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();
                    MyPathfinder.SetPositionGoal(_owner, buildingPos);
                    _stateTimer = (uint)_rng.RandiRange(80, 150);
                    return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
                }
            }

            _stateTimer = IdleTime;
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
            // Time to go back home - ProcessIdleAtHomeState will handle navigation
            ChangeState(VillagerState.IdleAtHome, "Finished at square, going home");
            _stateTimer = (uint)_rng.RandiRange(150, 300);
            return null;
        }

        // Check if we're already at the square
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        if (currentPos.DistanceTo(_squarePosition) > 3)
        {
            MyPathfinder.SetPositionGoal(_owner, _squarePosition);
            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        // If we're at the square, just idle or wander slightly
        if (_rng.Randf() < 0.2f)
        {
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
            // Time to go back home - ProcessIdleAtHomeState will handle navigation
            ChangeState(VillagerState.IdleAtHome, "Finished visiting, going home");
            _stateTimer = (uint)_rng.RandiRange(150, 300);
            return null;
        }

        // Check if we're at the building
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();
        Vector2I buildingSize = _currentDestinationBuilding.GridSize;

        bool atBuilding = false;
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
            MyPathfinder.SetPositionGoal(_owner, buildingPos);
            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

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
        ChangeState(VillagerState.IdleAtHome, "Sleep activity ended");
        _stateTimer = IdleTime;
        return new IdleAction(_owner, this, priority: 1);
    }

    public override string InitialDialogue(Being speaker)
    {
        return $"Hello there {speaker.Name}";
    }

    /// <summary>
    /// Change state and log the transition if debug is enabled.
    /// </summary>
    private void ChangeState(VillagerState newState, string reason)
    {
        if (_currentState != newState)
        {
            DebugLog("STATE", $"{_currentState} -> {newState}: {reason}", 0);
            _currentState = newState;
        }
    }

    /// <summary>
    /// Log home storage contents periodically for debugging.
    /// </summary>
    private void LogHomeStorage()
    {
        if (_owner?.DebugEnabled != true || _home == null || !GodotObject.IsInstanceValid(_home))
        {
            return;
        }

        var homeStorage = _home.GetStorage();
        if (homeStorage != null)
        {
            DebugLog("STORAGE", $"Home ({_home.BuildingName}): {homeStorage.GetContentsSummary()}");
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            DebugLog("STORAGE", $"Inventory: {inventory.GetContentsSummary()}");
        }
    }
}
