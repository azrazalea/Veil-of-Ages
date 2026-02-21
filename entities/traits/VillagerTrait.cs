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
    private RoomReference? _wellRoomRef;
    private Room? _currentDestinationRoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="VillagerTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home must be configured via HomeTrait.
    /// </summary>
    public VillagerTrait()
    {
    }

    /// <summary>
    /// Gets the home room from HomeTrait.
    /// </summary>
    private Room? GetHome() => _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;

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

        // Find the well room from SharedKnowledge (village gathering point)
        if (owner != null)
        {
            foreach (var knowledge in owner.SharedKnowledge)
            {
                if (knowledge.TryGetRoomOfType("Well", out var wellRef) && wellRef?.Room != null)
                {
                    _wellRoomRef = wellRef;
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
            Log.Print($"{_owner?.Name}: Villager trait initialized with home {home.Name}");
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
        var homeRoom = GetHome();
        var homeTrait = _owner?.SelfAsEntity().GetTrait<HomeTrait>();
        if (_owner == null || homeRoom == null)
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: owner or home is null", 0);
            return null;
        }

        var currentActivity = _owner.GetCurrentActivity();

        // Check if already navigating via an activity (let it handle things)
        if (currentActivity is GoToRoomActivity)
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: Already navigating (GoToRoomActivity), returning null");
            return null;
        }

        // If not at home, start navigation
        if (homeTrait != null && !homeTrait.IsEntityAtHome())
        {
            DebugLog("VILLAGER", $"ProcessIdleAtHomeState: Not at home, starting navigation");
            var newGoHomeActivity = new GoToRoomActivity(homeRoom, priority: 1);
            return new StartActivityAction(_owner, this, newGoHomeActivity, priority: 1);
        }

        // We're at home - daytime behavior
        if (_stateTimer == 0)
        {
            // Chance to go to the village well (gathering point)
            var wellRoom = _wellRoomRef?.Room;
            if (_rng.Randf() < WanderProbability && wellRoom != null && !wellRoom.IsDestroyed)
            {
                ChangeState(VillagerState.IdleAtSquare, "Going to village well");
                _stateTimer = (uint)_rng.RandiRange(100, 200);
                var goToWellActivity = new GoToRoomActivity(wellRoom, priority: 1, requireInterior: false);
                return new StartActivityAction(_owner, this, goToWellActivity, priority: 1);
            }

            // Chance to visit a room (using SharedKnowledge)
            if (_rng.Randf() < VisitBuildingProbability)
            {
                // Get all known rooms from SharedKnowledge (using thread-safe method)
                // Note: 'homeRoom' is already defined at the start of this method
                var knownRooms = _owner.SharedKnowledge
                    .SelectMany(k => k.GetAllRooms())
                    .Where(r => r.IsValid && r.Room != homeRoom) // Exclude home and invalid refs
                    .ToList();

                if (knownRooms.Count > 0)
                {
                    var selectedRef = knownRooms[_rng.RandiRange(0, knownRooms.Count - 1)];
                    var selectedRoom = selectedRef.Room;

                    if (selectedRoom != null)
                    {
                        _currentDestinationRoom = selectedRoom;
                        ChangeState(VillagerState.VisitingBuilding, $"Visiting {selectedRef.RoomType}");
                        _stateTimer = (uint)_rng.RandiRange(80, 150);

                        // Use requireInterior: false so villagers can visit buildings by standing nearby
                        // (e.g., gathering at the well doesn't require going inside)
                        var visitActivity = new GoToRoomActivity(selectedRoom, priority: 1, requireInterior: false);
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

        // Check if GoToRoomActivity is still running
        if (_owner.GetCurrentActivity() is GoToRoomActivity)
        {
            // Let the activity handle navigation
            return null;
        }

        // Activity completed or not started - check if we're near the well
        if (_wellRoomRef != null && _wellRoomRef.IsValid)
        {
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I wellPos = _wellRoomRef.Position;
            if (currentPos.DistanceTo(wellPos) > 3)
            {
                var wellRoom = _wellRoomRef.Room;
                if (wellRoom != null && !wellRoom.IsDestroyed)
                {
                    // Need to navigate to well (activity failed or wasn't started)
                    var goToWellActivity = new GoToRoomActivity(wellRoom, priority: 1, requireInterior: false);
                    return new StartActivityAction(_owner, this, goToWellActivity, priority: 1);
                }
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

        if (_currentDestinationRoom == null || _currentDestinationRoom.IsDestroyed)
        {
            ChangeState(VillagerState.IdleAtHome, "No destination room");
            return null;
        }

        // Check if GoToRoomActivity is still running
        if (_owner.GetCurrentActivity() is GoToRoomActivity)
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
        var homeRoom = GetHome();
        if (_owner?.DebugEnabled != true || homeRoom == null || homeRoom.IsDestroyed)
        {
            return;
        }

        var homeStorage = homeRoom.GetStorage();
        if (homeStorage != null)
        {
            var realContents = homeStorage.GetContentsSummary();

            // Get remembered contents
            var memoryContents = "nothing (no memory)";
            var homeStorageFacility = homeRoom.GetStorageFacility();
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

            DebugLog("STORAGE", $"[{homeRoom.Name}] Real: {realContents} | Remembered: {memoryContents}");
        }

        var inventory = _owner.SelfAsEntity().GetTrait<InventoryTrait>();
        if (inventory != null)
        {
            DebugLog("STORAGE", $"Inventory: {inventory.GetContentsSummary()}");
        }
    }
}
