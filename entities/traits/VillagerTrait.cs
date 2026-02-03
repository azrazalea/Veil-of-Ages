using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
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
    private Building? _currentDestinationBuilding;

    // Home building for this villager
    private Building? _home;
    public Building? Home => _home;

    /// <summary>
    /// Initializes a new instance of the <see cref="VillagerTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via Configure() method.
    /// </summary>
    public VillagerTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VillagerTrait"/> class.
    /// Convenience constructor for direct instantiation with a home building.
    /// </summary>
    public VillagerTrait(Building home)
    {
        _home = home;
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "home" (Building): The home building for this villager (optional but recommended).
    /// </summary>
    /// <remarks>
    /// If no home is provided, the villager will function but cannot return home to sleep
    /// or access home storage for food. A warning is logged if home is not configured.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // Home is optional but recommended - villager will have limited functionality without it
        if (config.GetBuilding("home") == null && _home == null)
        {
            Log.Warn("VillagerTrait: 'home' parameter recommended for proper sleep and storage access");
        }

        return true; // Don't fail - we handle missing home gracefully
    }

    public override void Configure(TraitConfiguration config)
    {
        // Only apply config if not already set via constructor
        if (_home == null)
        {
            var home = config.GetBuilding("home");
            if (home != null)
            {
                SetHome(home);
                Log.Print($"VillagerTrait configured with home: {home.BuildingName}");
            }
        }
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Find village square (assume it's near the center of the map)
        if (owner?.GridArea != null)
        {
            _squarePosition = new Vector2I(owner.GridArea.GridSize.X / 2, owner.GridArea.GridSize.Y / 2);
        }

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

        // Get current activity for debugging
        var currentActivity = _owner.GetCurrentActivity();
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);

        // Only sleep during Night phase - Dusk is evening idle time
        bool shouldSleep = gameTime.CurrentDayPhase is DayPhaseType.Night;

        // Debug: Log current state periodically
        DebugLog("SLEEP", $"State: {_currentState}, Phase: {gameTime.CurrentDayPhase}, ShouldSleep: {shouldSleep}, Activity: {currentActivity?.GetType().Name ?? "none"}");

        // If already sleeping, let the activity handle it
        if (currentActivity is SleepActivity)
        {
            DebugLog("SLEEP", "Already sleeping, returning null");
            return null;
        }

        // If nighttime and not already heading home or sleeping, go home
        if (shouldSleep && _currentState != VillagerState.Sleeping &&
            _currentState != VillagerState.IdleAtHome)
        {
            DebugLog("SLEEP", $"Night time but state is {_currentState}, changing to IdleAtHome");
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
        if (_owner == null || _home == null)
        {
            DebugLog("SLEEP", $"ProcessIdleAtHomeState: owner or home is null", 0);
            return null;
        }

        var currentActivity = _owner.GetCurrentActivity();

        // Check if already navigating via an activity (let it handle things)
        if (currentActivity is GoToBuildingActivity)
        {
            DebugLog("SLEEP", $"ProcessIdleAtHomeState: Already navigating (GoToBuildingActivity), returning null");
            return null;
        }

        // If not at home, start navigation
        if (!IsAtHome())
        {
            DebugLog("SLEEP", $"ProcessIdleAtHomeState: Not at home, starting navigation. ShouldSleep={shouldSleep}");
            var newGoHomeActivity = new GoToBuildingActivity(_home, priority: 1);
            return new StartActivityAction(_owner, this, newGoHomeActivity, priority: 1);
        }

        // We're at home - proceed with at-home logic
        DebugLog("SLEEP", $"ProcessIdleAtHomeState: At home. ShouldSleep={shouldSleep}, CurrentActivity={currentActivity?.GetType().Name ?? "none"}");

        // Should we sleep?
        if (shouldSleep)
        {
            DebugLog("SLEEP", "ProcessIdleAtHomeState: Starting SleepActivity with priority -1", 0);
            ChangeState(VillagerState.Sleeping, "Going to sleep");
            var sleepActivity = new SleepActivity(priority: -1);
            return new StartActivityAction(_owner, this, sleepActivity, priority: -1);
        }

        // Daytime behavior
        if (_stateTimer == 0)
        {
            // Chance to go to the village square
            if (_rng.Randf() < WanderProbability)
            {
                ChangeState(VillagerState.IdleAtSquare, "Going to village square");
                _stateTimer = (uint)_rng.RandiRange(100, 200);
                var goToSquareActivity = new GoToLocationActivity(_squarePosition, priority: 1);
                return new StartActivityAction(_owner, this, goToSquareActivity, priority: 1);
            }

            // Chance to visit a building (using SharedKnowledge)
            if (_rng.Randf() < VisitBuildingProbability)
            {
                // Get all known buildings from SharedKnowledge (using thread-safe method)
                var knownBuildings = _owner.SharedKnowledge
                    .SelectMany(k => k.GetAllBuildings())
                    .Where(b => b.IsValid && b.Building != _home) // Exclude home and invalid refs
                    .ToList();

                if (knownBuildings.Count > 0)
                {
                    var selectedRef = knownBuildings[_rng.RandiRange(0, knownBuildings.Count - 1)];
                    _currentDestinationBuilding = selectedRef.Building;

                    if (_currentDestinationBuilding != null)
                    {
                        ChangeState(VillagerState.VisitingBuilding, $"Visiting {_currentDestinationBuilding.BuildingType}");
                        _stateTimer = (uint)_rng.RandiRange(80, 150);
                        var visitActivity = new GoToBuildingActivity(_currentDestinationBuilding, priority: 1);
                        return new StartActivityAction(_owner, this, visitActivity, priority: 1);
                    }
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
            var goToSquareActivity = new GoToLocationActivity(_squarePosition, priority: 1);
            return new StartActivityAction(_owner, this, goToSquareActivity, priority: 1);
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
    /// Shows both real storage contents and what the entity remembers.
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
            var realContents = homeStorage.GetContentsSummary();

            // Get remembered contents
            var memoryContents = "nothing (no memory)";
            var storageMemory = _owner.Memory?.RecallStorageContents(_home);
            if (storageMemory != null)
            {
                var rememberedItems = storageMemory.Items
                    .Select(i => $"{i.Quantity} {i.Name}")
                    .ToList();
                memoryContents = rememberedItems.Count > 0 ? string.Join(", ", rememberedItems) : "empty";
            }

            DebugLog("STORAGE", $"[{_home.BuildingName}] Real: {realContents} | Remembered: {memoryContents}");
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            DebugLog("STORAGE", $"Inventory: {inventory.GetContentsSummary()}");
        }
    }
}
