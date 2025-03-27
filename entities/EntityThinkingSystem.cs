using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Core
{
    public partial class EntityThinkingSystem : Node
    {
        [Export]
        public int MaxThreads { get; set; } = Math.Max(1, System.Environment.ProcessorCount - 1);

        private List<Being> _entities = new();
        private ConcurrentDictionary<Being, Byte> _entitiesProcessed = [];
        private ConcurrentQueue<EntityAction> _pendingActions = [];

        private SemaphoreSlim _thinkingSemaphore;

        private bool _isProcessingTick = false;

        public EntityThinkingSystem()
        {
            _thinkingSemaphore = new SemaphoreSlim(MaxThreads, MaxThreads);
        }
        public override void _Ready()
        {
            // Automatically find and register all beings in the scene
            RegisterExistingEntities();
        }

        private void RegisterExistingEntities()
        {
            if (GetTree().GetFirstNodeInGroup("World") is World world)
            {
                var entitiesNode = world.GetNode<Node2D>("Entities");
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
                GD.Print($"Registered entity {entity.Name} with thinking system");
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
                GD.PrintErr("Attempted to process game tick while another tick is in progress");
                return;
            }

            _isProcessingTick = true;
            _entitiesProcessed.Clear();
            _pendingActions.Clear();

            var tasks = new List<Task>();

            var world = GetTree().GetFirstNodeInGroup("World") as World;
            world?.PrepareForTick();
            // Start thinking tasks for all entities
            foreach (var entity in _entities)
            {
                var entityPosition = entity.Position;
                var currentObservation = world?.GetSensorySystem()?.GetObservationFor(entity);
                tasks.Add(Task.Run(() => ProcessEntityThinking(entity, entityPosition, currentObservation)));
            }

            // Wait for all entities to complete their thinking
            await Task.WhenAll(tasks);

            // Apply all actions on the main thread
            ApplyAllPendingActions();

            _isProcessingTick = false;
        }

        private async Task ProcessEntityThinking(Being entity, Vector2 currentPosition, ObservationData? currentObservation)
        {
            if (currentObservation == null) return;

            // Use semaphore to limit concurrent processing
            await _thinkingSemaphore.WaitAsync();

            try
            {
                // Get the entity's decision
                var action = entity.Think(currentPosition, currentObservation);

                // Store the action for later execution
                if (action != null)
                {
                    _pendingActions.Enqueue(action);
                }
                _entitiesProcessed.TryAdd(entity, 0);

            }
            finally
            {
                // Always release the semaphore
                _thinkingSemaphore.Release();
            }
        }

        private void ApplyAllPendingActions()
        {
            // Sort actions by priority if needed
            var pendingActions = _pendingActions.ToList();
            pendingActions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Apply each action
            foreach (var action in pendingActions)
            {
                action.Execute();
                action.Entity.ProcessMovementTick();
            }
            _pendingActions.Clear();
        }

        public override void _ExitTree()
        {
            _thinkingSemaphore.Dispose();
        }
    }
}
