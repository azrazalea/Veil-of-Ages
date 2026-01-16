using System.Collections.Generic;
using Godot;
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

    // Activity for navigating to village square
    private GoToLocationActivity? _goToSquareActivity;

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
    /// Check if the entity is currently inside their home building.
    /// </summary>
    private bool IsAtHome()
    {
        if (_owner == null || _home == null)
        {
            return false;
        }

        Vector2I entityPos = _owner.GetCurrentGridPosition();
        Vector2I homePos = _home.GetCurrentGridPosition();
        var interiorPositions = _home.GetInteriorPositions();

        foreach (var relativePos in interiorPositions)
        {
            if (entityPos == homePos + relativePos)
            {
                return true;
            }
        }

        return false;
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
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
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

        // If already at home, skip navigation entirely
        if (IsAtHome())
        {
            // Clear any pending navigation activity
            if (_goHomeActivity != null)
            {
                _goHomeActivity = null;
            }

            // Fall through to at-home logic below
        }
        else
        {
            // Not at home - need to navigate there
            // Start going home if we haven't already
            if (_goHomeActivity == null)
            {
                StartGoingHome();
            }

            // Check if we have an active go-home activity
            if (_goHomeActivity != null)
            {
                // Still navigating home
                if (_goHomeActivity.State == Activity.ActivityState.Running)
                {
                    return _goHomeActivity.GetNextAction(_owner.GetCurrentGridPosition(), new Perception());
                }

                // Activity completed - arrived at home
                if (_goHomeActivity.State == Activity.ActivityState.Completed)
                {
                    DebugLog("STATE", "GoHomeActivity completed - arrived at home", 0);
                    _goHomeActivity = null;

                    // Fall through to at-home logic below
                }
                else if (_goHomeActivity.State == Activity.ActivityState.Failed)
                {
                    // Navigation failed - restart the activity to try again
                    DebugLog("STATE", "GoHomeActivity FAILED - restarting navigation", 0);
                    _goHomeActivity = null;
                    StartGoingHome();
                    if (_goHomeActivity != null)
                    {
                        return _goHomeActivity.GetNextAction(_owner.GetCurrentGridPosition(), new Perception());
                    }

                    // If we still can't start navigation, idle for now
                    return new IdleAction(_owner, this, priority: 1);
                }
            }
        }

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
                _goToSquareActivity = new GoToLocationActivity(_squarePosition, priority: 1);
                return new StartActivityAction(_owner, this, _goToSquareActivity, priority: 1);
            }

            // Chance to visit a building
            if (_rng.Randf() < VisitBuildingProbability && _knownBuildings.Count > 0)
            {
                _currentDestinationBuilding = _knownBuildings[_rng.RandiRange(0, _knownBuildings.Count - 1)];

                if (_currentDestinationBuilding != null)
                {
                    ChangeState(VillagerState.VisitingBuilding, $"Visiting {_currentDestinationBuilding.BuildingType}");
                    _stateTimer = (uint)_rng.RandiRange(80, 150);
                    var visitActivity = new GoToBuildingActivity(_currentDestinationBuilding, priority: 1);
                    return new StartActivityAction(_owner, this, visitActivity, priority: 1);
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
            _goToSquareActivity = null; // Clear the activity when leaving state
            return null;
        }

        // Check if GoToLocationActivity is still running
        if (_owner.GetCurrentActivity() is GoToLocationActivity)
        {
            // Let the activity handle navigation
            return null;
        }

        // Activity completed or not started - check if we're at the square
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        if (currentPos.DistanceTo(_squarePosition) > 3)
        {
            // Need to navigate to square (activity failed or wasn't started)
            _goToSquareActivity = new GoToLocationActivity(_squarePosition, priority: 1);
            return new StartActivityAction(_owner, this, _goToSquareActivity, priority: 1);
        }

        // We're at the square - clear activity reference
        _goToSquareActivity = null;

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

        if (_currentDestinationBuilding == null)
        {
            ChangeState(VillagerState.IdleAtHome, "No destination building");
            return null;
        }

        // Check if GoToBuildingActivity is still running
        if (_owner.GetCurrentActivity() is GoToBuildingActivity)
        {
            // Let the activity handle navigation
            return null;
        }

        // Activity completed (we're inside) or failed - check timer for how long to stay
        if (_stateTimer == 0)
        {
            // Time to go back home
            ChangeState(VillagerState.IdleAtHome, "Finished visiting, going home");
            _stateTimer = (uint)_rng.RandiRange(150, 300);
            return null;
        }

        // We're at the building, idle until timer expires
        return new IdleAction(_owner, this, priority: 1);
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
