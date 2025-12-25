# /entities/beings/undead/mindless

## Purpose

This directory contains mindless (non-sapient) undead entity implementations. These are creatures that cannot engage in complex dialogue and operate purely on instinct. They form the basic undead workforce and threats in the necromancy simulation.

## Subdirectories

### /skeleton
Contains `MindlessSkeleton` - territorial skeletal undead.

### /zombie
Contains `MindlessZombie` - hunger-driven shambling undead.

## Common Characteristics

### MindlessTrait Effects
All entities here use `MindlessTrait` which:
- Limits dialogue options to non-complex commands
- Provides generic dialogue responses ("blank stare", "silently obeys")
- Prevents complex reasoning/decision making

### Dialogue Behavior
```csharp
// Complex commands are refused
public override bool IsOptionAvailable(DialogueOption option)
{
    if (option.Command == null) return true;
    return !option.Command.IsComplex;
}
```

### Body Modifications
Both skeleton and zombie types modify the standard humanoid body:
- **Skeletons**: Remove soft tissues, strengthen bones by 50%
- **Zombies**: Random decay damage to 2-5 non-vital body parts

## Important Notes

### Mindless vs Sapient
The "mindless" classification indicates:
- No complex reasoning capability
- Cannot disobey direct commands (unless physically impossible)
- Limited memory and planning
- Suitable for simple labor tasks

Future "sapient" undead (liches, vampires) would go in `/beings/undead/sapient/`.

### Audio Feedback
Both entity types have audio components:
- Skeletons: Bone rattle sound on movement/detection
- Zombies: Groan sound on hunger satisfaction/wandering

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Base class
- `VeilOfAges.Entities.Traits.MindlessTrait` - Dialogue limitations
- `VeilOfAges.Entities.Traits.UndeadTrait` - Undead properties

### Depended On By
- Necromancy command system
- World spawning systems
