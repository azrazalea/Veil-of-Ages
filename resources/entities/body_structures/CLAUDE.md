# Body Structure Definitions

## Purpose

This directory contains JSON files that define body structure configurations for entities. Body structures define body part groups, individual body parts with their health and importance values, and body systems that track functional capabilities.

## Schema

### BodyStructureDefinition

```json
{
  "Id": "unique_id",
  "Name": "Display Name",
  "Description": "Description text",
  "Groups": {
    "GroupName": {
      "Parts": [
        {
          "Name": "Part Name",
          "MaxHealth": 50,
          "Importance": 0.5,
          "PainSensitivity": 0.5,
          "Flags": ["vital", "organ", "bone", "sensory"]
        }
      ]
    }
  },
  "Systems": {
    "SystemName": {
      "Contributors": { "PartName": 1.0 },
      "Fatal": true,
      "MinEfficiency": 0.0
    }
  }
}
```

### Field Descriptions

#### BodyStructureDefinition
- **Id** (required): Unique identifier for this body structure
- **Name**: Human-readable display name
- **Description**: Description text
- **Groups** (required): Dictionary of body part groups
- **Systems** (required): Dictionary of body systems

#### BodyPartGroupDefinition
- **Parts** (required): List of body parts in this group

#### BodyPartDefinition
- **Name** (required): Name of the body part
- **MaxHealth**: Maximum health points (default: 50)
- **Importance**: Weight for overall health calculations (0.0-1.0, default: 0.5)
- **PainSensitivity**: How much pain this part contributes when damaged (default: 0.5)
- **Flags**: Optional list of flags for categorization:
  - `vital`: Part is critical for survival
  - `organ`: Part is a soft tissue organ (removed for skeletal entities)
  - `bone`: Part is a bone (scaled for skeletal entities)
  - `sensory`: Part is involved in senses

#### BodySystemDefinition
- **Contributors** (required except for Pain): Dictionary mapping body part names to contribution weights (0.0-1.0)
- **Fatal**: If true, system failure results in death
- **MinEfficiency**: Minimum efficiency before system fails (default: 0.0)

## Body Systems Reference

| System | Fatal | Purpose |
|--------|-------|---------|
| Consciousness | Yes | Awareness and decision-making (Brain) |
| Sight | No | Visual perception (Eyes) |
| Hearing | No | Auditory perception (Ears) |
| Smell | No | Olfactory perception (Nose) |
| Moving | No | Locomotion (Legs, Feet) |
| Manipulation | No | Object handling (Arms, Hands) |
| Talking | No | Speech (Jaw, Neck) |
| Communication | No | General communication (Jaw, Neck) |
| Breathing | Yes | Oxygen intake (Lungs) |
| BloodFiltration | Yes | Toxin removal (Kidneys, Liver) |
| BloodPumping | Yes | Blood circulation (Heart) |
| Digestion | Yes | Food processing (Stomach) |
| Pain | Special | Calculated from all part damage (no contributors) |

## Files

### humanoid.json
Standard humanoid body structure with:
- 10 body part groups (Torso, UpperHead, FullHead, Shoulders, Arms, Hands, LeftHand, RightHand, Legs, Feet)
- 13 body systems
- Used by humans, undead, and player entities

## Usage

Body structures are loaded by `BodyStructureResourceManager` at startup. To use a body structure:

```csharp
// In Being or BodyHealth initialization
var definition = BodyStructureResourceManager.Instance.GetDefinition("humanoid");
Health.InitializeFromDefinition(definition);
```

## Validation Rules

The resource manager performs strict validation:
1. `Id` must be non-empty
2. `Groups` must contain at least one group
3. Each group must have at least one part
4. Each part must have a non-empty `Name`
5. `Systems` must contain at least one system
6. Each system (except Pain) must have at least one contributor

**NO FALLBACKS**: If validation fails, the game crashes with a clear error message.

## Dependencies

- **BodyStructureResourceManager**: `entities/beings/health/BodyStructureResourceManager.cs` - Loads definitions
- **BodyStructureDefinition**: `entities/beings/health/BodyStructureDefinition.cs` - Data structures
- **BodyHealth**: `entities/beings/health/BodyHealth.cs` - Consumes definitions to create body parts

## Creating New Body Structures

1. Create a new JSON file in this directory
2. Define all required groups and systems
3. Ensure all part names referenced in systems exist in some group
4. The structure will be loaded automatically on game startup
