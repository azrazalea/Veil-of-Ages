# /entities/beings/health

## Purpose

This directory implements a detailed body simulation system inspired by games like Dwarf Fortress and RimWorld. It provides body parts organized into groups, body systems that track functionality, and health/damage mechanics. This enables nuanced combat, injury effects, and differentiation between living and undead entities.

## Files

### BodyHealth.cs
Main health management class that coordinates body parts and systems.

**Key Features:**
- Dictionary of `BodyPartGroup` instances by name
- Dictionary of `BodySystem` instances by type
- Humanoid body structure initialization
- Soft tissue removal for undead entities
- System efficiency calculations

**Key Methods:**
- `InitializeHumanoidBodyStructure()` - Sets up standard humanoid body parts
- `InitializeBodySystems()` - Configures body systems with contributors
- `GetSystemEfficiency(BodySystemType)` - Calculate weighted efficiency
- `DisableBodySystem(BodySystemType)` - Disable a system (for undead)
- `RemoveSoftTissuesAndOrgans()` - Remove organs (for skeletons)
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

## Key Classes/Interfaces

| Type | Description |
|------|-------------|
| `BodyHealth` | Main coordinator for body simulation |
| `BodySystem` | Functional capability (sight, movement, etc.) |
| `BodyPart` | Individual anatomical part with health |
| `BodyPartGroup` | Collection of related parts |
| `BodySystemType` | Enum of all system types |
| `BodyPartStatus` | Enum of health states |

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

### Soft Tissues Array
Organs that can be removed for skeletal entities:
```csharp
["Stomach", "Heart", "Lungs", "Kidneys", "Liver", "Eyes", "Nose", "Gonads", "Genitals"]
```

### Body Part Properties
- `MaxHealth` - Maximum hit points
- `CurrentHealth` - Current hit points
- `Importance` - Weight for overall health calculations
- `PainSensitivity` - How much pain this part contributes
- `IsBonePart()` - Helper to identify skeletal parts

### Bone Part Detection
Parts considered "bones" (for skeleton strengthening):
- Explicitly named: Skull, Ribs, Spine, Pelvis, Sternum, Jaw, Clavicles
- Bone pairs: Femurs, Tibiae, Humeri, Radii
- Extremities: Fingers (any containing "Fingers"), Toes
- Any part with "bone" in the name

## Dependencies

### Depends On
- Godot base types (for GD.Print)
- Standard .NET collections

### Depended On By
- `VeilOfAges.Entities.Being` - Health property
- All Being subclasses - Body structure customization
- `VeilOfAges.Entities.Traits.UndeadTrait` - Disables living systems
- `VeilOfAges.Entities.BeingServices.BeingPerceptionSystem` - Sense efficiency
