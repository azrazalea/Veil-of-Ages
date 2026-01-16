using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;
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

    // Trait system
    public SortedSet<BeingTrait> Traits { get; protected set; } = [];
    public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; } = [];
    public BeingPerceptionSystem? PerceptionSystem { get; private set; }
    public BeingNeedsSystem? NeedsSystem { get; protected set; }

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

        ZIndex = 7;
    }

    public virtual void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
    {
        GridArea = gridArea;
        DetectionDifficulties = [];

        Name = $"{GetType().Name}-{Guid.NewGuid().ToString("N")[..8]}";

        Movement = new MovementController(this, BaseMovementPointsPerTick);
        Movement.Initialize(startGridPos);

        PerceptionSystem = new BeingPerceptionSystem(this);
        NeedsSystem = new BeingNeedsSystem(this);

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
            var action = possibleActions.Dequeue();

            return action;
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
