using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Sensory;

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
        VisitingBuilding
    }

    private VillagerState _currentState = VillagerState.IdleAtHome;

    // Village knowledge
    private Building? _wellBuilding;
    private Building? _currentDestinationBuilding;

    /// <summary>
    /// Initializes a new instance of the <see cref="VillagerTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via HomeTrait.
    /// </summary>
    public VillagerTrait()
    {
    }

    /// <summary>
    /// Gets the home building from HomeTrait.
    /// </summary>
    private Building? GetHome() => _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Home should be configured via HomeTrait, not directly on VillagerTrait.
    /// </summary>
    /// <remarks>
    /// If no home is provided via HomeTrait, the villager will function but cannot return home to sleep
    /// or access home storage for food.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // Home is managed by HomeTrait, not VillagerTrait
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        // Home is managed by HomeTrait, not VillagerTrait
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        // Find the well building from SharedKnowledge (village gathering point)
        if (owner != null)
        {
            foreach (var knowledge in owner.SharedKnowledge)
            {
                if (knowledge.TryGetBuildingOfType("Well", out var wellRef) && wellRef?.Building != null)
                {
                    _wellBuilding = wellRef.Building;
                    break;
                }
            }
        }

        if (owner == null || owner.Health == null)
        {
            return;
        }

        // NOTE: LivingTrait, InventoryTrait, HomeTrait, and ItemConsumptionBehaviorTrait
        // are now defined in JSON (human_townsfolk.json) rather than programmatically added here.
        // This follows the ECS architecture where trait composition is data-driven.
        _currentState = VillagerState.IdleAtHome;
        var home = GetHome();
        if (home != null)
        {
            Log.Print($"{_owner?.Name}: Villager trait initialized with home {home.BuildingName}");
        }
        else
        {
            Log.Warn($"{_owner?.Name}: Villager trait initialized WITHOUT a home");
        }

        IsInitialized = true;
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

        // Debug: Log current state periodically
        DebugLog("VILLAGER", $"State: {_currentState}, Activity: {currentActivity?.GetType().Name ?? "none"}");

        // If sleeping (managed by ScheduleTrait), don't suggest anything
        if (currentActivity is SleepActivity)
        {
            return null;
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
                return ProcessIdleAtHomeState();
            case VillagerState.IdleAtSquare:
                return ProcessIdleAtSquareState();
            case VillagerState.VisitingBuilding:
                return ProcessVisitingBuildingState();
            default:
                return new IdleAction(_owner, this);
        }
    }

    private EntityAction? ProcessIdleAtHomeState()
    {
        var home = GetHome();
        var homeTrait = _owner?.SelfAsEntity().GetTrait<HomeTrait>();
        if (_owner == null || home == null)
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: owner or home is null", 0);
            return null;
        }

        var currentActivity = _owner.GetCurrentActivity();

        // Check if already navigating via an activity (let it handle things)
        if (currentActivity is GoToBuildingActivity)
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: Already navigating (GoToBuildingActivity), returning null");
            return null;
        }

        // If not at home, start navigation
        if (homeTrait != null && !homeTrait.IsEntityAtHome())
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: Not at home, starting navigation");
            var newGoHomeActivity = new GoToBuildingActivity(home, priority: 1);
            return new StartActivityAction(_owner, this, newGoHomeActivity, priority: 1);
        }

        // We're at home - daytime behavior
        if (_stateTimer == 0)
        {
            // Chance to go to the village well (gathering point)
            if (_rng.Randf() < WanderProbability && _wellBuilding != null)
            {
                ChangeState(VillagerState.IdleAtSquare, "Going to village well");
                _stateTimer = (uint)_rng.RandiRange(100, 200);
                var goToWellActivity = new GoToBuildingActivity(_wellBuilding, priority: 1, requireInterior: false);
                return new StartActivityAction(_owner, this, goToWellActivity, priority: 1);
            }

            // Chance to visit a building (using SharedKnowledge)
            if (_rng.Randf() < VisitBuildingProbability)
            {
                // Get all known buildings from SharedKnowledge (using thread-safe method)
                // Note: 'home' is already defined at the start of this method
                var knownBuildings = _owner.SharedKnowledge
                    .SelectMany(k => k.GetAllBuildings())
                    .Where(b => b.IsValid && b.Building != home) // Exclude home and invalid refs
                    .ToList();

                if (knownBuildings.Count > 0)
                {
                    var selectedRef = knownBuildings[_rng.RandiRange(0, knownBuildings.Count - 1)];
                    _currentDestinationBuilding = selectedRef.Building;

                    if (_currentDestinationBuilding != null)
                    {
                        ChangeState(VillagerState.VisitingBuilding, $"Visiting {_currentDestinationBuilding.BuildingType}");
                        _stateTimer = (uint)_rng.RandiRange(80, 150);

                        // Use requireInterior: false so villagers can visit buildings by standing nearby
                        // (e.g., gathering at the well doesn't require going inside)
                        var visitActivity = new GoToBuildingActivity(_currentDestinationBuilding, priority: 1, requireInterior: false);
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

        // Check if GoToBuildingActivity is still running
        if (_owner.GetCurrentActivity() is GoToBuildingActivity)
        {
            // Let the activity handle navigation
            return null;
        }

        // Activity completed or not started - check if we're near the well
        if (_wellBuilding != null)
        {
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I wellPos = _wellBuilding.GetCurrentGridPosition();
            if (currentPos.DistanceTo(wellPos) > 3)
            {
                // Need to navigate to well (activity failed or wasn't started)
                var goToWellActivity = new GoToBuildingActivity(_wellBuilding, priority: 1, requireInterior: false);
                return new StartActivityAction(_owner, this, goToWellActivity, priority: 1);
            }
        }

        // If we're near the well, just idle or wander slightly
        if (_rng.Randf() < 0.2f)
        {
            Vector2I currentPos = _owner.GetCurrentGridPosition();
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
        var home = GetHome();
        if (_owner?.DebugEnabled != true || home == null || !GodotObject.IsInstanceValid(home))
        {
            return;
        }

        var homeStorage = home.GetStorage();
        if (homeStorage != null)
        {
            var realContents = homeStorage.GetContentsSummary();

            // Get remembered contents
            var memoryContents = "nothing (no memory)";
            var homeStorageFacility = home.GetStorageFacility();
            var storageMemory = homeStorageFacility != null
                ? _owner.Memory?.RecallStorageContents(homeStorageFacility)
                : null;
            if (storageMemory != null)
            {
                var rememberedItems = storageMemory.Items
                    .Select(i => $"{i.Quantity} {i.Name}")
                    .ToList();
                memoryContents = rememberedItems.Count > 0 ? string.Join(", ", rememberedItems) : "empty";
            }

            DebugLog("STORAGE", $"[{home.BuildingName}] Real: {realContents} | Remembered: {memoryContents}");
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            DebugLog("STORAGE", $"Inventory: {inventory.GetContentsSummary()}");
        }
    }
}
