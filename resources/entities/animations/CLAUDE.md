# Entity Animation Definitions

## Purpose

Contains JSON files that define sprite animations for entities. Animation definitions are referenced by entity definitions via the AnimationId field and are used to create Godot SpriteFrames at runtime.

## Files

### human_townsfolk.json
Animations for human villagers.
- Sprite Size: 32x32
- Animations: idle (6 frames), walk (6 frames)
- Sprites now use Kenney atlas as single-frame placeholders

### skeleton_warrior.json
Animations for skeleton entities.
- Sprite Size: 32x32
- Animations: idle (7 frames from row 2, starting at column 2), walk (3 frames)
- Note: Idle animation uses specific row/column offsets
- Sprites now use Kenney atlas as single-frame placeholders

### zombie.json
Animations for zombie entities.
- Sprite Size: 32x32
- Animations: idle (5 frames), walk (4 frames)
- Sprites now use Kenney atlas as single-frame placeholders

### necromancer.json
Animations for the player necromancer using **multi-layer format**.
- Sprite Size: 32x32
- Uses `Layers` array with 5 DCSS player doll overlay layers:
  - **body**: human_female base (row 80, col 8)
  - **clothing_outer**: robe_black_gold (row 82, col 60)
  - **hair**: long_white (row 85, col 23)
  - **headwear**: hood_black_2 (row 92, col 41)
  - **held_item**: staff_skull (row 90, col 10)
- Layer names match slots defined in `being_base.json` SpriteLayers
- ZIndex for each layer comes from the definition slot, not the animation JSON

## Animation Data Formats

### Single-Layer Format (Legacy)

```json
{
  "Id": "unique_animation_id",
  "Name": "Human-readable name",
  "SpriteSize": [width, height],
  "Animations": {
    "animation_name": {
      "TexturePath": "res://path/to/spritesheet.png",
      "FrameWidth": 32,
      "FrameHeight": 32,
      "FrameCount": 6,
      "FrameRow": 0,
      "StartColumn": 0,
      "Speed": 5.0,
      "Loop": true
    }
  }
}
```

### Multi-Layer Format

```json
{
  "Id": "unique_animation_id",
  "Name": "Human-readable name",
  "SpriteSize": [width, height],
  "Layers": [
    {
      "Name": "body",
      "Animations": {
        "idle": { "TexturePath": "...", "FrameWidth": 32, "FrameHeight": 32, "FrameCount": 1, "FrameRow": 0, "StartColumn": 0, "Speed": 5.0, "Loop": true },
        "walk": { ... }
      }
    },
    {
      "Name": "clothing_outer",
      "Animations": { ... }
    }
  ]
}
```

Layer `Name` must match a slot name from the entity's `SpriteLayers` definition (inherited from `being_base.json`). ZIndex comes from the definition slot. When `Layers` is absent, the top-level `Animations` is treated as a single "body" layer (backwards compatible).

### Field Descriptions

- **TexturePath**: Godot resource path to the sprite sheet
- **FrameWidth/FrameHeight**: Size of each frame in pixels
- **FrameCount**: Number of frames in the animation
- **FrameRow**: Which row of the sprite sheet (0-indexed)
- **StartColumn**: Starting column offset (0-indexed)
- **Speed**: Animation playback speed (frames per second)
- **Loop**: Whether animation repeats

## Runtime Usage

- **Single-layer**: `SpriteAnimationDefinition.CreateSpriteFrames()` creates one SpriteFrames resource
- **Multi-layer**: `SpriteAnimationDefinition.CreateAllLayerSpriteFrames()` creates a list of `(name, SpriteFrames)` tuples
- `GetEffectiveLayers()` normalizes both formats — returns Layers if present, else wraps Animations as single "body" layer
- `GenericBeing.ConfigureAnimations()` creates one `AnimatedSprite2D` per layer with ZIndex from definition slots

## Creating New Animations

1. Look up sprites in atlas index files (`dcss_utumno_index.json`, `dcss_supplemental_index.json`, etc.)
2. Create a new JSON file with unique Id
3. For single-layer: use top-level `Animations` dict
4. For multi-layer: use `Layers` array with named layers matching `being_base.json` slots
5. Reference the animation Id in your entity definition's AnimationId field

## Standard Animation Names

These animation names are expected by the entity system:
- **idle**: Default standing animation (played automatically on spawn)
- **walk**: Movement animation (triggered during movement)

## Dependencies

- **SpriteAnimationDefinition**: `entities/beings/SpriteAnimationDefinition.cs` - Parses and creates SpriteFrames
- **BeingResourceManager**: `entities/beings/BeingResourceManager.cs` - Loads animation definitions
- **GenericBeing**: `entities/beings/GenericBeing.cs` - Configures AnimatedSprite2D from animations
