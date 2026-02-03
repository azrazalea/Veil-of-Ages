# /entities/beings/health

## Purpose

This directory implements a detailed body simulation system inspired by games like Dwarf Fortress and RimWorld. It provides body parts organized into groups, body systems that track functionality, and health/damage mechanics. This enables nuanced combat, injury effects, and differentiation between living and undead entities.

## Data-Driven Architecture

Body structures are defined in JSON files and loaded at runtime by `BodyStructureResourceManager`. This replaces the hardcoded body initialization with a flexible data-driven system.

**NO FALLBACKS**: If JSON files fail to load or validate, the game crashes with a clear error message. This follows the "fail early, fail loud" principle.

### Resource Loading Flow
1. `BodyStructureResourceManager` loads from `res://resources/entities/body_structures/`
2. Each JSON file defines groups, parts, and systems
3. `BodyHealth.InitializeHumanoidBodyStructure()` calls the resource manager
4. `BodyHealth.InitializeFromDefinition()` creates runtime objects from JSON

## Files

### BodyHealth.cs
Main health management class that coordinates body parts and systems.

**Key Features:**
- Dictionary of `BodyPartGroup` instances by name
- Dictionary of `BodySystem` instances by type
- Data-driven body structure initialization from JSON
- Soft tissue removal for undead entities (uses "organ" flag from JSON)
- System efficiency calculations

**Key Methods:**
- `InitializeHumanoidBodyStructure()` - Loads "humanoid" definition from JSON and initializes body (NO FALLBACK)
- `InitializeFromDefinition(BodyStructureDefinition)` - Creates body parts and systems from JSON definition
- `GetSystemEfficiency(BodySystemType)` - Calculate weighted efficiency
- `DisableBodySystem(BodySystemType)` - Disable a system (for undead)
- `RemoveSoftTissuesAndOrgans()` - Remove organs (uses parts with "organ" flag)
- `GetSystemStatus(BodySystemType)` - Human-readable status string

### BodyPartSystem.cs
Core types for the body part and system simulation.

**Enums:**
- `BodySystemType` - All body system types (Consciousness, Sight, Moving, etc.)
- `BodyPartStatus` - Health states (Healthy, Injured, SeverelyInjured, Destroyed, Missing)

**Classes:**
- `BodySystem` - A functional system with contributors and fatality flag
- `BodyPart` - Individual body part with health, importance, and pain sensitivity
- `BodyPartGroup` - Collection of related body parts

### BodyStructureDefinition.cs
JSON-serializable definition classes for body structure data.

**Classes:**
- `BodyStructureDefinition` - Root definition with Id, Name, Groups, and Systems
- `BodyPartGroupDefinition` - Definition of a body part group
- `BodyPartDefinition` - Definition of a single body part with flags
- `BodySystemDefinition` - Definition of a body system with contributors

**Body Part Flags:**
- `vital` - Part is critical for survival
- `organ` - Part is soft tissue (removed for skeletal entities)
- `bone` - Part is bone (scaled for skeletal entities)
- `sensory` - Part is involved in senses

### BodyStructureResourceManager.cs
Singleton manager for loading body structure definitions from JSON.

**Key Methods:**
- `GetDefinition(id)` - Get a body structure definition (THROWS if not found)
- `HasDefinition(id)` - Check if definition exists
- `GetAllDefinitions()` - Get all loaded definitions

**Important:** This is a Godot autoload - registered in project.godot before BeingResourceManager.

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `BodyHealth` | Main coordinator for body simulation |
| `BodySystem` | Functional capability (sight, movement, etc.) |
| `BodyPart` | Individual anatomical part with health |
| `BodyPartGroup` | Collection of related parts |
| `BodySystemType` | Enum of all system types |
| `BodyPartStatus` | Enum of health states |
| `BodyStructureDefinition` | JSON-serializable body structure |
| `BodyStructureResourceManager` | Singleton loader for definitions |

## Body Systems Reference

| System | Fatal | Contributors |
|--------|-------|--------------|
| Consciousness | Yes | Brain (100%) |
| Sight | No | Eyes (100%) |
| Hearing | No | Ears (100%) |
| Smell | No | Nose (100%) |
| Moving | No | Legs (70%), Feet (30%) |
| Manipulation | No | Arms (40%), Hands (60%) |
| Talking | No | Jaw (70%), Neck (30%) |
| Communication | No | Jaw (50%), Neck (50%) |
| Breathing | Yes | Lungs (100%) |
| BloodFiltration | Yes | Kidneys (70%), Liver (30%) |
| BloodPumping | Yes | Heart (100%) |
| Digestion | Yes | Stomach (100%) |
| Pain | Special | All parts contribute |

## Important Notes

### Efficiency Calculation
- Each body part has an efficiency based on `CurrentHealth / MaxHealth`
- System efficiency is weighted average of contributor parts
- Parts with Destroyed or Missing status contribute 0
- Pain is calculated inversely (higher damage = more pain)

### Soft Tissues and Organs
Parts with the `organ` flag in JSON are automatically tracked in `SoftTissuesAndOrgans` list.
Used by `RemoveSoftTissuesAndOrgans()` for skeletal entities.

### Body Part Properties
- `MaxHealth` - Maximum hit points
- `CurrentHealth` - Current hit points
- `Importance` - Weight for overall health calculations
- `PainSensitivity` - How much pain this part contributes
- `IsBonePart()` - Helper to identify skeletal parts (checks name patterns)

### Bone Part Detection
Parts considered "bones" (for skeleton strengthening):
- Explicitly named: Skull, Ribs, Spine, Pelvis, Sternum, Jaw, Clavicles
- Bone pairs: Femurs, Tibiae, Humeri, Radii
- Extremities: Fingers (any containing "Fingers"), Toes
- Any part with "bone" in the name
- Note: The `bone` flag in JSON is for categorization; `IsBonePart()` still uses name patterns

### Error Handling
- **NO FALLBACKS**: All loading/validation failures throw exceptions
- Missing JSON files cause game to crash with clear error
- Invalid JSON syntax causes game to crash with clear error
- Missing required fields cause game to crash with clear error

## Dependencies

### Depends On
- `VeilOfAges.Core.Lib` - Log utility and JsonOptions
- Godot base types (Node for autoload)
- Standard .NET collections and System.Text.Json

### Depended On By
- `VeilOfAges.Entities.Being` - Health property
- All Being subclasses - Body structure customization
- `VeilOfAges.Entities.Traits.UndeadTrait` - Disables living systems
- `VeilOfAges.Entities.BeingServices.BeingPerceptionSystem` - Sense efficiency

## Related Resources

- JSON definitions: `res://resources/entities/body_structures/`
- See `resources/entities/body_structures/CLAUDE.md` for JSON schema documentation
