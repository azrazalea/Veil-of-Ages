# Entity Resources

## Purpose

This directory contains JSON data files defining entity configurations using a data-driven approach. Entities (beings like villagers, undead, player) are configured through JSON definitions that are loaded at runtime by BeingResourceManager.

## Directory Structure

```
resources/entities/
├── definitions/          # Entity type definitions
│   ├── human_townsfolk.json
│   ├── mindless_skeleton.json
│   ├── mindless_zombie.json
│   └── player.json
└── sprites/              # Sprite definitions (static sprites using AtlasTexture)
    ├── human_townsfolk.json
    ├── skeleton_warrior.json
    ├── zombie.json
    └── player_default.json
```

## How Resources Are Loaded

Entity resources are loaded by `BeingResourceManager` (singleton) during initialization:

1. **Definitions** are loaded from `res://resources/entities/definitions/`
2. **Sprites** are loaded from `res://resources/entities/sprites/`

## Schema Overview

### BeingDefinition (definitions/*.json)

```json
{
  "Id": "unique_id",
  "Name": "Display Name",
  "Description": "Description text",
  "Category": "Human|Undead",
  "SpriteId": "reference_to_sprite_def",
  "Attributes": {
    "Strength": 10, "Dexterity": 10, "Constitution": 10,
    "Intelligence": 10, "Willpower": 10, "Wisdom": 10, "Charisma": 10
  },
  "Movement": {
    "BaseMovementPointsPerTick": 0.33
  },
  "Traits": [
    { "TraitType": "TraitClassName", "Priority": 0, "Parameters": {} }
  ],
  "Body": {
    "BaseStructure": "Humanoid",
    "Modifications": [
      { "Type": "ModificationType", "Parameters": {} }
    ]
  },
  "Audio": {
    "Sounds": { "soundName": "res://path/to/audio.mp3" }
  },
  "Tags": ["tag1", "tag2"]
}
```

### SpriteDefinition (sprites/*.json)

Entities now use static sprites (AtlasTexture) instead of animations. The sprite system supports either:

**Multi-layer sprites** (e.g., player with customizable appearance):
```json
{
  "Id": "sprite_id",
  "Name": "Display Name",
  "TexturePath": "res://path/to/atlas.png",
  "SpriteSize": [32, 32],
  "Layers": [
    { "Name": "base", "Row": 0, "Col": 0 },
    { "Name": "body", "Row": 1, "Col": 2 },
    { "Name": "hair", "Row": 3, "Col": 1 }
  ]
}
```

**Single-sprite entities** (e.g., NPCs, monsters):
```json
{
  "Id": "sprite_id",
  "Name": "Display Name",
  "TexturePath": "res://path/to/atlas.png",
  "SpriteSize": [32, 32],
  "Row": 5,
  "Col": 3
}
```

## Body Modifications

Supported modification types:
- `RemoveSoftTissues` - Removes soft tissue and organs (for skeletal entities)
- `ScaleBoneHealth` - Multiplies bone part health (Parameters: `Multiplier`)
- `ApplyRandomDecay` - Applies random damage to body parts (Parameters: `MinParts`, `MaxParts`, `MinDamagePercent`, `MaxDamagePercent`)

## Creating New Entity Types

1. Create a new definition JSON in `definitions/`
2. Create a corresponding sprite JSON in `sprites/` (or reuse existing)
3. Reference existing traits or create new ones
4. Use `GenericBeing.CreateFromDefinition(id)` to spawn

## Dependencies

- **BeingResourceManager**: `entities/beings/BeingResourceManager.cs` - Loads all resources
- **BeingDefinition**: `entities/beings/BeingDefinition.cs` - Definition data structure
- **SpriteDefinition**: `entities/beings/SpriteDefinition.cs` - Sprite data structure
- **GenericBeing**: `entities/beings/GenericBeing.cs` - Runtime entity class
- **TraitFactory**: `entities/traits/TraitFactory.cs` - Creates traits from definitions

## Important Notes

- All JSON files use `PropertyNameCaseInsensitive = true` for parsing
- SpriteId references must match an Id in the sprites/ directory
- TraitType must match an existing trait class name exactly
- Tags are used for filtering/categorization (e.g., "undead", "living", "mindless")
- Entities use static sprites (Sprite2D + AtlasTexture), not animations
- Decorations still use the old animation system (AnimatedSprite2D + SpriteAnimationDefinition)
