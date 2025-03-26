using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Specialized trait base class for traits that apply to Beings.
    /// Provides common functionality specific to Being entities.
    /// </summary>
    public abstract class BeingTrait : Trait
    {
        // Reference to the Being owner with stronger typing
        protected Being? _owner;
        public PathFinder MyPathfinder { get; set; } = new();

        // State tracking fields
        protected uint _stateTimer = 0;
        protected Vector2I _spawnPosition = Vector2I.Zero;

        // Memory-related fields (optional, for traits that need memory)
        protected Dictionary<Vector2I, Dictionary<string, object>> _memory = new();
        protected Dictionary<Vector2I, uint> _memoryTimestamps = new();
        protected uint _memoryDuration = 3000; // Default memory duration in ticks

        /// <summary>
        /// Initialize the trait with a Being owner
        /// </summary>
        public void Initialize(IEntity<BeingTrait> owner)
        {
            base.Initialize();

            if (owner is Being being)
            {
                _owner = being;
                _spawnPosition = _owner.GetCurrentGridPosition();
            }
        }

        /// <summary>
        /// Initialize the trait with a Being owner and health system
        /// </summary>
        public virtual void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;
            _rng.Randomize();
            _spawnPosition = _owner.GetCurrentGridPosition();
            IsInitialized = true;
        }

        #region Dialogue

        /// <summary>
        /// Method to suggest actions for the entity
        /// </summary>
        public virtual EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            return null;
        }

        // Dialogue-related methods with default implementations
        public virtual bool RefusesCommand(EntityCommand command) { return false; }

        public virtual bool IsOptionAvailable(DialogueOption option) { return true; }

        public virtual string? InitialDialogue(Being speaker) { return null; }

        public virtual string? GetSuccessResponse(EntityCommand command) { return null; }

        public virtual string? GetFailureResponse(EntityCommand command) { return null; }

        public virtual string? GetSuccessResponse(string text) { return null; }

        public virtual string? GetFailureResponse(string text) { return null; }

        public virtual List<DialogueOption> GenerateDialogueOptions(Being speaker) { return []; }

        public virtual string? GenerateDialogueDescription() { return "I am a being."; }

        #endregion

        #region Memory Management

        /// <summary>
        /// Store information in memory
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
        /// Retrieve information from memory
        /// </summary>
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
        /// Check if a position exists in memory
        /// </summary>
        protected bool HasMemoryAt(Vector2I position, string key)
        {
            return _memory.TryGetValue(position, out var posMemory) &&
                   posMemory.ContainsKey(key);
        }

        /// <summary>
        /// Update memory timestamps and clean up old memories
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

        #endregion

        #region Movement Helpers

        /// <summary>
        /// Helper for wandering behavior - creates a movement action in a random direction
        /// </summary>
        protected EntityAction? TryToWander(float wanderRange = 10.0f, int priority = 1)
        {
            if (_owner == null) return null;

            // Pick a random direction
            int randomDir = _rng.RandiRange(0, 7);
            Vector2I newDirection = Vector2I.Zero;

            switch (randomDir)
            {
                case 0: newDirection = Vector2I.Right; break;
                case 1: newDirection = Vector2I.Left; break;
                case 2: newDirection = Vector2I.Down; break;
                case 3: newDirection = Vector2I.Up; break;
                case 4: newDirection = Vector2I.Left + Vector2I.Up; break;
                case 5: newDirection = Vector2I.Left + Vector2I.Down; break;
                case 6: newDirection = Vector2I.Right + Vector2I.Up; break;
                case 7: newDirection = Vector2I.Right + Vector2I.Down; break;
            }

            // Calculate target grid position
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I targetGridPos = currentPos + newDirection;

            // Check if the target position is within wander range
            var distanceFromSpawn = targetGridPos.DistanceSquaredTo(_spawnPosition);
            if (distanceFromSpawn > wanderRange * wanderRange)
            {
                // Too far from spawn, try to move back toward spawn
                Vector2 towardSpawn = (_spawnPosition - currentPos).Sign();

                // Recalculate target position
                targetGridPos = currentPos + new Vector2I(
                    (int)towardSpawn.X,
                    (int)towardSpawn.Y
                );
            }

            // Try to move to the target position
            return new MoveAction(_owner, this, targetGridPos, priority);
        }

        /// <summary>
        /// Check if entity is outside a specified range from spawn position
        /// </summary>
        protected bool IsOutsideWanderRange(float range)
        {
            return IsOutsideRange(range, _spawnPosition);
        }

        /// <summary>
        /// Check if entity is outside a specified range from a position
        /// </summary>
        protected bool IsOutsideRange(float range, Vector2I referencePos)
        {
            if (_owner == null) return true;

            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I distance = currentPos - referencePos;

            return Mathf.Abs(distance.X) > range ||
                   Mathf.Abs(distance.Y) > range;
        }

        #endregion

        #region Perception Helpers

        /// <summary>
        /// Find entities of a specific type in perception
        /// </summary>
        protected List<(T entity, Vector2I position)> FindEntitiesOfType<T>(Perception perception) where T : Being
        {
            return perception.GetEntitiesOfType<T>();
        }

        /// <summary>
        /// Check if a type of entity is visible in perception
        /// </summary>
        protected bool CanSeeEntityType<T>(Perception perception) where T : Being
        {
            return FindEntitiesOfType<T>(perception).Count > 0;
        }

        /// <summary>
        /// Find the closest entity of a specific type
        /// </summary>
        protected (T? entity, Vector2I position) FindClosestEntityOfType<T>(Perception perception, Vector2I fromPosition) where T : Being
        {
            var entities = FindEntitiesOfType<T>(perception);
            if (entities.Count == 0) return (null, Vector2I.Zero);

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

        #endregion
    }
}
