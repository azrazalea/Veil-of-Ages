using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Core;

public partial class EntityThinkingSystem : Node
{
    /// <summary>
    /// Gets or sets timeout for entity thinking in milliseconds. If exceeded, task is killed and stack trace logged.
    /// </summary>
    [Export]
    public int ThinkingTimeoutMs { get; set; } = 200;

    private readonly List<Being> _entities = new ();
    private readonly ConcurrentQueue<EntityAction> _pendingActions = new ();
    private readonly TaskTracker _taskTracker = new ();

    private bool _isProcessingTick;

    public override void _Ready()
    {
        RegisterExistingEntities();
    }

    private void RegisterExistingEntities()
    {
        if (GetTree().GetFirstNodeInGroup("World") is World world)
        {
            var entitiesNode = world.GetNode<Node>("Entities");
            foreach (Node child in entitiesNode.GetChildren())
            {
                if (child is Being entity)
                {
                    RegisterEntity(entity);
                }
            }
        }
    }

    public void RegisterEntity(Being entity)
    {
        if (!_entities.Contains(entity))
        {
            _entities.Add(entity);
            Log.Print($"Registered entity {entity.Name} with thinking system");
        }
    }

    public void UnregisterEntity(Being entity)
    {
        _entities.Remove(entity);
    }

    public async Task ProcessGameTick()
    {
        if (_isProcessingTick)
        {
            Log.Error("Attempted to process game tick while another tick is in progress");
            return;
        }

        _isProcessingTick = true;
        _pendingActions.Clear();
        _taskTracker.TimeoutMs = ThinkingTimeoutMs;

        var world = GetTree().GetFirstNodeInGroup("World") as World;
        world?.PrepareForTick();

        // Start thinking tasks for all entities
        foreach (var entity in _entities)
        {
            var entityPosition = entity.Position;
            var currentObservation = world?.GetSensorySystem()?.GetObservationFor(entity);
            var gridPos = entity.GetCurrentGridPosition();

            // Fire-and-forget, tracked by _taskTracker
            _ = _taskTracker.Run(
                $"{entity.Name} at ({gridPos.X},{gridPos.Y})",
                () => ProcessEntityThinking(entity, entityPosition, currentObservation));
        }

        // Wait for all tasks, checking for stuck ones periodically
        var allTasks = Task.WhenAll(_taskTracker.GetAllTasks());
        while (!allTasks.IsCompleted)
        {
            await Task.WhenAny(allTasks, Task.Delay(50));
            _taskTracker.CheckAndKillStuck(GameController.CurrentTick);
        }

        // Apply all actions on the main thread
        ApplyAllPendingActions();

        _isProcessingTick = false;
    }

    private void ProcessEntityThinking(Being entity, Vector2 currentPosition, ObservationData? currentObservation)
    {
        if (currentObservation == null)
        {
            return;
        }

        var action = entity.Think(currentPosition, currentObservation);

        if (action != null)
        {
            _pendingActions.Enqueue(action);
        }
    }

    private void ApplyAllPendingActions()
    {
        var pendingActions = _pendingActions.ToList();
        pendingActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (var action in pendingActions)
        {
            bool success = action.Execute();
            if (success)
            {
                action.OnSuccessful?.Invoke(action);
            }

            action.Entity.ProcessMovementTick();
        }

        _pendingActions.Clear();
    }
}
