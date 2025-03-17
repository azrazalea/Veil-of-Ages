using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NecromancerKingdom.Entities;

namespace NecromancerKingdom.Core
{
    public partial class EntityThinkingSystem : Node
    {
        [Export]
        public int MaxThreads { get; set; } = Math.Max(1, System.Environment.ProcessorCount - 1);

        private List<Being> _entities = new();
        private HashSet<Being> _entitiesProcessed = new();
        private List<EntityAction> _pendingActions = new();

        private SemaphoreSlim _thinkingSemaphore;
        private object _lockObject = new object();

        private bool _isProcessingTick = false;

        public override void _Ready()
        {
            _thinkingSemaphore = new SemaphoreSlim(MaxThreads, MaxThreads);

            // Automatically find and register all beings in the scene
            RegisterExistingEntities();
        }

        private void RegisterExistingEntities()
        {
            var world = GetTree().GetFirstNodeInGroup("World") as World;
            if (world != null)
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

            // Start thinking tasks for all entities
            foreach (var entity in _entities)
            {
                var entityPosition = entity.Position;
                tasks.Add(Task.Run(() => ProcessEntityThinking(entity, entityPosition)));
            }

            // Wait for all entities to complete their thinking
            await Task.WhenAll(tasks);

            // Apply all actions on the main thread
            ApplyAllPendingActions();

            _isProcessingTick = false;
        }

        private async Task ProcessEntityThinking(Being entity, Vector2 currentPosition)
        {
            // Use semaphore to limit concurrent processing
            await _thinkingSemaphore.WaitAsync();

            try
            {
                // Get the entity's decision
                var action = entity.Think(currentPosition);

                // Store the action for later execution
                lock (_lockObject)
                {
                    if (action != null)
                    {
                        _pendingActions.Add(action);
                    }
                    _entitiesProcessed.Add(entity);
                }
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
            _pendingActions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Apply each action
            foreach (var action in _pendingActions)
            {
                action.Execute();
                action.Entity.ProcessMovementTick();
            }
        }

        public override void _ExitTree()
        {
            _thinkingSemaphore.Dispose();
        }
    }
}
