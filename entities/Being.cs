using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Actions;
using System.Linq;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities
{
    public record BeingAttributes(
        float Strength,
        float Dexterity,
        float Constitution,
        float Intelligence,
        float Willpower,
        float Wisdom,
        float Charisma
    );

    public abstract partial class Being : CharacterBody2D, IEntity
    {
        [Export]
        protected uint _totalMoveTicks = 4; // How many ticks it takes to move one tile
        protected uint _remainingMoveTicks = 0; // Time in seconds to move one tile
        protected bool _isMoving = false;

        /// <summary>
        /// Attributes for a perfectly "average" Being 
        /// </summary>
        public static readonly BeingAttributes BaseAttributesSet = new(
            10.0f,
            10.0f,
            10.0f,
            10.0f,
            10.0f,
            10.0f,
            10.0f
        );
        public abstract BeingAttributes DefaultAttributes { get; }

        public BeingAttributes Attributes { get; protected set; }

        public uint MaxSenseRange = 10;

        // Body system
        public BodyHealth Health { get; protected set; }
        protected Dictionary<string, BodyPartGroup> _bodyPartGroups
        {
            get => Health.BodyPartGroups;
        }
        protected bool BodyStructureInitialized
        {
            get => Health.BodyStructureInitialized;
        }

        protected Vector2 _targetPosition;
        protected Vector2 _startPosition;
        protected float _moveProgress = 1.0f; // 1.0 means movement complete
        protected Vector2 _direction = Vector2.Zero;
        protected AnimatedSprite2D _animatedSprite;
        protected Vector2I _currentGridPos;

        // Reference to the grid system
        public GridSystem _gridSystem { get; protected set; }

        // Trait system
        public List<ITrait> _traits { get; protected set; } = [];
        public Dictionary<SenseType, float> DetectionDifficulties { get; protected set; }
        // Memory system to track what the entity has perceived
        private Dictionary<Vector2I, Dictionary<string, object>> _memory = new();

        // Track when memory was last updated for each position
        private Dictionary<Vector2I, uint> _memoryTimestamps = new();
        protected uint MemoryDuration = 3_000; // Roughly 5 minutes game time

        public override void _Ready()
        {
            _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            _animatedSprite.Play("idle");
        }

        public virtual void Initialize(GridSystem gridSystem, Vector2I startGridPos, BeingAttributes attributes = null)
        {
            _gridSystem = gridSystem;
            _currentGridPos = startGridPos;

            Name = $"{GetType().Name}-{Guid.NewGuid().ToString("N")[..8]}";

            // Set initial position aligned to the grid
            Position = _gridSystem.GridToWorld(_currentGridPos);
            _targetPosition = Position;
            _startPosition = Position;

            // Mark being's initial position as occupied
            _gridSystem.SetCellOccupied(_currentGridPos, true);

            // Set attributes if provided
            Attributes = attributes ?? DefaultAttributes;

            Health = new BodyHealth(this);

            // Initialize body structure if not already done
            if (!BodyStructureInitialized)
            {
                InitializeBodyStructure();
                InitializeBodySystems();
            }

            // Initialize all traits
            foreach (var trait in _traits)
            {
                trait.Initialize(this, Health);
            }

            Health.PrintSystemStatuses();
        }

        // Allows easy calling of Default implemenation methods
        public IEntity selfAsEntity()
        {
            return this;
        }
        public Vector2I GetGridPosition()
        {
            return _currentGridPos;
        }

        public SensableType GetSensableType()
        {
            return SensableType.Being;
        }

        // Method to handle body structure initialization - can be overridden by subclasses
        protected virtual void InitializeBodyStructure() => Health.InitializeHumanoidBodyStructure();
        protected virtual void InitializeBodySystems() => Health.InitializeBodySystems();

        public virtual EntityAction Think(Vector2 currentPosition, ObservationData observationData)
        {
            // Ask each trait for suggested actions
            List<EntityAction> possibleActions = [];

            var currentPerception = ProcessPerception(observationData);
            SetMemory(currentPosition, currentPerception);

            foreach (var trait in _traits)
            {
                var suggestedAction = trait.SuggestAction(currentPosition, currentPerception);
                if (suggestedAction != null)
                {
                    possibleActions.Add(suggestedAction);
                }
            }

            // Choose the highest priority action or default to idle
            if (possibleActions.Count > 0)
            {
                var action = possibleActions.OrderByDescending(a => a.Priority).First();
                return action;
            }

            // Default idle behavior
            return new IdleAction(this);
        }
        public virtual int GetSightRange()
        {
            if (!HasSenseType(SenseType.Sight))
                return 0;

            // Base sight range
            int baseRange = 8;

            // Modify by sight system efficiency
            float sightEfficiency = Health.GetSystemEfficiency(BodySystemType.Sight);

            // Calculate final range (minimum 1 if has sight)
            return Math.Max(1, Mathf.RoundToInt(baseRange * sightEfficiency));
        }

        public virtual bool HasSenseType(SenseType senseType)
        {
            switch (senseType)
            {
                case SenseType.Sight:
                    return !Health.BodySystems[BodySystemType.Sight].Disabled;

                case SenseType.Hearing:
                    return !Health.BodySystems[BodySystemType.Hearing].Disabled;

                case SenseType.Smell:
                    return !Health.BodySystems[BodySystemType.Smell].Disabled;

                default:
                    return false;
            }
        }

        public virtual float GetPerceptionLevel(SenseType senseType)
        {
            return 1.0f;
        }

        protected virtual bool IsLOSBlocking(Vector2I position)
        {
            // Check if the world has terrain that blocks sight at this position
            var world = GetTree().GetFirstNodeInGroup("World") as World;
            if (world == null)
                return false;

            // First check if the cell is occupied (entities block sight)
            // But we allow seeing the entities themselves
            if (world.IsCellOccupied(position))
            {
                // Check if this is an entity or terrain
                // For now, we'll use a simplified approach:
                // Check the terrain type at this position
                var groundLayer = world.GetNode<TileMapLayer>("GroundLayer");
                if (groundLayer != null)
                {
                    // Get the tile data at this position
                    var tileData = groundLayer.GetCellTileData(position);

                    // Check if it's a water tile (special case example)
                    if (tileData != null && groundLayer.GetCellAtlasCoords(position) == world.GetNode<GridSystem>("GridSystem").WaterAtlasCoords)
                    {
                        // Water doesn't block sight
                        return false;
                    }
                }

                // For buildings, walls, trees, etc.
                // These would block sight, but for now we'll use a simplified check
                // Are there specific entities at this position that block sight?
                foreach (var entity in world.GetEntities())
                {
                    if (entity.GetCurrentGridPosition() == position)
                    {
                        return true;
                    }
                }
            }

            // Default: not blocking
            return false;
        }

        // Store perception data in memory
        protected virtual void SetMemory(Vector2 currentPosition, Perception currentPerception)
        {
            // Store sensed entities in memory
            foreach (var entityData in currentPerception.GetEntitiesOfType<Being>())
            {
                var entity = entityData.entity;
                var pos = entityData.position;

                // Create or update memory for this position
                if (!_memory.ContainsKey(pos))
                {
                    _memory[pos] = new Dictionary<string, object>();
                }

                // Store entity information
                string entityKey = $"entity_{entity.GetType().Name}";
                _memory[pos][entityKey] = entity;

                // Update timestamp
                _memoryTimestamps[pos] = MemoryDuration;
            }

            // Periodically clean up old memories
            CleanupOldMemories();
        }

        // Remove memories that are too old
        private void CleanupOldMemories()
        {
            var posToRemove = new List<Vector2I>();

            foreach (var entry in _memoryTimestamps)
            {
                if (entry.Value <= 0)
                {
                    _memory.Remove(entry.Key);
                }
                else
                {
                    _memoryTimestamps[entry.Key]--;
                }
            }
        }

        // Get what the entity remembers about a position
        public Dictionary<string, object> GetMemoryAt(Vector2I position)
        {
            if (_memory.TryGetValue(position, out var memory))
            {
                return new Dictionary<string, object>(memory);
            }

            return new Dictionary<string, object>();
        }

        // Check if entity has any memory of a specific entity type
        public bool HasMemoryOfEntityType<T>() where T : Being
        {
            foreach (var posMemory in _memory.Values)
            {
                foreach (var entry in posMemory)
                {
                    if (entry.Key.StartsWith("entity_") && entry.Value is T)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Get overall health percentage
        public float GetHealthPercentage()
        {
            float totalHealth = 0;
            float totalImportance = 0;

            foreach (var group in _bodyPartGroups.Values)
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
                return "Critical";
            else if (health <= 0.3f)
                return "Severely Injured";
            else if (health <= 0.6f)
                return "Injured";
            else if (health <= 0.9f)
                return "Lightly Injured";
            else
                return "Healthy";
        }

        // Get overall efficiency for performing tasks
        public float GetEfficiency()
        {
            float efficiency = 0;
            float totalImportance = 0;

            foreach (var group in _bodyPartGroups.Values)
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
            if (_bodyPartGroups.TryGetValue(groupName, out var group))
            {
                var part = group.Parts.FirstOrDefault(p => p.Name == partName);
                part?.TakeDamage(amount);
            }
        }

        // Heal a specific body part
        public void HealBodyPart(string groupName, string partName, float amount)
        {
            if (_bodyPartGroups.TryGetValue(groupName, out var group))
            {
                var part = group.Parts.FirstOrDefault(p => p.Name == partName);
                part?.Heal(amount);
            }
        }

        // Move to a specific grid position if possible
        public bool TryMoveToGridPosition(Vector2I targetGridPos)
        {
            // Check if the target cell is free
            if (!_gridSystem.IsCellOccupied(targetGridPos))
            {
                // Free the current cell
                _gridSystem.SetCellOccupied(_currentGridPos, false);

                // Update current grid position
                _currentGridPos = targetGridPos;

                // Mark new cell as occupied
                _gridSystem.SetCellOccupied(_currentGridPos, true);

                // Start moving
                _startPosition = Position;
                _targetPosition = _gridSystem.GridToWorld(_currentGridPos);
                _remainingMoveTicks = _totalMoveTicks;
                _isMoving = true;

                Vector2 moveDirection = (_targetPosition - _startPosition).Normalized();
                _direction = moveDirection;

                // Handle animation and facing direction
                if (_direction.X > 0)
                    _animatedSprite.FlipH = false;
                else if (_direction.X < 0)
                    _animatedSprite.FlipH = true;

                _animatedSprite.Play("walk");

                return true;
            }

            return false;
        }

        public void ProcessMovementTick()
        {
            if (_isMoving && _remainingMoveTicks > 0)
            {
                _remainingMoveTicks--;

                // Calculate movement progress
                float progress = 1.0f - (_remainingMoveTicks / (float)_totalMoveTicks);

                // Update position based on interpolation
                Position = _startPosition.Lerp(_targetPosition, progress);

                // Check if movement is complete
                if (_remainingMoveTicks <= 0)
                {
                    Position = _targetPosition; // Ensure exact position
                    _isMoving = false;

                    // If no direction, play idle animation
                    if (_direction == Vector2.Zero)
                    {
                        _animatedSprite.Play("idle");
                    }
                }
            }
        }

        public bool IsMoving()
        {
            return _isMoving;
        }

        // Set a new direction for the being
        public void SetDirection(Vector2 newDirection)
        {
            _direction = newDirection;
        }

        // Get the current grid position
        public Vector2I GetCurrentGridPosition()
        {
            return _currentGridPos;
        }

        // Get the grid system (for traits that need it)
        public GridSystem GetGridSystem()
        {
            return _gridSystem;
        }

        // Process traits in the physics update
        public override void _PhysicsProcess(double delta)
        {
            // Process all traits
            foreach (var trait in _traits)
            {
                trait.Process(delta);
            }
        }
        protected virtual Perception ProcessPerception(ObservationData data)
        {
            var perception = new Perception();

            // Process grid contents
            foreach (var pos in data.Grid.GetCoveredPositions())
            {
                foreach (var sensable in data.Grid.GetAtPosition(pos))
                {
                    // Apply Line of Sight and other filtering
                    if (CanPerceive(sensable, pos))
                    {
                        perception.AddDetectedSensable(sensable, pos);
                    }
                }
            }

            // Process events
            foreach (var evt in data.Events)
            {
                if (CanPerceiveEvent(evt))
                {
                    perception.AddPerceivedEvent(evt);
                }
            }

            // Process Dijkstra maps (no filtering needed - already relevant)
            foreach (var map in data.DijkstraMaps)
            {
                perception.AddDijkstraMap(map);
            }

            return perception;
        }

        // Determines if this entity can perceive a specific sensable
        protected virtual bool CanPerceive(ISensable sensable, Vector2I position)
        {
            // Check sensable type
            var sensableType = sensable.GetSensableType();

            // Apply different strategies based on sense type

            // Visual perception - requires line of sight
            if (HasSenseType(SenseType.Sight))
            {
                int sightRange = GetSightRange();
                Vector2I myPos = GetCurrentGridPosition();

                // Check distance
                int distance = Math.Max(
                    Math.Abs(position.X - myPos.X),
                    Math.Abs(position.Y - myPos.Y)
                );

                if (distance <= sightRange && HasLineOfSight(position))
                {
                    // Check detection difficulty
                    float detectionDifficulty = sensable.GetDetectionDifficulty(SenseType.Sight);
                    float perceptionLevel = GetPerceptionLevel(SenseType.Sight);

                    if (perceptionLevel >= detectionDifficulty)
                        return true;
                }
            }

            // Hearing - no line of sight needed but affected by distance
            if (HasSenseType(SenseType.Hearing))
            {
                // Similar hearing checks
            }

            // Smell - affected by wind direction, etc.
            if (HasSenseType(SenseType.Smell))
            {
                // Smell checks
            }

            return false;
        }

        // Calculates line of sight using Bresenham's algorithm
        protected virtual bool HasLineOfSight(Vector2I target)
        {
            Vector2I start = GetCurrentGridPosition();

            // Bresenham's line algorithm
            int x0 = start.X;
            int y0 = start.Y;
            int x1 = target.X;
            int y1 = target.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (x0 != x1 || y0 != y1)
            {
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }

                // Skip starting position
                if (x0 == start.X && y0 == start.Y)
                    continue;

                // Check if this position blocks sight
                if (IsLOSBlocking(new Vector2I(x0, y0)))
                    return false;
            }

            return true;
        }

        protected virtual bool CanPerceiveEvent(WorldEvent worldEvent)
        {
            // Get distance to event
            Vector2I myPos = GetCurrentGridPosition();
            float distance = myPos.DistanceTo(worldEvent.Position);

            // Base detection range is the event radius plus a small buffer
            float baseDetectionRange = worldEvent.Radius + 1f;

            // Check if event is in range based on event type
            switch (worldEvent.Type)
            {
                case EventType.Visual:
                    // Visual events require line of sight and sight capability
                    if (!HasSenseType(SenseType.Sight))
                        return false;

                    // Check if event is within sight range
                    if (distance > GetSightRange() + baseDetectionRange)
                        return false;

                    // Check line of sight to the event's position
                    if (!HasLineOfSight(worldEvent.Position))
                        return false;

                    // Higher intensity visual events are more noticeable
                    float visualDetectionChance = worldEvent.Intensity * GetPerceptionLevel(SenseType.Sight);

                    // Apply distance falloff
                    visualDetectionChance *= 1.0f - (distance / (GetSightRange() + baseDetectionRange));

                    // Apply trait modifiers
                    foreach (var trait in _traits)
                    {
                        if (trait is Traits.UndeadTrait)
                        {
                            // Undead might have worse visual perception
                            visualDetectionChance *= 0.8f;
                        }
                    }

                    // Random chance based on calculated probability
                    return new RandomNumberGenerator().Randf() < visualDetectionChance;

                case EventType.Sound:
                    // Sound events don't require line of sight
                    if (!HasSenseType(SenseType.Hearing))
                        return false;

                    // Sound range is based on intensity
                    float soundRange = baseDetectionRange + (worldEvent.Intensity * 15f);

                    // Check if event is within hearing range
                    if (distance > soundRange)
                        return false;

                    // Calculate detection chance
                    float soundDetectionChance = worldEvent.Intensity * GetPerceptionLevel(SenseType.Hearing);

                    // Apply distance falloff (sound drops with square of distance)
                    soundDetectionChance *= 1.0f - ((distance * distance) / (soundRange * soundRange));

                    // Apply trait modifiers
                    foreach (var trait in _traits)
                    {
                        // Trait-specific modifiers could go here
                    }

                    // Random chance based on calculated probability
                    return new RandomNumberGenerator().Randf() < soundDetectionChance;

                case EventType.Smell:
                    // Smell doesn't require line of sight
                    if (!HasSenseType(SenseType.Smell))
                        return false;

                    // Smell range is based on intensity
                    float smellRange = baseDetectionRange + (worldEvent.Intensity * 8f);

                    // Check if event is within smell range
                    if (distance > smellRange)
                        return false;

                    // Calculate detection chance
                    float smellDetectionChance = worldEvent.Intensity * GetPerceptionLevel(SenseType.Smell);

                    // Apply distance falloff
                    smellDetectionChance *= 1.0f - (distance / smellRange);

                    // Apply trait modifiers
                    foreach (var trait in _traits)
                    {
                        if (trait is Traits.UndeadTrait)
                        {
                            // Undead have heightened sense of smell for certain things
                            smellDetectionChance *= 1.5f;
                        }
                    }

                    // Random chance based on calculated probability
                    return new RandomNumberGenerator().Randf() < smellDetectionChance;

                case EventType.Environmental:
                    // Environmental events can be detected by any sense
                    bool canDetect = false;

                    // Try each sense type
                    if (HasSenseType(SenseType.Sight) && distance <= GetSightRange())
                    {
                        canDetect = true;
                    }
                    else if (HasSenseType(SenseType.Hearing) && distance <= baseDetectionRange + (worldEvent.Intensity * 10f))
                    {
                        canDetect = true;
                    }
                    else if (HasSenseType(SenseType.Smell) && distance <= baseDetectionRange + (worldEvent.Intensity * 5f))
                    {
                        canDetect = true;
                    }

                    return canDetect;

                default:
                    return false;
            }
        }
    }
}
