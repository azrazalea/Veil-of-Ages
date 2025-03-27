using Godot;
using System;
using System.Collections.Generic;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.BeingServices
{
    public class BeingPerceptionSystem
    {
        private Being _owner;
        private Dictionary<Vector2I, Dictionary<string, object>> _memory = new();
        private Dictionary<Vector2I, uint> _memoryTimestamps = new();
        protected uint _memoryDuration = 3_000; // Roughly 5 minutes game time

        public BeingPerceptionSystem(Being owner)
        {
            _owner = owner;
        }

        // Process raw observation data into perceived entities and events
        public Perception ProcessPerception(ObservationData data)
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

            // Update memory with current perception
            StorePerceptionInMemory(perception);

            return perception;
        }

        // Store perception data in memory
        protected void StorePerceptionInMemory(Perception currentPerception)
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
                _memoryTimestamps[pos] = _memoryDuration;
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

        // Determines if this entity can perceive a specific sensable
        protected bool CanPerceive(ISensable sensable, Vector2I position)
        {
            // Check sensable type
            var sensableType = sensable.GetSensableType();

            // Visual perception - requires line of sight
            int sightRange = GetSightRange();
            Vector2I myPos = _owner.GetCurrentGridPosition();

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

            // Hearing - no line of sight needed but affected by distance
            if (HasSenseType(SenseType.Hearing))
            {
                // Hearing checks (simplified for now)
                // Could be expanded with more detailed hearing rules
            }

            // Smell - affected by wind direction, etc.
            if (HasSenseType(SenseType.Smell))
            {
                // Smell checks (simplified for now)
                // Could be expanded with more detailed smell rules
            }

            return false;
        }

        // Calculates line of sight using Bresenham's algorithm
        public bool HasLineOfSight(Vector2I target)
        {
            Vector2I start = _owner.GetCurrentGridPosition();

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

        protected bool IsLOSBlocking(Vector2I position)
        {
            // Default implementation - override in subclasses if needed
            return false;
        }

        protected bool CanPerceiveEvent(WorldEvent worldEvent)
        {
            // Get distance to event
            Vector2I myPos = _owner.GetCurrentGridPosition();
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

                    // Random chance based on calculated probability
                    return new RandomNumberGenerator().Randf() < soundDetectionChance;

                case EventType.Smell:
                    // Similar implementation for smell events
                    return false; // Simplified for now

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

        // Helper methods that delegate to the owner
        private int GetSightRange()
        {
            return _owner.GetSightRange();
        }

        private float GetPerceptionLevel(SenseType senseType)
        {
            return _owner.GetPerceptionLevel(senseType);
        }

        private bool HasSenseType(SenseType senseType)
        {
            return _owner.HasSenseType(senseType);
        }
    }
}
