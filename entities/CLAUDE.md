# /entities

## Purpose

This directory contains the core entity system for Veil of Ages. It defines the fundamental building blocks for all game entities including beings (living and undead), buildings, and terrain objects. The architecture follows an Entity-Component-Trait pattern where `Being` is the base class and behaviors are composed through modular traits.

## Files

### Being.cs
The central abstract class for all living and undead entities. Extends Godot's `CharacterBody2D` and implements `IEntity<BeingTrait>`. Manages:
- Attribute system (Strength, Dexterity, Constitution, Intelligence, Willpower, Wisdom, Charisma)
- Health/body part system integration
- Trait collection and initialization queue
- Movement delegation to `MovementController`
- AI decision-making via `Think()` method with priority queue
- Dialogue system integration
- Perception and sensory capabilities

### BeingTrait.cs
Specialized trait base class for Being entities. Provides:
- PathFinder integration for movement
- Memory system with timestamps for entity recollection
- Perception helpers for finding entities by type
- Movement helpers (MoveToPosition, MoveNearEntity, MoveToArea, TryToWander)
- Range checking utilities
- Dialogue response generation interface

### EntityAction.cs
Abstract base class for all actions an entity can perform. Uses priority-based execution (lower values = higher priority). Contains:
- Reference to the executing entity
- Priority value for action queue sorting
- Source tracking (which class generated the action)
- Optional callbacks for selection and success events

### EntityThinkingSystem.cs
Multi-threaded AI processing system. A Godot Node that:
- Registers and manages all Being entities
- Processes entity thinking in parallel using semaphore-controlled threads
- Collects pending actions and applies them on the main thread
- Sorts actions by priority before execution
- Handles movement tick processing after action execution

### IEntity.cs
Generic interface for trait-based entities. Provides:
- Trait collection management (`_traits` SortedSet)
- `AddTrait<T>()` and `AddTraitToQueue<T>()` methods for trait registration
- `GetTrait<T>()` and `HasTrait<T>()` for trait queries
- Event broadcasting to all traits via `OnTraitEvent()`

### Trait.cs
Base class for all traits. Implements `IComparable` for priority-based sorting. Contains:
- Initialization state tracking
- Priority property for execution order
- Random number generator for behavioral variety
- Virtual Process() and OnEvent() methods

### SensorySystem.cs
World-level sensory coordination system. Manages:
- Observation data caching per frame
- Spatial partitioning for efficient entity lookup
- ObservationGrid creation for each entity's perception range
- Event system integration for world events

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `Being` | Abstract base for all living/undead entities |
| `BeingTrait` | Specialized trait with Being-specific helpers |
| `EntityAction` | Base class for executable actions |
| `EntityThinkingSystem` | Multi-threaded AI coordinator |
| `IEntity<T>` | Generic interface for trait-based entities |
| `Trait` | Base trait class with priority sorting |
| `SensorySystem` | World-level perception management |
| `BeingAttributes` | Record type for entity attributes |

## Important Notes

### Threading Considerations
- `EntityThinkingSystem.ProcessGameTick()` is async and uses `Task.WhenAll()` for parallel processing
- Entity `Think()` methods run on background threads - avoid direct scene tree manipulation
- Actions are queued and executed on the main thread in `ApplyAllPendingActions()`
- Semaphore limits concurrent processing to `Environment.ProcessorCount - 1`

### Trait Initialization
- Traits use a queue-based initialization system to handle dependencies
- Traits added during initialization are automatically queued for processing
- `IsInitialized` flag prevents double initialization
- Use `AddTraitToQueue<T>()` for cleaner trait registration during initialization

### Action Priority System
- Lower priority values execute first (0 = highest priority)
- Actions are sorted before execution each tick
- Dialogue commands have a specific priority (`TalkCommand.Priority`) that affects behavior

### Memory System
- BeingTrait includes a memory system with position-based storage
- Memory has configurable duration (default 3000 ticks, roughly 5 minutes game time)
- Memory is automatically cleaned up each tick

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Grid system and pathfinding
- `VeilOfAges.Core.Lib` - Utilities and time system
- `VeilOfAges.UI` - Dialogue system and commands
- Godot engine classes (CharacterBody2D, Node, etc.)

### Depended On By
- All subdirectories within `/entities/`
- `/core/` - GameController references EntityThinkingSystem
- `/world/` - World instantiates and manages entities
