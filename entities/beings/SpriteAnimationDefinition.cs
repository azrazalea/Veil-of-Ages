using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using VeilOfAges.Core.Lib;

using static VeilOfAges.Core.Lib.JsonOptions;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// JSON-serializable class representing animation definitions for entities.
/// Used to create Godot SpriteFrames resources at runtime.
/// </summary>
public class SpriteAnimationDefinition : IResourceDefinition
{
    /// <summary>
    /// Gets or sets unique identifier for this animation definition.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets human-readable name for this animation definition.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets default sprite size as [width, height] (e.g., [32, 32]).
    /// </summary>
#pragma warning disable SA1018 // Nullable type symbol should not be preceded by a space
    public int[] ? SpriteSize { get; set; }
#pragma warning restore SA1018

    /// <summary>
    /// Gets or sets dictionary mapping animation names to their data.
    /// </summary>
    public Dictionary<string, AnimationData>? Animations { get; set; }

    /// <summary>
    /// Load a SpriteAnimationDefinition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file (res:// or absolute path).</param>
    /// <returns>The loaded definition, or null if loading failed.</returns>
    /// <summary>
    /// Load a SpriteAnimationDefinition from a JSON file.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>The loaded definition, or null if loading failed.</returns>
    public static SpriteAnimationDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpriteAnimationDefinition>(jsonContent, Default);
        }
        catch (System.Exception e)
        {
            Log.Error($"Error loading animation definition: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate that all required fields are present.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Log.Error("SpriteAnimationDefinition: Missing required field 'id'");
            return false;
        }

        if (SpriteSize == null || SpriteSize.Length != 2)
        {
            Log.Error($"SpriteAnimationDefinition '{Id}': Invalid or missing 'sprite_size' (expected [width, height])");
            return false;
        }

        if (Animations == null || Animations.Count == 0)
        {
            Log.Error($"SpriteAnimationDefinition '{Id}': Missing or empty 'animations' dictionary");
            return false;
        }

        foreach (var (animName, animData) in Animations)
        {
            if (!animData.Validate(Id, animName))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Create a Godot SpriteFrames resource from this definition.
    /// </summary>
    /// <returns>A new SpriteFrames resource with all animations configured.</returns>
    public SpriteFrames CreateSpriteFrames()
    {
        var spriteFrames = new SpriteFrames();

        // Remove the default animation that SpriteFrames creates automatically
        if (spriteFrames.HasAnimation("default"))
        {
            spriteFrames.RemoveAnimation("default");
        }

        if (Animations == null)
        {
            Log.Error($"SpriteAnimationDefinition '{Id}': Cannot create SpriteFrames - no animations defined");
            return spriteFrames;
        }

        foreach (var (animName, animData) in Animations)
        {
            if (string.IsNullOrEmpty(animData.TexturePath))
            {
                Log.Error($"SpriteAnimationDefinition '{Id}': Animation '{animName}' has no texture path");
                continue;
            }

            // Load the texture
            var texture = ResourceLoader.Load<Texture2D>(animData.TexturePath);
            if (texture == null)
            {
                Log.Error($"SpriteAnimationDefinition '{Id}': Failed to load texture for animation '{animName}': {animData.TexturePath}");
                continue;
            }

            // Add the animation
            spriteFrames.AddAnimation(animName);
            spriteFrames.SetAnimationSpeed(animName, animData.Speed);
            spriteFrames.SetAnimationLoop(animName, animData.Loop);

            // Create and add frames
            for (int i = 0; i < animData.FrameCount; i++)
            {
                var atlasTexture = new AtlasTexture
                {
                    Atlas = texture
                };

                // Calculate the region for this frame
                int column = animData.StartColumn + i;
                float x = column * (animData.FrameWidth + animData.Separation);
                float y = animData.FrameRow * (animData.FrameHeight + animData.Separation);

                atlasTexture.Region = new Rect2(x, y, animData.FrameWidth, animData.FrameHeight);

                spriteFrames.AddFrame(animName, atlasTexture);
            }

            Log.Print($"SpriteAnimationDefinition '{Id}': Added animation '{animName}' with {animData.FrameCount} frames");
        }

        return spriteFrames;
    }

    /// <summary>
    /// Data for a single animation within a sprite animation definition.
    /// </summary>
    public class AnimationData
    {
        /// <summary>
        /// Gets or sets path to the texture file (res://assets/...).
        /// </summary>
        public string? TexturePath { get; set; }

        /// <summary>
        /// Gets or sets width of each frame in pixels.
        /// </summary>
        public int FrameWidth { get; set; }

        /// <summary>
        /// Gets or sets height of each frame in pixels.
        /// </summary>
        public int FrameHeight { get; set; }

        /// <summary>
        /// Gets or sets number of frames in the animation.
        /// </summary>
        public int FrameCount { get; set; }

        /// <summary>
        /// Gets or sets which row in the spritesheet (0-indexed).
        /// </summary>
        public int FrameRow { get; set; }

        /// <summary>
        /// Gets or sets starting column in the spritesheet (0-indexed).
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// Gets or sets animation speed in frames per second.
        /// </summary>
        public float Speed { get; set; } = 5.0f;

        /// <summary>
        /// Gets or sets a value indicating whether whether the animation should loop.
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// Gets or sets the pixel gap between frames in the spritesheet. Default 0.
        /// </summary>
        public int Separation { get; set; }

        /// <summary>
        /// Validate that all required fields are present and valid.
        /// </summary>
        /// <param name="definitionId">Parent definition ID for error messages.</param>
        /// <param name="animationName">Animation name for error messages.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(string? definitionId, string animationName)
        {
            if (string.IsNullOrEmpty(TexturePath))
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' missing 'texture_path'");
                return false;
            }

            if (FrameWidth <= 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'frame_width': {FrameWidth}");
                return false;
            }

            if (FrameHeight <= 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'frame_height': {FrameHeight}");
                return false;
            }

            if (FrameCount <= 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'frame_count': {FrameCount}");
                return false;
            }

            if (FrameRow < 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'frame_row': {FrameRow}");
                return false;
            }

            if (StartColumn < 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'start_column': {StartColumn}");
                return false;
            }

            if (Speed <= 0)
            {
                Log.Error($"SpriteAnimationDefinition '{definitionId}': Animation '{animationName}' has invalid 'speed': {Speed}");
                return false;
            }

            return true;
        }
    }
}
