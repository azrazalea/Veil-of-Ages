using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

/// <summary>
/// Specialized trait base class for traits that apply to Beings.
/// Provides common functionality specific to Being entities.
/// </summary>
public abstract class BeingTrait : Trait
{
    // Reference to the Being owner with stronger typing
    protected Being? _owner;
    public PathFinder MyPathfinder { get; set; } = new ();

    // State tracking fields
    protected uint _stateTimer = 0;
    protected Vector2I _spawnPosition = Vector2I.Zero;

    // Memory-related fields (optional, for traits that need memory)
    protected Dictionary<Vector2I, Dictionary<string, object>> _memory = new ();
    protected Dictionary<Vector2I, uint> _memoryTimestamps = new ();
    protected uint _memoryDuration = 3000; // Default memory duration in ticks

    /// <summary>
    /// Initialize the trait with a Being owner.
    /// </summary>
    public void Initialize(IEntity<BeingTrait> owner)
    {
        Initialize();

        if (owner is Being being)
        {
            _owner = being;
            _spawnPosition = _owner.GetCurrentGridPosition();
        }
    }

    /// <summary>
    /// Initialize the trait with a Being owner.
    /// </summary>
    public virtual void Initialize(Being owner, Queue<BeingTrait>? initQueue = null)
    {
        Initialize(owner, null, initQueue);
    }

    /// <summary>
    /// Initialize the trait with a Being owner and health system.
    /// </summary>
    public virtual void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        if (IsInitialized)
        {
            return;
        }

        _owner = owner;
        _rng.Randomize();
        _spawnPosition = _owner.GetCurrentGridPosition();
        IsInitialized = true;
    }

    /// <summary>
    /// Find entities of a specific type in perception.
    /// </summary>
    /// <returns></returns>
    protected List<(T entity, Vector2I position)> FindEntitiesOfType<T>(Perception perception)
        where T : Being
    {
        return perception.GetEntitiesOfType<T>();
    }

    /// <summary>
    /// Check if a type of entity is visible in perception.
    /// </summary>
    /// <returns></returns>
    protected bool CanSeeEntityType<T>(Perception perception)
        where T : Being
    {
        return FindEntitiesOfType<T>(perception).Count > 0;
    }

    /// <summary>
    /// Find the closest entity of a specific type.
    /// </summary>
    /// <returns></returns>
    protected (T? entity, Vector2I position) FindClosestEntityOfType<T>(Perception perception, Vector2I fromPosition)
        where T : Being
    {
        var entities = FindEntitiesOfType<T>(perception);
        if (entities.Count == 0)
        {
            return (null, Vector2I.Zero);
        }

        T? closest = null;
        Vector2I closestPos = Vector2I.Zero;
        float closestDist = float.MaxValue;

        foreach (var (entity, pos) in entities)
        {
            float dist = fromPosition.DistanceSquaredTo(pos);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = entity;
                closestPos = pos;
            }
        }

        return (closest, closestPos);
    }

    /// <summary>
    /// Updated helper for goal-based movement to a position - uses lazy pathfinding.
    /// </summary>
    /// <returns></returns>
    protected EntityAction? MoveToPosition(Vector2I targetPos, int priority = 1)
    {
        if (_owner == null)
        {
            return null;
        }

        // Set position goal without calculating path yet
        MyPathfinder.SetPositionGoal(_owner, targetPos);

        // Return action that will handle lazy path calculation
        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority);
    }

    /// <summary>
    /// Updated helper for goal-based movement near an entity - uses lazy pathfinding.
    /// </summary>
    /// <returns></returns>
    protected EntityAction? MoveNearEntity(Being targetEntity, int proximityRange = 1, int priority = 1)
    {
        if (_owner == null)
        {
            return null;
        }

        // Set entity proximity goal without calculating path yet
        MyPathfinder.SetEntityProximityGoal(_owner, targetEntity, proximityRange);

        // Return action that will handle lazy path calculation
        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority);
    }

    /// <summary>
    /// Updated helper for goal-based movement to an area - uses lazy pathfinding.
    /// </summary>
    /// <returns></returns>
    protected EntityAction? MoveToArea(Vector2I centerPos, int radius, int priority = 1)
    {
        if (_owner == null)
        {
            return null;
        }

        // Set area goal without calculating path yet
        MyPathfinder.SetAreaGoal(_owner, centerPos, radius);

        // Return action that will handle lazy path calculation
        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority);
    }

    /// <summary>
    /// Helper for wandering behavior - uses direct move for simple adjacent motion.
    /// </summary>
    /// <returns></returns>
    protected EntityAction? TryToWander(float wanderRange = 10.0f, int priority = 1)
    {
        if (_owner == null)
        {
            return null;
        }

        // Pick a random direction
        int randomDir = _rng.RandiRange(0, 7);
        Vector2I newDirection = Vector2I.Zero;
        Vector2I currentPos = _owner.GetCurrentGridPosition();
        Vector2I targetGridPos;
        var attempts = 0;
        var maxAttempts = 7;

        do
        {
            switch (randomDir)
            {
                case 0:
                    newDirection = Vector2I.Right;
                    break;
                case 1:
                    newDirection = Vector2I.Left;
                    break;
                case 2:
                    newDirection = Vector2I.Down;
                    break;
                case 3:
                    newDirection = Vector2I.Up;
                    break;
                case 4:
                    newDirection = Vector2I.Left + Vector2I.Up;
                    break;
                case 5:
                    newDirection = Vector2I.Left + Vector2I.Down;
                    break;
                case 6:
                    newDirection = Vector2I.Right + Vector2I.Up;
                    break;
                case 7:
                    newDirection = Vector2I.Right + Vector2I.Down;
                    break;
            }

            targetGridPos = currentPos + newDirection;
            attempts++;
        }
        while (_owner?.GridArea?.IsCellWalkable(targetGridPos) != true && attempts < maxAttempts);

        // Check if the target position is within wander range
        if (IsOutsideRange(wanderRange, _spawnPosition))
        {
            return MoveToPosition(_spawnPosition, priority);
        }

        if (_owner == null)
        {
            return null;
        }

        // Just use a simple move action for adjacent positions
        return new MoveAction(_owner, this, targetGridPos, priority);
    }

    /// <summary>
    /// Helper to move back to spawn position when too far away.
    /// </summary>
    /// <returns></returns>
    protected EntityAction? ReturnToSpawn(int priority = 1)
    {
        if (_owner == null)
        {
            return null;
        }

        // Set position goal without calculating path yet
        MyPathfinder.SetPositionGoal(_owner, _spawnPosition);

        // Return action that will handle lazy path calculation
        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority);
    }

    /// <summary>
    /// Check if entity is outside a specified range from its spawn position.
    /// </summary>
    /// <returns></returns>
    protected bool IsOutsideSpawnRange(float range)
    {
        return IsOutsideRange(range, _spawnPosition);
    }

    /// <summary>
    /// Check if entity is outside a specified range from a position.
    /// </summary>
    /// <returns></returns>
    protected bool IsOutsideRange(float range, Vector2I referencePos)
    {
        if (_owner == null)
        {
            return true;
        }

        return !Utils.WithinProximityRangeOf(_owner.GetCurrentGridPosition(), referencePos, range);
    }

    /// <summary>
    /// Check if entity is inside a specified range from a position.
    /// </summary>
    /// <returns></returns>
    protected bool IsInsideRange(float range, Vector2I referencePos)
    {
        if (_owner == null)
        {
            return true;
        }

        return Utils.WithinProximityRangeOf(_owner.GetCurrentGridPosition(), referencePos, range);
    }

    /// <summary>
    /// Calculate squared distance from a position
    /// (more efficient than regular distance as it avoids square root).
    /// </summary>
    /// <returns></returns>
    protected float SquaredDistanceFrom(Vector2I referencePos)
    {
        if (_owner == null)
        {
            return float.MaxValue;
        }

        Vector2I currentPos = _owner.GetCurrentGridPosition();
        return currentPos.DistanceSquaredTo(referencePos);
    }

    /// <summary>
    /// Store information in memory.
    /// </summary>
    protected void StoreMemory(Vector2I position, string key, object value)
    {
        if (!_memory.ContainsKey(position))
        {
            _memory[position] = new Dictionary<string, object>();
        }

        _memory[position][key] = value;
        _memoryTimestamps[position] = _memoryDuration;
    }

    /// <summary>
    /// Retrieve information from memory.
    /// </summary>
    /// <returns></returns>
    protected object? GetMemory(Vector2I position, string key)
    {
        if (_memory.TryGetValue(position, out var posMemory) &&
            posMemory.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// Check if a position exists in memory.
    /// </summary>
    /// <returns></returns>
    protected bool HasMemoryAt(Vector2I position, string key)
    {
        return _memory.TryGetValue(position, out var posMemory) &&
               posMemory.ContainsKey(key);
    }

    /// <summary>
    /// Update memory timestamps and clean up old memories.
    /// </summary>
    protected void UpdateMemory()
    {
        var posToRemove = new List<Vector2I>();

        foreach (var entry in _memoryTimestamps)
        {
            if (entry.Value <= 0)
            {
                posToRemove.Add(entry.Key);
            }
            else
            {
                _memoryTimestamps[entry.Key]--;
            }
        }

        foreach (var pos in posToRemove)
        {
            _memory.Remove(pos);
            _memoryTimestamps.Remove(pos);
        }
    }

    // Method to suggest actions for the entity
    public virtual EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        return null;
    }

    // Dialogue-related methods with default implementations
    public virtual bool RefusesCommand(EntityCommand command)
    {
        return false;
    }

    public virtual bool IsOptionAvailable(DialogueOption option)
    {
        return true;
    }

    public virtual string? InitialDialogue(Being speaker)
    {
        return null;
    }

    public virtual string? GetSuccessResponse(EntityCommand command)
    {
        return null;
    }

    public virtual string? GetFailureResponse(EntityCommand command)
    {
        return null;
    }

    public virtual string? GetSuccessResponse(string text)
    {
        return null;
    }

    public virtual string? GetFailureResponse(string text)
    {
        return null;
    }

    public virtual List<DialogueOption> GenerateDialogueOptions(Being speaker)
    {
        return [];
    }

    public virtual string? GenerateDialogueDescription()
    {
        return null;
    }
}
