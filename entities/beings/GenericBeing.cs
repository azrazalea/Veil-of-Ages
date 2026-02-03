using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// A data-driven entity class that loads its configuration from BeingDefinition JSON files.
/// This generic Being can replicate the behavior of HumanTownsfolk, MindlessSkeleton,
/// MindlessZombie, and any other being type purely from JSON definitions.
/// </summary>
public partial class GenericBeing : Being
{
    /// <summary>
    /// Gets or sets the ID of the definition this entity was created from.
    /// </summary>
    public string? DefinitionId { get; protected set; }

    /// <summary>
    /// Gets runtime parameters passed during creation.
    /// These are merged with JSON parameters when configuring traits.
    /// </summary>
    public Dictionary<string, object?> RuntimeParameters { get; private set; } = new ();

    /// <summary>
    /// Cached definition for attribute access.
    /// </summary>
    private BeingDefinition? _cachedDefinition;

    /// <summary>
    /// Gets default attributes loaded from the definition.
    /// Falls back to base attributes if no definition is found.
    /// </summary>
    public override BeingAttributes DefaultAttributes
    {
        get
        {
            _cachedDefinition ??= DefinitionId != null
                ? BeingResourceManager.Instance.GetDefinition(DefinitionId)
                : null;

            return _cachedDefinition?.Attributes?.ToBeingAttributes() ?? BaseAttributesSet;
        }
    }

    /// <summary>
    /// Audio players keyed by sound name.
    /// </summary>
    private readonly Dictionary<string, AudioStreamPlayer2D> _audioPlayers = new ();

    /// <summary>
    /// Creates a GenericBeing from a definition ID.
    /// </summary>
    /// <param name="definitionId">The ID of the BeingDefinition to use.</param>
    /// <param name="runtimeParams">Optional runtime parameters to merge with JSON parameters.</param>
    /// <returns>A new GenericBeing instance, or null if the definition was not found.</returns>
    public static GenericBeing? CreateFromDefinition(string definitionId, Dictionary<string, object?>? runtimeParams = null)
    {
        var definition = BeingResourceManager.Instance.GetDefinition(definitionId);
        if (definition == null)
        {
            Log.Error($"GenericBeing: Definition '{definitionId}' not found");
            return null;
        }

        if (definition.Abstract == true)
        {
            Log.Error($"Cannot create entity from abstract definition '{definitionId}'");
            return null;
        }

        // Load scene to get child nodes (AnimatedSprite2D, AudioStreamPlayer2D)
        var scene = GD.Load<PackedScene>("res://entities/beings/generic_being.tscn");
        var being = scene.Instantiate<GenericBeing>();
        being.DefinitionId = definitionId;
        being.RuntimeParameters = runtimeParams ?? new Dictionary<string, object?>();

        return being;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericBeing"/> class.
    /// Protected constructor for subclasses. Use CreateFromDefinition factory method for direct instantiation.
    /// </summary>
    protected GenericBeing()
    {
    }

    public override void _Ready()
    {
        if (string.IsNullOrEmpty(DefinitionId))
        {
            Log.Error("GenericBeing: DefinitionId is not set");
            base._Ready();
            return;
        }

        var definition = BeingResourceManager.Instance.GetDefinition(DefinitionId);
        if (definition == null)
        {
            Log.Error($"GenericBeing: Definition '{DefinitionId}' not found in _Ready");
            base._Ready();
            return;
        }

        // Configure animations if specified
        if (!string.IsNullOrEmpty(definition.AnimationId))
        {
            var animation = BeingResourceManager.Instance.GetAnimation(definition.AnimationId);
            if (animation != null)
            {
                ConfigureAnimations(animation);
            }
            else
            {
                Log.Warn($"GenericBeing: Animation '{definition.AnimationId}' not found for definition '{DefinitionId}'");
            }
        }

        // Create and add traits from definition
        foreach (var traitDef in definition.Traits)
        {
            // Create runtime config from stored runtime parameters
            TraitConfiguration? runtimeConfig = null;
            if (RuntimeParameters.Count > 0)
            {
                runtimeConfig = new TraitConfiguration(RuntimeParameters);
            }

            var trait = TraitFactory.CreateTrait(traitDef, runtimeConfig);
            if (trait != null)
            {
                SelfAsEntity().AddTrait(trait, traitDef.Priority);
            }
            else
            {
                Log.Warn($"GenericBeing: Failed to create trait '{traitDef.TraitType}' for definition '{DefinitionId}'");
            }
        }

        // Apply body modifications if specified
        if (definition.Body?.Modifications != null && definition.Body.Modifications.Count > 0)
        {
            ApplyBodyModifications(definition.Body.Modifications);
        }

        // Set up audio if configured
        if (definition.Audio != null)
        {
            SetupAudio(definition.Audio);
        }

        base._Ready();

        Log.Print($"GenericBeing '{DefinitionId}' initialized with {definition.Traits.Count} traits");
    }

    public override void Initialize(Area gridArea, Vector2I startGridPos, GameController? gameController = null, BeingAttributes? attributes = null, bool debugEnabled = false)
    {
        if (string.IsNullOrEmpty(DefinitionId))
        {
            base.Initialize(gridArea, startGridPos, gameController, attributes, debugEnabled);
            return;
        }

        var definition = BeingResourceManager.Instance.GetDefinition(DefinitionId);
        if (definition == null)
        {
            base.Initialize(gridArea, startGridPos, gameController, attributes, debugEnabled);
            return;
        }

        // Set movement speed from definition
        if (definition.Movement != null)
        {
            BaseMovementPointsPerTick = definition.Movement.BaseMovementPointsPerTick;
        }

        // Use definition attributes if none provided
        var effectiveAttributes = attributes ?? definition.Attributes?.ToBeingAttributes();

        base.Initialize(gridArea, startGridPos, gameController, effectiveAttributes, debugEnabled);
    }

    /// <summary>
    /// Configures the AnimatedSprite2D with frames from a SpriteAnimationDefinition.
    /// </summary>
    /// <param name="animation">The animation definition to use.</param>
    private void ConfigureAnimations(SpriteAnimationDefinition animation)
    {
        var animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animatedSprite == null)
        {
            Log.Warn($"GenericBeing: AnimatedSprite2D node not found for definition '{DefinitionId}'");
            return;
        }

        var spriteFrames = animation.CreateSpriteFrames();
        animatedSprite.SpriteFrames = spriteFrames;

        // Play idle animation if available
        if (spriteFrames.HasAnimation("idle"))
        {
            animatedSprite.Play("idle");
        }
    }

    /// <summary>
    /// Applies body modifications from the definition using the registry.
    /// </summary>
    /// <param name="modifications">List of body modifications to apply.</param>
    private void ApplyBodyModifications(List<BodyModification> modifications)
    {
        if (Health == null)
        {
            return;
        }

        foreach (var modification in modifications)
        {
            if (!BodyModificationRegistry.Apply(this, modification))
            {
                Log.Warn($"GenericBeing '{DefinitionId}': Failed to apply body modification '{modification.Type}'");
            }
        }
    }

    /// <summary>
    /// Sets up audio players based on the audio definition.
    /// </summary>
    /// <param name="audio">The audio definition to use.</param>
    private void SetupAudio(AudioDefinition audio)
    {
        if (audio.Sounds == null || audio.Sounds.Count == 0)
        {
            return;
        }

        foreach (var (soundName, resourcePath) in audio.Sounds)
        {
            var stream = ResourceLoader.Load<AudioStream>(resourcePath);
            if (stream == null)
            {
                Log.Warn($"GenericBeing: Failed to load audio stream '{resourcePath}' for sound '{soundName}'");
                continue;
            }

            // Try to find existing audio player or create a new one
            var player = GetNodeOrNull<AudioStreamPlayer2D>($"Audio_{soundName}");
            if (player == null)
            {
                player = new AudioStreamPlayer2D
                {
                    Name = $"Audio_{soundName}"
                };
                AddChild(player);
            }

            player.Stream = stream;
            _audioPlayers[soundName] = player;
        }
    }

    /// <summary>
    /// Plays a named sound from the audio configuration.
    /// </summary>
    /// <param name="soundName">The name of the sound to play.</param>
    public void PlaySound(string soundName)
    {
        if (_audioPlayers.TryGetValue(soundName, out var player))
        {
            player.Position = Grid.Utils.GridToWorld(GetCurrentGridPosition());
            player.Play();
        }
    }
}
