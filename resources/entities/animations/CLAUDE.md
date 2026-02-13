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
Animations for the player necromancer.
- Sprite Size: 32x32
- Animations: idle (8 frames), walk (4 frames)
- Sprites now use Kenney atlas as single-frame placeholders

## Animation Data Format

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

### Field Descriptions

- **TexturePath**: Godot resource path to the sprite sheet
- **FrameWidth/FrameHeight**: Size of each frame in pixels
- **FrameCount**: Number of frames in the animation
- **FrameRow**: Which row of the sprite sheet (0-indexed)
- **StartColumn**: Starting column offset (0-indexed)
- **Speed**: Animation playback speed (frames per second)
- **Loop**: Whether animation repeats

## Runtime Usage

Animation definitions are loaded by `SpriteAnimationDefinition.CreateSpriteFrames()` which:
1. Loads the texture from TexturePath
2. Creates AtlasTexture for each frame
3. Builds a Godot SpriteFrames resource
4. Returns the SpriteFrames for use with AnimatedSprite2D

## Creating New Animations

1. Add your sprite sheet to the assets directory
2. Create a new JSON file with unique Id
3. Define each animation state (idle, walk, etc.)
4. Reference the animation Id in your entity definition's AnimationId field

## Standard Animation Names

These animation names are expected by the entity system:
- **idle**: Default standing animation (played automatically on spawn)
- **walk**: Movement animation (triggered during movement)

## Dependencies

- **SpriteAnimationDefinition**: `entities/beings/SpriteAnimationDefinition.cs` - Parses and creates SpriteFrames
- **BeingResourceManager**: `entities/beings/BeingResourceManager.cs` - Loads animation definitions
- **GenericBeing**: `entities/beings/GenericBeing.cs` - Configures AnimatedSprite2D from animations
