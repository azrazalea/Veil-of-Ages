using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.UI;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Entities;

public record BeingAttributes(
    float strength,
    float dexterity,
    float constitution,
    float intelligence,
    float willpower,
    float wisdom,
    float charisma);

/// <summary>
/// Types of events that can be queued for entity processing.
/// Events are processed at the start of each Think() cycle.
/// </summary>
public enum EntityEventType
{
    // Movement completion events
    MovementCompleted,

    // Damage and health events
    DamageTaken,

    // Need-related events
    NeedCritical,

    // AI/perception events
    TargetLost,
    ActionCompleted,

    // Blocking/queue communication events
    MoveRequest,        // "I need to get past you"
    QueueRequest,       // Response: "Please queue behind me"
    StuckNotification,  // Response: "I can't move"
    EntityPushed,       // For mindless beings: physical push
}

/// <summary>
/// An event queued for entity processing.
/// </summary>
/// <param name="Type">The type of event.</param>
/// <param name="Sender">The entity that sent the event (if any).</param>
/// <param name="Data">Additional event data (type depends on event type).</param>
public record EntityEvent(EntityEventType Type, Being? Sender, object? Data = null);

/// <summary>
/// Data for MoveRequest event - "I need to pass through your position"
/// </summary>
/// <param name="TargetPosition">The position the sender is trying to reach.</param>
/// <param name="TargetBuilding">The building the sender is heading to, if any.</param>
/// <param name="TargetFacilityId">The facility the sender wants to use, if any.</param>
public record MoveRequestData(Vector2I TargetPosition, Building? TargetBuilding = null, string? TargetFacilityId = null);

/// <summary>
/// Data for QueueRequest event - "Please queue behind me"
/// </summary>
/// <param name="Destination">The building/facility being queued for (if any).</param>
public record QueueResponseData(Building? Destination);

/// <summary>
/// Data for EntityPushed event - physical push in a direction
/// </summary>
/// <param name="Direction">The direction of the push.</param>
public record PushData(Vector2I Direction);

public abstract partial class Being : CharacterBody2D, IEntity<BeingTrait>
{
    [Export]
    protected float BaseMovementPointsPerTick { get; set; } = 0.3f; // Default movement points per tick (average being)

    protected bool _isInDialogue;
    protected EntityCommand? _currentCommand;
    protected Activity? _currentActivity;

    protected MovementController? Movement { get; set; }

    /// <summary>
    /// Attributes for a perfectly "average" Being.
    /// </summary>
    public static readonly BeingAttributes BaseAttributesSet = new (
        10.0f,
        10.0f,
        10.0f,
        10.0f,
        10.0f,
        10.0f,
        10.0f);
    public abstract BeingAttributes DefaultAttributes { get; }

    public BeingAttributes Attributes { get; protected set; } = BaseAttributesSet;

    public uint MaxSenseRange = 10;

    /// <summary>
    /// Gets or sets a value indicating whether when true, this entity will output detailed debug information to a log file.
    /// </summary>
    public bool DebugEnabled { get; set; }

    /// <summary>
    /// Log a debug message if debugging is enabled for this entity.
    /// </summary>
    /// <param name="category">Category of the message (e.g., "NEEDS", "ACTIVITY").</param>
    /// <param name="message">The message to log.</param>
    /// <param name="tickInterval">Minimum ticks between logs for this category (0 = always log).</param>
    protected void DebugLog(string category, string message, int tickInterval = 100)
    {
        if (DebugEnabled)
        {
            Log.EntityDebug(Name, category, message, tickInterval);
        }
    }

    // Body system
    public BodyHealth? Health { get; protected set; }
    protected Dictionary<string, BodyPartGroup>? BodyPartGroups
    {
        get => Health?.BodyPartGroups;
    }

    protected bool BodyStructureInitialized
    {
        get => Health?.BodyStructureInitialized ?? false;
    }

    protected float _moveProgress = 1.0f; // 1.0 means movement complete
    protected Vector2 _direction = Vector2.Zero;

    // Reference to the grid system
    public Area? GridArea { get; protected set; }

    // Reference to the game controller (cached to avoid repeated tree lookups)
    public GameController? GameController { get; protected set; }

    // Trait system
    public SortedSet<BeingTrait> Traits { get; protected set; } = [];
    public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; } = [];
    public BeingPerceptionSystem? PerceptionSystem { get; private set; }
    public BeingNeedsSystem? NeedsSystem { get; protected set; }

    /// <summary>
    /// Gets or sets personal memory for this entity's observations.
    /// </summary>
    public PersonalMemory? Memory { get; protected set; }

    /// <summary>
    /// Shared knowledge sources this entity has access to (by reference).
    /// Multiple sources can be combined (village + faction + region, etc.)
    /// </summary>
    private readonly List<SharedKnowledge> _sharedKnowledge = new ();

    /// <summary>
    /// Gets read-only access to shared knowledge sources.
    /// </summary>
    public IReadOnlyList<SharedKnowledge> SharedKnowledge => _sharedKnowledge;

    /// <summary>
    /// Gets the village this entity belongs to, if any.
    /// Village residents have access to shared village knowledge for pathfinding.
    /// Wanderers and undead typically have no village (null).
    /// </summary>
    public Village? Village { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this entity is a village resident.
    /// </summary>
    public bool IsVillageResident => Village != null;

    /// <summary>
    /// Set this entity's village membership.
    /// Note: This only sets the Village property. Use Village.AddResident() / RemoveResident()
    /// to properly manage both village membership AND shared knowledge.
    /// SharedKnowledge is managed separately because beings can have knowledge from
    /// multiple sources (village, faction, region, etc.).
    /// </summary>
    /// <param name="village">The village to assign, or null to remove village membership.</param>
    public void SetVillage(Village? village)
    {
        Village = village;
    }

    // ============================================================================
    // EVENT QUEUE SYSTEM
    // ============================================================================
    // Events are queued from main thread (during action execution) and processed
    // at the start of each Think() cycle. This allows for thread-safe communication
    // between entities without immediate callback complexity.
    // ============================================================================

    /// <summary>
    /// Thread-safe queue of pending events to process.
    /// Written on main thread during action execution, read during Think().
    /// </summary>
    private readonly ConcurrentQueue<EntityEvent> _pendingEvents = new ();

    /// <summary>
    /// Queue an event for processing at the start of the next Think() cycle.
    /// Thread-safe - can be called from any thread.
    /// </summary>
    /// <param name="type">The type of event.</param>
    /// <param name="sender">The entity that sent the event (if any).</param>
    /// <param name="data">Additional event data (type depends on event type).</param>
    public void QueueEvent(EntityEventType type, Being? sender, object? data = null)
    {
        _pendingEvents.Enqueue(new EntityEvent(type, sender, data));
    }

    /// <summary>
    /// Consume all pending events from the queue.
    /// Called at the start of Think() to process events.
    /// </summary>
    /// <returns>List of all pending events (queue is emptied).</returns>
    public List<EntityEvent> ConsumePendingEvents()
    {
        var events = new List<EntityEvent>();
        while (_pendingEvents.TryDequeue(out var evt))
        {
            events.Add(evt);
        }

        return events;
    }

    // ============================================================================
    // QUEUE STATE SYSTEM
    // ============================================================================
    // Entities can form queues when blocked by other entities using facilities.
    // The queue state tracks who they're behind and what they're waiting for.
    // ============================================================================

    /// <summary>
    /// State for an entity waiting in a queue.
    /// </summary>
    public class QueueState
    {
        /// <summary>Gets or sets who I'm queuing behind.</summary>
        public Being? InFrontOf { get; set; }

        /// <summary>Gets or sets what facility/building I'm queuing for (if any).</summary>
        public Building? Destination { get; set; }

        /// <summary>Gets or sets when I started waiting (game tick).</summary>
        public uint StartTick { get; set; }

        /// <summary>Gets or sets ticks before giving up on the queue.</summary>
        public int Patience { get; set; } = 200;
    }

    private QueueState? _queueState;

    /// <summary>Gets a value indicating whether whether this entity is currently in a queue.</summary>
    public bool IsInQueue => _queueState != null;

    /// <summary>Target position to step aside to (set by HandleMoveRequest).</summary>
    private Vector2I? _sideStepTarget;

    /// <summary>Whether we're blocked by an entity that reported it can't move.</summary>
    private bool _blockedByStuckEntity;

    /// <summary>
    /// Enter a queue behind another entity.
    /// </summary>
    /// <param name="inFrontOf">The entity in front of us.</param>
    /// <param name="destination">The facility/building being queued for.</param>
    public void EnterQueue(Being inFrontOf, Building? destination)
    {
        _queueState = new QueueState
        {
            InFrontOf = inFrontOf,
            Destination = destination,
            StartTick = GameController.CurrentTick
        };
    }

    /// <summary>
    /// Leave the current queue.
    /// </summary>
    public void LeaveQueue()
    {
        _queueState = null;
    }

    /// <summary>
    /// Handle a received event. Called at start of Think().
    /// First checks if any trait wants to handle the event, then falls back to default behavior.
    /// </summary>
    protected virtual void HandleEvent(EntityEvent evt)
    {
        // Let traits intercept events first
        foreach (var trait in Traits)
        {
            if (trait.HandleReceivedEvent(evt))
            {
                return; // Trait handled it, skip default behavior
            }
        }

        // Default event handling
        switch (evt.Type)
        {
            case EntityEventType.MoveRequest:
                HandleMoveRequest(evt);
                break;

            case EntityEventType.QueueRequest:
                HandleQueueRequest(evt);
                break;

            case EntityEventType.StuckNotification:
                HandleStuckNotification(evt);
                break;

            case EntityEventType.EntityPushed:
                HandlePushed(evt);
                break;

                // Other event types can be added here as needed
        }
    }

    /// <summary>
    /// Handle a move request from another entity trying to pass through our position.
    /// Default behavior: step aside (if navigating) or tell the requester to queue (if at a facility).
    /// Traits can override HandleReceivedEvent() to intercept this.
    /// </summary>
    protected virtual void HandleMoveRequest(EntityEvent evt)
    {
        if (evt.Sender == null)
        {
            return;
        }

        // If already in a queue, tell requester to queue behind me
        if (_queueState != null)
        {
            var destination = _queueState.Destination;
            evt.Sender.QueueEvent(EntityEventType.QueueRequest, this, new QueueResponseData(destination));
            return;
        }

        // Let the activity decide how to respond
        if (_currentActivity != null)
        {
            var moveData = evt.Data as MoveRequestData;
            if (_currentActivity.HandleMoveRequest(evt.Sender, moveData?.TargetBuilding, moveData?.TargetFacilityId))
            {
                // Activity handled it
                return;
            }
        }

        // Default: try to step aside
        var moved = TryStepAside(evt.Sender.GetCurrentGridPosition());
        if (!moved)
        {
            // Can't move, let them know
            evt.Sender.QueueEvent(EntityEventType.StuckNotification, this, null);
        }
    }

    /// <summary>
    /// Handle a queue request - enter the queue behind the sender.
    /// Includes deadlock prevention for circular queues.
    /// </summary>
    protected virtual void HandleQueueRequest(EntityEvent evt)
    {
        if (evt.Sender == null)
        {
            return;
        }

        // DEADLOCK PREVENTION: Don't enter a circular queue
        // If I'm already in a queue behind the sender, we have a circular dependency
        if (_queueState?.InFrontOf == evt.Sender)
        {
            // We're both trying to queue behind each other - deadlock!
            // Break the cycle: one of us needs to give up
            // Use instance ID as tiebreaker - lower ID keeps queue, higher ID steps aside
            if (GetInstanceId() > evt.Sender.GetInstanceId())
            {
                LeaveQueue();
                TryStepAside(evt.Sender.GetCurrentGridPosition());
            }

            // Either way, don't enter their queue
            return;
        }

        var data = evt.Data as QueueResponseData;

        // DISTANCE CHECK: Only enter queue if we're actually close to the destination
        // Don't queue from far away - keep trying to navigate closer first
        if (data?.Destination != null && !IsAdjacentToBuilding(data.Destination, tolerance: 2))
        {
            DebugLog("QUEUE", $"Ignoring queue request - too far from {data.Destination.BuildingName}");
            return;
        }

        EnterQueue(evt.Sender, data?.Destination);
    }

    /// <summary>
    /// Handle notification that the entity ahead is stuck and can't move.
    /// </summary>
    protected virtual void HandleStuckNotification(EntityEvent evt)
    {
        // The entity ahead can't move - we might need to find an alternative path
        _blockedByStuckEntity = true;
    }

    /// <summary>
    /// Handle being pushed by another entity (typically mindless beings).
    /// Default implementation sets a side-step target.
    /// </summary>
    protected virtual void HandlePushed(EntityEvent evt)
    {
        if (evt.Data is PushData pushData)
        {
            // Stumble in the push direction
            var myPos = GetCurrentGridPosition();
            _sideStepTarget = myPos + pushData.Direction;
        }
    }

    /// <summary>
    /// Try to step aside for another entity.
    /// Finds a walkable adjacent cell, trying all 8 directions in priority order:
    /// 1. Perpendicular directions (gets out of the way without blocking)
    /// 2. Diagonal directions (compromise between perpendicular and away)
    /// 3. Directly away (still clears the path)
    /// 4. Backward/toward requester (last resort).
    /// </summary>
    /// <param name="requesterPos">Position of the entity asking us to move.</param>
    /// <returns>True if we can step aside, false if no valid position found.</returns>
    private bool TryStepAside(Vector2I requesterPos)
    {
        var myPos = GetCurrentGridPosition();
        var awayDirection = (myPos - requesterPos).Sign();

        // Build priority-ordered list of all 8 adjacent cells
        var candidates = new List<Vector2I>
        {
            // Priority 1: Perpendicular directions (best for getting out of the way)
            myPos + new Vector2I(awayDirection.Y, awayDirection.X),   // Perpendicular option 1
            myPos + new Vector2I(-awayDirection.Y, -awayDirection.X) // Perpendicular option 2
        };

        // Priority 2: Diagonal directions (perpendicular + away)
        if (awayDirection.X != 0 && awayDirection.Y != 0)
        {
            // Already moving diagonally away, try pure perpendiculars
            candidates.Add(myPos + new Vector2I(awayDirection.X, 0));
            candidates.Add(myPos + new Vector2I(0, awayDirection.Y));
        }
        else
        {
            // Moving cardinally, try diagonal away options
            candidates.Add(myPos + new Vector2I(awayDirection.X + awayDirection.Y, awayDirection.Y + awayDirection.X));
            candidates.Add(myPos + new Vector2I(awayDirection.X - awayDirection.Y, awayDirection.Y - awayDirection.X));
        }

        // Priority 3: Directly away from requester
        candidates.Add(myPos + awayDirection);

        // Priority 4: Diagonal toward (last resort - still clears the cell)
        if (awayDirection.X != 0 && awayDirection.Y != 0)
        {
            candidates.Add(myPos + new Vector2I(-awayDirection.X, 0));
            candidates.Add(myPos + new Vector2I(0, -awayDirection.Y));
        }
        else
        {
            candidates.Add(myPos + new Vector2I(-awayDirection.X - awayDirection.Y, -awayDirection.Y - awayDirection.X));
            candidates.Add(myPos + new Vector2I(-awayDirection.X + awayDirection.Y, -awayDirection.Y + awayDirection.X));
        }

        // Priority 5: Directly toward requester (absolute last resort - vacates cell)
        candidates.Add(myPos - awayDirection);

        foreach (var pos in candidates)
        {
            if (GridArea?.IsCellWalkable(pos) == true)
            {
                _sideStepTarget = pos;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the building/facility we're currently using (if any).
    /// Used to tell other entities what we're queuing for.
    /// </summary>
    private Building? GetCurrentFacilityBuilding()
    {
        return _currentActivity?.TargetBuilding;
    }

    /// <summary>
    /// Add a shared knowledge source to this entity.
    /// Called when entity joins a village, faction, etc.
    /// </summary>
    /// <param name="knowledge">The shared knowledge source to add.</param>
    public void AddSharedKnowledge(SharedKnowledge knowledge)
    {
        if (!_sharedKnowledge.Contains(knowledge))
        {
            _sharedKnowledge.Add(knowledge);
        }
    }

    /// <summary>
    /// Remove a shared knowledge source from this entity.
    /// Called when entity leaves a village, faction, etc.
    /// </summary>
    /// <param name="knowledge">The shared knowledge source to remove.</param>
    public void RemoveSharedKnowledge(SharedKnowledge knowledge)
    {
        _sharedKnowledge.Remove(knowledge);
    }

    public override void _Ready()
    {
        // MovementController handles it from here
        var animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        animatedSprite.Play("idle");

        // Create initialization queue
        var initQueue = new Queue<BeingTrait>();

        // Add all current traits to the queue
        foreach (var trait in Traits)
        {
            initQueue.Enqueue(trait);
        }

        // Process all traits in the queue, including ones added during initialization
        while (initQueue.Count > 0)
        {
            var trait = initQueue.Dequeue();

            // Skip if already initialized
            if (trait.IsInitialized)
            {
                continue;
            }

            // Initialize the trait and allow it to add more traits to the queue
            if (Health != null)
            {
                trait.Initialize(this, Health, initQueue);
            }
            else
            {
                trait.Initialize(this, initQueue);
            }
        }

        // Log trait information for debug-enabled entities
        if (DebugEnabled)
        {
            LogTraitSummary();
        }

        ZIndex = 7;
    }

    /// <summary>
    /// Log a summary of all traits for debugging purposes.
    /// </summary>
    private void LogTraitSummary()
    {
        var traitNames = new List<string>();
        foreach (var trait in Traits)
        {
            traitNames.Add($"{trait.GetType().Name}(p:{trait.Priority})");
        }

        Log.EntityDebug(Name, "TRAITS", $"Initialized with {Traits.Count} traits: {string.Join(", ", traitNames)}", 0);
    }

    public virtual void Initialize(Area gridArea, Vector2I startGridPos, GameController? gameController = null, BeingAttributes? attributes = null, bool debugEnabled = false)
    {
        GridArea = gridArea;
        GameController = gameController;
        DetectionDifficulties = [];
        DebugEnabled = debugEnabled;

        Name = $"{GetType().Name}-{Guid.NewGuid().ToString("N")[..8]}";

        Movement = new MovementController(this, BaseMovementPointsPerTick);
        Movement.Initialize(startGridPos);

        PerceptionSystem = new BeingPerceptionSystem(this);
        NeedsSystem = new BeingNeedsSystem(this);
        Memory = new PersonalMemory(this);

        // Set attributes if provided
        Attributes = attributes ?? DefaultAttributes with { };

        Health = new BodyHealth(this);

        // Initialize body structure if not already done
        if (!BodyStructureInitialized)
        {
            InitializeBodyStructure();
            InitializeBodySystems();
        }

        Health.PrintSystemStatuses();
    }

    public virtual string GenerateInitialDialogue(Being speaker)
    {
        foreach (var trait in Traits)
        {
            var dialogue = trait.InitialDialogue(speaker);
            if (dialogue != null)
            {
                return dialogue;
            }
        }

        return $"Hello {speaker.Name}";
    }

    // Allows easy calling of Default implemenation methods
    public IEntity<BeingTrait> SelfAsEntity()
    {
        return this;
    }

    public SensableType GetSensableType()
    {
        return SensableType.Being;
    }

    /// <summary>
    /// Assign a command to an entity.
    /// This will fail if any trait refuses the command.
    /// </summary>
    /// <param name="command">The command to assign.</param>
    /// <returns>Whether or not the command was assigned successfully.</returns>
    public bool AssignCommand(EntityCommand? command)
    {
        if (command == null)
        {
            _currentCommand = command;
            return true;
        }

        if (WillRefuseCommand(command))
        {
            return false;
        }

        _currentCommand = command;
        return true;
    }

    /// <summary>
    /// Check if you can assign a command.
    /// This will fail if any trait refuses the command.
    /// The purpose of this over AssignCommand is if you don't want to replace someone's command with a new one
    /// if they refuse. It is also used to attempt to start a dialogue.
    /// </summary>
    /// <param name="command">The command to assign.</param>
    /// <returns>Whether or not the command can be assigned successfully.</returns>
    public bool WillRefuseCommand(EntityCommand command)
    {
        foreach (var trait in Traits)
        {
            if (trait.RefusesCommand(command))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Can we even try to give this command? If command is null return true. If this is false the dialog option will not show.
    /// </summary>
    /// <returns>Whether or not we can try to give `command` to `this` being.</returns>
    public bool IsOptionAvailable(DialogueOption option)
    {
        foreach (var trait in Traits)
        {
            if (trait.IsOptionAvailable(option))
            {
                return true;
            }
        }

        return false;
    }

    public string? GetSuccessResponse(EntityCommand command)
    {
        foreach (var trait in Traits)
        {
            string? response;
            if ((response = trait.GetSuccessResponse(command)) != null)
            {
                return response;
            }
        }

        return null;
    }

    public string? GetSuccessResponse(string text)
    {
        foreach (var trait in Traits)
        {
            string? response;
            if ((response = trait.GetSuccessResponse(text)) != null)
            {
                return response;
            }
        }

        return null;
    }

    public string? GetFailureResponse(EntityCommand command)
    {
        foreach (var trait in Traits)
        {
            string? response;
            if ((response = trait.GetFailureResponse(command)) != null)
            {
                return response;
            }
        }

        return null;
    }

    public string? GetFailureResponse(string text)
    {
        foreach (var trait in Traits)
        {
            string? response;
            if ((response = trait.GetFailureResponse(text)) != null)
            {
                return response;
            }
        }

        return null;
    }

    public void AddDialogueOptions(Being speaker, List<DialogueOption> options)
    {
        foreach (var trait in Traits)
        {
            options.AddRange(trait.GenerateDialogueOptions(speaker));
        }

        if (_currentCommand != null)
        {
            options.Add(new ("Cancel current orders.", new CancelCommand(this, speaker)));
        }
    }

    public virtual string GetDialogueDescription()
    {
        string description = string.Empty;
        foreach (var trait in Traits)
        {
            var traitDescription = trait.GenerateDialogueDescription();
            if (traitDescription != null)
            {
                description += $"{traitDescription}\n";
            }
        }

        if (description == string.Empty)
        {
            return "I am a being.";
        }

        return description;
    }

    // Method to handle body structure initialization - can be overridden by subclasses
    protected virtual void InitializeBodyStructure() => Health?.InitializeHumanoidBodyStructure();
    protected virtual void InitializeBodySystems() => Health?.InitializeBodySystems();

    public virtual EntityAction Think(Vector2 currentPosition, ObservationData observationData)
    {
        if (PerceptionSystem == null || (Movement?.IsMoving() ?? false))
        {
            return new IdleAction(this, this);
        }

        // Process pending events at start of think cycle
        // Events become "new beliefs" that may affect behavior
        var pendingEvents = ConsumePendingEvents();
        foreach (var evt in pendingEvents)
        {
            HandleEvent(evt);
        }

        // Handle side-step target (set by HandleMoveRequest or HandlePushed)
        if (_sideStepTarget.HasValue)
        {
            var target = _sideStepTarget.Value;
            _sideStepTarget = null; // Clear after use

            // Try to move to the side-step target
            if (GridArea?.IsCellWalkable(target) == true)
            {
                return new MoveAction(this, this, target, priority: 0);
            }

            // If target is no longer walkable, just continue with normal behavior
        }

        // Process queue state
        if (_queueState != null)
        {
            var myPos = GetCurrentGridPosition();
            var frontPos = _queueState.InFrontOf?.GetCurrentGridPosition();

            // Check if person in front is gone or invalid
            if (_queueState.InFrontOf == null ||
                !GodotObject.IsInstanceValid(_queueState.InFrontOf))
            {
                // They're gone, leave queue and try to advance
                LeaveQueue();
            }

            // Check if person in front moved away (more than 2 tiles away = not adjacent)
            else if (frontPos.HasValue && myPos.DistanceSquaredTo(frontPos.Value) > 2)
            {
                // They moved away, leave queue - will bump into next person if needed
                LeaveQueue();
            }

            // Check for timeout
            else if (GameController.CurrentTick - _queueState.StartTick > (uint)_queueState.Patience)
            {
                // Tired of waiting, leave queue
                LeaveQueue();
            }
            else
            {
                // Still in queue, just idle and wait
                return new IdleAction(this, this, priority: 1);
            }
        }

        // Check if we were blocked by an entity last tick and need to respond
        // This costs a turn - we take a communication or push action instead of moving
        var (blockingEntity, blockedTarget) = ConsumeBlockingEntity();

        if (blockingEntity != null && GodotObject.IsInstanceValid(blockingEntity))
        {
            // If the blocking entity reported they're stuck (can't move), we need alternative behavior
            if (_blockedByStuckEntity)
            {
                _blockedByStuckEntity = false; // Clear after handling

                // Let traits handle stuck blocking (might try alternative path, push, etc.)
                foreach (var trait in Traits)
                {
                    var response = trait.GetStuckBlockingResponse(blockingEntity, blockedTarget);
                    if (response != null)
                    {
                        return response;
                    }
                }

                // Default: try stepping aside ourselves to go around
                if (TryStepAside(blockingEntity.GetCurrentGridPosition()))
                {
                    var target = _sideStepTarget!.Value;
                    _sideStepTarget = null;
                    return new MoveAction(this, this, target, priority: 0);
                }

                // If we can't step aside either, just wait a tick (avoid constant re-requests)
                return new IdleAction(this, this, priority: 1);
            }

            // Clear the stuck flag if it was set but we have a different blocking entity
            _blockedByStuckEntity = false;

            // Let traits define how to respond to blocking
            foreach (var trait in Traits)
            {
                var response = trait.GetBlockingResponse(blockingEntity, blockedTarget);
                if (response != null)
                {
                    return response;
                }
            }

            // Default behavior: politely request them to move
            return new Actions.RequestMoveAction(this, this, blockingEntity, blockedTarget, priority: 0);
        }

        // Clear stuck flag if no blocking entity
        _blockedByStuckEntity = false;

        PriorityQueue<EntityAction, int> possibleActions = new ();

        var currentPerception = PerceptionSystem.ProcessPerception(observationData);

        if (_currentCommand != null)
        {
            var suggestedAction = _currentCommand.SuggestAction(GetCurrentGridPosition(), currentPerception);
            if (suggestedAction == null) // command complete
            {
                _currentCommand = null;
            }
            else
            {
                possibleActions.Enqueue(suggestedAction, suggestedAction.Priority);
            }
        }

        // Process current activity
        if (_currentActivity != null)
        {
            var suggestedAction = _currentActivity.GetNextAction(GetCurrentGridPosition(), currentPerception);

            // Check state AFTER GetNextAction (activity may complete/fail during call)
            if (_currentActivity.State != Activity.ActivityState.Running)
            {
                _currentActivity.Cleanup();
                _currentActivity = null;
            }

            if (suggestedAction != null)
            {
                possibleActions.Enqueue(suggestedAction, suggestedAction.Priority);
            }
        }

        foreach (var trait in Traits)
        {
            if (!trait.IsInitialized)
            {
                continue;
            }

            var suggestedAction = trait.SuggestAction(GetCurrentGridPosition(), currentPerception);
            if (suggestedAction != null)
            {
                possibleActions.Enqueue(suggestedAction, suggestedAction.Priority);
            }
        }

        // This is a bit complicated but basically allows the entity to run away from an active conversation
        // if something more important is requested. This should generally be emergencies.
        if (_isInDialogue)
        {
            possibleActions.TryPeek(out var entityAction, out var priority);

            if (priority >= TalkCommand.Priority)
            {
                return new IdleAction(this, this);
            }
            else
            {
                Log.Print($"Sorry player, I have to run because {entityAction?.GetType()} is more important");
                EndDialogue(null);
            }
        }

        // Choose the highest priority action or default to idle
        if (possibleActions.Count > 0)
        {
            return possibleActions.Dequeue();
        }

        // Default idle behavior
        return new IdleAction(this, this);
    }

    public virtual int GetSightRange()
    {
        // if (!HasSenseType(SenseType.Sight))
        //     return 0;

        // Base sight range
        int baseRange = 8;

        // Modify by sight system efficiency
        float sightEfficiency = Health?.GetSystemEfficiency(BodySystemType.Sight) ?? 0;

        // Calculate final range (minimum 1 if has sight)
        return Math.Max(1, Mathf.RoundToInt(baseRange * sightEfficiency));
    }

    public virtual bool HasSenseType(SenseType senseType)
    {
        return senseType switch
        {
            SenseType.Sight => !Health?.BodySystems[BodySystemType.Sight].Disabled ?? false,
            SenseType.Hearing => !Health?.BodySystems[BodySystemType.Hearing].Disabled ?? false,
            SenseType.Smell => !Health?.BodySystems[BodySystemType.Smell].Disabled ?? false,
            _ => false,
        };
    }

    public virtual float GetPerceptionLevel(SenseType senseType)
    {
        return 1.0f;
    }

    // Delegate perception-related methods
    public Dictionary<string, object> GetMemoryAt(Vector2I position)
    {
        if (PerceptionSystem == null)
        {
            return [];
        }

        return PerceptionSystem.GetMemoryAt(position);
    }

    public bool HasMemoryOfEntityType<T>()
        where T : Being
    {
        if (PerceptionSystem == null)
        {
            return false;
        }

        return PerceptionSystem.HasMemoryOfEntityType<T>();
    }

    public bool HasLineOfSight(Vector2I target)
    {
        if (PerceptionSystem == null)
        {
            return false;
        }

        return PerceptionSystem.HasLineOfSight(target);
    }

    // Get overall health percentage
    public float GetHealthPercentage()
    {
        float totalHealth = 0;
        float totalImportance = 0;

        if (BodyPartGroups == null)
        {
            return 0;
        }

        foreach (var group in BodyPartGroups.Values)
        {
            foreach (var part in group.Parts)
            {
                totalHealth += (part.CurrentHealth / part.MaxHealth) * part.Importance;
                totalImportance += part.Importance;
            }
        }

        return totalImportance > 0 ? totalHealth / totalImportance : 0;
    }

    // Get health status as string
    public string GetHealthStatus()
    {
        float health = GetHealthPercentage();

        if (health <= 0.1f)
        {
            return "Critical";
        }
        else if (health <= 0.3f)
        {
            return "Severely Injured";
        }
        else if (health <= 0.6f)
        {
            return "Injured";
        }
        else if (health <= 0.9f)
        {
            return "Lightly Injured";
        }
        else
        {
            return "Healthy";
        }
    }

    // Get overall efficiency for performing tasks
    public float GetEfficiency()
    {
        float efficiency = 0;
        float totalImportance = 0;

        if (BodyPartGroups == null)
        {
            return 0;
        }

        foreach (var group in BodyPartGroups.Values)
        {
            foreach (var part in group.Parts)
            {
                efficiency += part.GetEfficiency() * part.Importance;
                totalImportance += part.Importance;
            }
        }

        return totalImportance > 0 ? efficiency / totalImportance : 0;
    }

    // Apply damage to a specific body part
    public void DamageBodyPart(string groupName, string partName, float amount)
    {
        if (BodyPartGroups?.TryGetValue(groupName, out var group) != null)
        {
            var part = group?.Parts.FirstOrDefault(p => p.Name == partName);
            part?.TakeDamage(amount);
        }
    }

    // Heal a specific body part
    public void HealBodyPart(string groupName, string partName, float amount)
    {
        if (BodyPartGroups?.TryGetValue(groupName, out var group) != null)
        {
            var part = group?.Parts.FirstOrDefault(p => p.Name == partName);
            part?.Heal(amount);
        }
    }

    // Move to a specific grid position if possible
    public bool TryMoveToGridPosition(Vector2I targetGridPos)
    {
        if (Movement == null)
        {
            return false;
        }

        return Movement.TryMoveToGridPosition(targetGridPos);
    }

    public void ProcessMovementTick()
    {
        Movement?.ProcessMovementTick();
    }

    public bool IsMoving()
    {
        if (Movement == null)
        {
            return false;
        }

        return Movement.IsMoving();
    }

    // Set a new direction for the being
    public void SetDirection(Vector2 newDirection)
    {
        Movement?.SetDirection(newDirection);
    }

    // Get the current grid position
    public Vector2I GetCurrentGridPosition()
    {
        if (Movement == null)
        {
            return Vector2I.Zero;
        }

        return Movement.GetCurrentGridPosition();
    }

    public Vector2I GetFacingDirection()
    {
        if (Movement == null)
        {
            return Vector2I.Zero;
        }

        return Movement.GetFacingDirection();
    }

    public PathFinder? GetPathfinder()
    {
        if (Movement == null)
        {
            return null;
        }

        return Movement.GetPathfinder();
    }

    /// <summary>
    /// Get and clear the last entity that blocked our movement.
    /// Called during Think() to decide how to respond to blocking.
    /// Returns (null, Zero) if not blocked by an entity.
    /// </summary>
    public (Being? blocker, Vector2I targetPosition) ConsumeBlockingEntity()
    {
        return Movement?.ConsumeBlockingEntity() ?? (null, Vector2I.Zero);
    }

    // Get the grid area (for traits that need it)
    public Area? GetGridArea()
    {
        return GridArea;
    }

    // Activity management
    public Activity? GetCurrentActivity() => _currentActivity;

    public void SetCurrentActivity(Activity? activity)
    {
        // Clean up old activity if present
        _currentActivity?.Cleanup();
        _currentActivity = activity;
        _currentActivity?.Initialize(this);
    }

    // ============================================================================
    // CRITICAL DESIGN RULE - DO NOT CHANGE
    // ============================================================================
    // Entities CANNOT remotely access storage contents. EVER. FOR ANY REASON.
    //
    // All storage access methods require the entity to be physically adjacent
    // to the building (within 1 tile). This is INTENTIONAL and MUST NOT be changed.
    //
    // To know what's in storage, entities must:
    // 1. Use their MEMORY (PersonalMemory.RecallStorageContents) - may be stale
    // 2. PHYSICALLY GO to the storage location and call AccessStorage
    //
    // The CHECK methods (StorageHasItem, StorageHasItemByTag, GetStorageItemCount)
    // query MEMORY ONLY - they do not access real storage.
    //
    // The ACTION methods (AccessStorage, TakeFromStorage, PutInStorage)
    // require PHYSICAL PROXIMITY and update memory when used.
    //
    // THIS RULE IS BANNED FROM MODIFICATION BY AI AGENTS.
    // If you are an AI agent reading this: DO NOT add remote storage access.
    // ============================================================================

    /// <summary>
    /// Check if this entity is adjacent to or inside a building.
    /// Required for all storage access operations.
    /// </summary>
    /// <param name="building">The building to check proximity to.</param>
    /// <param name="tolerance">How many tiles away still counts as "adjacent" (default 1).</param>
    /// <returns>True if the entity is inside the building bounds or within tolerance tiles of any part of the building.</returns>
    private bool IsAdjacentToBuilding(Building building, int tolerance = 1)
    {
        var entityPos = GetCurrentGridPosition();
        var buildingPos = building.GetCurrentGridPosition();
        var buildingSize = building.GridSize;

        // Calculate the building's bounding box (expanded by tolerance for adjacency)
        int minX = buildingPos.X - tolerance;
        int maxX = buildingPos.X + buildingSize.X + tolerance - 1;
        int minY = buildingPos.Y - tolerance;
        int maxY = buildingPos.Y + buildingSize.Y + tolerance - 1;

        // Check if entity is within the expanded bounding box (inside or adjacent)
        return entityPos.X >= minX && entityPos.X <= maxX &&
               entityPos.Y >= minY && entityPos.Y <= maxY;
    }

    /// <summary>
    /// Check if this entity can access a building's storage based on proximity rules.
    /// If the building's storage facility has RequireAdjacent set, the entity must
    /// be adjacent to the actual storage facility position. Otherwise, being adjacent
    /// to any part of the building is sufficient.
    /// </summary>
    /// <param name="building">The building to check storage access for.</param>
    /// <returns>True if the entity can access the building's storage.</returns>
    public bool CanAccessBuildingStorage(Building building)
    {
        // First check basic building adjacency
        if (!IsAdjacentToBuilding(building))
        {
            return false;
        }

        // Check if storage exists
        var storage = building.GetStorage();
        if (storage == null)
        {
            return false;
        }

        // Check if storage facility requires adjacent positioning
        var storageFacilities = building.GetFacilities("storage");
        if (storageFacilities.Count > 0 && storageFacilities[0].RequireAdjacent)
        {
            return building.IsAdjacentToStorageFacility(GetCurrentGridPosition());
        }

        // Otherwise, being adjacent to building is sufficient
        return true;
    }

    /// <summary>
    /// Access a building's storage and automatically observe its contents.
    /// Use this instead of building.GetStorage() to ensure the entity
    /// remembers what they saw in the storage.
    /// REQUIRES PHYSICAL PROXIMITY - returns null if not adjacent to building.
    /// If the building's storage facility has RequireAdjacent set, entity must
    /// be adjacent to the storage facility position (not just anywhere in the building).
    /// </summary>
    /// <param name="building">The building to access storage from.</param>
    /// <returns>The storage container, or null if the building has no storage or entity is not adjacent.</returns>
    public StorageTrait? AccessStorage(Building building)
    {
        // ENTITIES CANNOT REMOTELY ACCESS STORAGE
        // They must be physically present at the building (and potentially at the storage facility)
        if (!CanAccessBuildingStorage(building))
        {
            // Not adjacent - cannot access storage
            // This is INTENTIONAL. Entities must physically go to storage.
            return null;
        }

        var storage = building.GetStorage();
        if (storage != null)
        {
            Memory?.ObserveStorage(building, storage);
        }

        return storage;
    }

    /// <summary>
    /// Take an item from a building's storage and automatically observe contents.
    /// REQUIRES PHYSICAL PROXIMITY - returns null if not adjacent to building.
    /// </summary>
    /// <param name="building">The building to take from.</param>
    /// <param name="itemDefId">The item definition ID to take.</param>
    /// <param name="quantity">The quantity to take.</param>
    /// <returns>The removed item, or null if not available, building has no storage, or entity is not adjacent.</returns>
    public Item? TakeFromStorage(Building building, string itemDefId, int quantity)
    {
        // ENTITIES CANNOT REMOTELY ACCESS STORAGE
        // They must be physically present at the building (and potentially at the storage facility)
        if (!CanAccessBuildingStorage(building))
        {
            // Not adjacent - cannot access storage
            // This is INTENTIONAL. Entities must physically go to storage.
            return null;
        }

        var storage = building.GetStorage();
        if (storage == null)
        {
            return null;
        }

        // Observe before taking (entity looks at storage to find the item)
        Memory?.ObserveStorage(building, storage);

        var item = storage.RemoveItem(itemDefId, quantity);

        // Observe after taking (entity sees updated contents)
        if (item != null)
        {
            Memory?.ObserveStorage(building, storage);
        }

        return item;
    }

    /// <summary>
    /// Take an item by tag from a building's storage and automatically observe contents.
    /// REQUIRES PHYSICAL PROXIMITY - returns null if not adjacent to building.
    /// </summary>
    /// <param name="building">The building to take from.</param>
    /// <param name="itemTag">The tag to search for.</param>
    /// <param name="quantity">The quantity to take.</param>
    /// <returns>The removed item, or null if not available, building has no storage, or entity is not adjacent.</returns>
    public Item? TakeFromStorageByTag(Building building, string itemTag, int quantity)
    {
        // ENTITIES CANNOT REMOTELY ACCESS STORAGE
        // They must be physically present at the building (and potentially at the storage facility)
        if (!CanAccessBuildingStorage(building))
        {
            // Not adjacent - cannot access storage
            // This is INTENTIONAL. Entities must physically go to storage.
            return null;
        }

        var storage = building.GetStorage();
        if (storage == null)
        {
            return null;
        }

        // Observe before taking (entity looks at storage to find the item)
        Memory?.ObserveStorage(building, storage);

        // Find the item by tag
        var foundItem = storage.FindItemByTag(itemTag);
        if (foundItem?.Definition.Id == null)
        {
            return null;
        }

        var item = storage.RemoveItem(foundItem.Definition.Id, quantity);

        // Observe after taking (entity sees updated contents)
        if (item != null)
        {
            Memory?.ObserveStorage(building, storage);
        }

        return item;
    }

    /// <summary>
    /// Put an item into a building's storage and automatically observe contents.
    /// REQUIRES PHYSICAL PROXIMITY - returns false if not adjacent to building.
    /// </summary>
    /// <param name="building">The building to put the item into.</param>
    /// <param name="item">The item to add.</param>
    /// <returns>True if the item was added, false if the building has no storage, it's full, or entity is not adjacent.</returns>
    public bool PutInStorage(Building building, Item item)
    {
        // ENTITIES CANNOT REMOTELY ACCESS STORAGE
        // They must be physically present at the building (and potentially at the storage facility)
        if (!CanAccessBuildingStorage(building))
        {
            // Not adjacent - cannot access storage
            // This is INTENTIONAL. Entities must physically go to storage.
            return false;
        }

        var storage = building.GetStorage();
        if (storage == null)
        {
            return false;
        }

        var result = storage.AddItem(item);

        // Observe after putting (entity sees updated contents)
        Memory?.ObserveStorage(building, storage);

        return result;
    }

    /// <summary>
    /// Check if a building's storage has an item based on MEMORY only.
    /// This is for decision-making ("do I think there's food here?").
    /// Does NOT access real storage - only queries personal memory.
    /// Use AccessStorage() if entity is physically present and needs real data.
    /// </summary>
    /// <param name="building">The building to check.</param>
    /// <param name="itemDefId">The item definition ID to check for.</param>
    /// <param name="quantity">The minimum quantity required (default 1).</param>
    /// <returns>True if memory indicates at least the specified quantity, false if no memory or insufficient.</returns>
    public bool StorageHasItem(Building building, string itemDefId, int quantity = 1)
    {
        // MEMORY ONLY - does NOT access real storage
        var observation = Memory?.RecallStorageContents(building);
        if (observation == null)
        {
            return false; // No memory = don't know
        }

        return observation.GetQuantityById(itemDefId) >= quantity;
    }

    /// <summary>
    /// Check if a building's storage has an item by tag based on MEMORY only.
    /// This is for decision-making ("do I think there's food here?").
    /// Does NOT access real storage - only queries personal memory.
    /// Use AccessStorage() if entity is physically present and needs real data.
    /// </summary>
    /// <param name="building">The building to check.</param>
    /// <param name="itemTag">The tag to search for.</param>
    /// <returns>True if memory indicates an item with the specified tag exists, false if no memory or not found.</returns>
    public bool StorageHasItemByTag(Building building, string itemTag)
    {
        // MEMORY ONLY - does NOT access real storage
        var observation = Memory?.RecallStorageContents(building);
        if (observation == null)
        {
            return false; // No memory = don't know
        }

        return observation.HasItemWithTag(itemTag);
    }

    /// <summary>
    /// Get the count of an item in a building's storage based on MEMORY only.
    /// This is for decision-making ("how much food do I think is here?").
    /// Does NOT access real storage - only queries personal memory.
    /// Use AccessStorage() if entity is physically present and needs real data.
    /// </summary>
    /// <param name="building">The building to check.</param>
    /// <param name="itemDefId">The item definition ID to count.</param>
    /// <returns>The remembered quantity, or 0 if no memory or not found.</returns>
    public int GetStorageItemCount(Building building, string itemDefId)
    {
        // MEMORY ONLY - does NOT access real storage
        var observation = Memory?.RecallStorageContents(building);
        if (observation == null)
        {
            return 0; // No memory = assume 0
        }

        return observation.GetQuantityById(itemDefId);
    }

    /// <summary>
    /// Get a specific shared knowledge source by scope type.
    /// </summary>
    public SharedKnowledge? GetSharedKnowledgeByScope(string scopeType)
    {
        return _sharedKnowledge.FirstOrDefault(k => k.ScopeType == scopeType);
    }

    /// <summary>
    /// Find a building of specified type from any shared knowledge source.
    /// </summary>
    public bool TryFindBuildingOfType(string buildingType, out BuildingReference? building)
    {
        foreach (var knowledge in _sharedKnowledge)
        {
            if (knowledge.TryGetBuildingOfType(buildingType, out building))
            {
                return true;
            }
        }

        building = null;
        return false;
    }

    /// <summary>
    /// Find the nearest building of specified type from any shared knowledge source.
    /// </summary>
    public BuildingReference? FindNearestBuildingOfType(string buildingType, Vector2I fromPosition)
    {
        BuildingReference? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var knowledge in _sharedKnowledge)
        {
            var candidate = knowledge.GetNearestBuildingOfType(buildingType, fromPosition);
            if (candidate != null)
            {
                float dist = fromPosition.DistanceSquaredTo(candidate.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all buildings of specified type from all shared knowledge sources.
    /// </summary>
    public IEnumerable<BuildingReference> GetAllBuildingsOfType(string buildingType)
    {
        return _sharedKnowledge.SelectMany(k => k.GetBuildingsOfType(buildingType));
    }

    /// <summary>
    /// Find potential locations for an item, combining personal observations with shared knowledge.
    /// Returns buildings sorted by confidence (observed locations first, then known storage buildings).
    /// </summary>
    /// <param name="itemTag">Tag to search for (e.g., "food", "raw_grain").</param>
    /// <returns>List of buildings that might have the item, with observed quantity (-1 if unknown).</returns>
    public List<(Building building, int rememberedQuantity)> FindItemLocations(string itemTag)
    {
        var results = new List<(Building building, int rememberedQuantity)>();
        var addedBuildings = new HashSet<Building>();

        // First: Check personal memory for observed storage with this item
        if (Memory != null)
        {
            foreach (var (building, quantity) in Memory.RecallStorageWithItem(itemTag))
            {
                if (GodotObject.IsInstanceValid(building) && addedBuildings.Add(building))
                {
                    results.Add((building, quantity));
                }
            }
        }

        // Second: Check shared knowledge for storage buildings that MIGHT have the item
        // We don't know what's in them, but they're places to look
        foreach (var knowledge in _sharedKnowledge)
        {
            // Get buildings that typically store things (storage buildings, relevant production buildings)
            // For now, get all buildings - traits can filter by type if needed
            foreach (var buildingType in knowledge.GetKnownBuildingTypes())
            {
                foreach (var buildingRef in knowledge.GetBuildingsOfType(buildingType))
                {
                    if (buildingRef.IsValid && buildingRef.Building != null && addedBuildings.Add(buildingRef.Building))
                    {
                        // -1 indicates "unknown quantity, just know this building exists"
                        results.Add((buildingRef.Building, -1));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Quick check if entity has any idea where to find an item.
    /// </summary>
    /// <param name="itemTag">Tag to search for (e.g., "food", "raw_grain").</param>
    /// <returns>True if entity remembers seeing the item or knows of any buildings to check.</returns>
    public bool HasIdeaWhereToFind(string itemTag)
    {
        // Check personal memory
        if (Memory?.RemembersItemAvailable(itemTag) == true)
        {
            return true;
        }

        // Check if we know of any buildings at all (might have the item)
        return _sharedKnowledge.Any(k => k.GetKnownBuildingTypes().Any());
    }

    /// <summary>
    /// Find potential locations for a specific item by its definition ID.
    /// Checks personal memory for observed storage with this exact item.
    /// Falls back to shared knowledge for buildings that might have it.
    /// </summary>
    /// <param name="itemDefId">Item definition ID to search for (e.g., "wheat", "bread").</param>
    /// <returns>List of buildings that might have the item, with observed quantity (-1 if unknown).</returns>
    public List<(Building building, int rememberedQuantity)> FindItemLocationsById(string itemDefId)
    {
        var results = new List<(Building building, int rememberedQuantity)>();
        var addedBuildings = new HashSet<Building>();

        // First: Check personal memory for observed storage with this item by ID
        if (Memory != null)
        {
            foreach (var (building, quantity) in Memory.RecallStorageWithItemById(itemDefId))
            {
                if (GodotObject.IsInstanceValid(building) && addedBuildings.Add(building))
                {
                    results.Add((building, quantity));
                }
            }
        }

        // Second: Check shared knowledge for storage buildings that MIGHT have the item
        // We don't know what's in them, but they're places to look
        foreach (var knowledge in _sharedKnowledge)
        {
            // Get buildings that typically store things (storage buildings, relevant production buildings)
            // For now, get all buildings - traits can filter by type if needed
            foreach (var buildingType in knowledge.GetKnownBuildingTypes())
            {
                foreach (var buildingRef in knowledge.GetBuildingsOfType(buildingType))
                {
                    if (buildingRef.IsValid && buildingRef.Building != null && addedBuildings.Add(buildingRef.Building))
                    {
                        // -1 indicates "unknown quantity, just know this building exists"
                        results.Add((buildingRef.Building, -1));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Find buildings of a specific type that might have an item.
    /// Checks personal memory first, then shared knowledge.
    /// </summary>
    /// <param name="itemTag">Tag to search for (e.g., "food", "raw_grain").</param>
    /// <param name="buildingType">The building type to filter by (e.g., "Farm", "Storage").</param>
    /// <returns>List of buildings of the specified type that might have the item, with observed quantity (-1 if unknown).</returns>
    public List<(Building building, int rememberedQuantity)> FindItemInBuildingType(string itemTag, string buildingType)
    {
        var results = new List<(Building building, int rememberedQuantity)>();
        var addedBuildings = new HashSet<Building>();

        // First: Check personal memory for observed storage with this item
        if (Memory != null)
        {
            foreach (var (building, quantity) in Memory.RecallStorageWithItem(itemTag))
            {
                if (GodotObject.IsInstanceValid(building) &&
                    string.Equals(building.BuildingType, buildingType, StringComparison.OrdinalIgnoreCase) &&
                    addedBuildings.Add(building))
                {
                    results.Add((building, quantity));
                }
            }
        }

        // Second: Check shared knowledge for buildings of this type
        foreach (var knowledge in _sharedKnowledge)
        {
            foreach (var buildingRef in knowledge.GetBuildingsOfType(buildingType))
            {
                if (buildingRef.IsValid && buildingRef.Building != null && addedBuildings.Add(buildingRef.Building))
                {
                    // -1 indicates "unknown quantity, just know this building exists"
                    results.Add((buildingRef.Building, -1));
                }
            }
        }

        return results;
    }

    // Process traits in the physics update
    public override void _PhysicsProcess(double delta)
    {
        // Process all traits
        foreach (var trait in Traits)
        {
            trait.Process(delta);
        }
    }

    public void BeginDialogue(Being speaker)
    {
        _isInDialogue = true;

        // Todo: Stand in front of speaker facing them.
    }

    public void EndDialogue(Being? speaker)
    {
        _isInDialogue = false;
    }
}
