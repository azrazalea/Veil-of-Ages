using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Skills;
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

        // Load scene to get child nodes (Sprite2D, AudioStreamPlayer2D)
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

        // Configure sprites if specified
        if (!string.IsNullOrEmpty(definition.SpriteId))
        {
            var spriteDef = BeingResourceManager.Instance.GetSprite(definition.SpriteId);
            if (spriteDef != null)
            {
                ConfigureSprites(spriteDef, definition);
            }
            else
            {
                Log.Warn($"GenericBeing: Sprite '{definition.SpriteId}' not found for definition '{DefinitionId}'");
            }
        }

        // Ensure NeedsSystem exists (may not if _Ready runs before Initialize, e.g., for Player)
        NeedsSystem ??= new BeingNeedsSystem(this);

        // Initialize needs from definition BEFORE creating traits
        // This ensures needs exist when traits initialize
        InitializeNeedsFromDefinition(definition);

        // Initialize skills from definition BEFORE creating traits
        SkillSystem ??= new BeingSkillSystem(this);
        InitializeSkillsFromDefinition(definition);

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

        var needsCount = definition.Needs?.Count ?? 0;
        Log.Print($"GenericBeing '{DefinitionId}' initialized with {needsCount} needs and {definition.Traits.Count} traits");
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

        // Set name from definition after base.Initialize()
        // Subclasses can override this by setting Name after calling base.Initialize()
        if (!string.IsNullOrEmpty(definition.Name))
        {
            Name = $"{definition.Name}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }

    /// <summary>
    /// Configures sprite layers from a SpriteDefinition.
    /// Supports both single-layer and multi-layer sprites.
    /// Layer slot ZIndex values come from the entity definition's SpriteLayers.
    /// </summary>
    /// <param name="spriteDef">The sprite definition to use.</param>
    /// <param name="beingDef">The being definition for layer slot ZIndex lookup.</param>
    private void ConfigureSprites(SpriteDefinition spriteDef, BeingDefinition beingDef)
    {
        // Build SpriteLayerSlots from definition for ZIndex lookup and runtime API
        if (beingDef.SpriteLayers != null)
        {
            foreach (var slot in beingDef.SpriteLayers)
            {
                SpriteLayerSlots[slot.Name] = slot.ZIndex;
            }
        }

        // Create atlas textures for all layers
        var layerTextures = spriteDef.CreateAtlasTextures();

        // Get the existing Sprite2D child node (from the scene)
        var existingSprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        bool firstLayer = true;
        foreach (var (layerName, atlasTexture) in layerTextures)
        {
            Sprite2D sprite;

            if (firstLayer && existingSprite != null)
            {
                sprite = existingSprite;
                firstLayer = false;
            }
            else
            {
                sprite = new Sprite2D
                {
                    Name = $"SpriteLayer_{layerName}"
                };
                AddChild(sprite);
                firstLayer = false;
            }

            sprite.Texture = atlasTexture;

            // Set ZIndex from definition slot if available
            if (SpriteLayerSlots.TryGetValue(layerName, out int zIndex))
            {
                sprite.ZIndex = zIndex;
                sprite.ZAsRelative = true;
            }

            SpriteLayers[layerName] = sprite;
        }

        if (SpriteLayers.Count == 0 && existingSprite != null)
        {
            Log.Warn($"GenericBeing '{DefinitionId}': No sprite layers created, falling back to existing Sprite2D");
            SpriteLayers["body"] = existingSprite;
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

    /// <summary>
    /// Initialize needs from the definition's Needs array.
    /// Called BEFORE traits are created to ensure needs exist when traits initialize.
    /// </summary>
    /// <param name="definition">The being definition containing needs.</param>
    private void InitializeNeedsFromDefinition(BeingDefinition definition)
    {
        if (NeedsSystem == null || definition.Needs == null || definition.Needs.Count == 0)
        {
            return;
        }

        foreach (var needDef in definition.Needs)
        {
            if (string.IsNullOrEmpty(needDef.Id) || string.IsNullOrEmpty(needDef.Name))
            {
                Log.Warn($"GenericBeing: Skipping need with missing Id or Name in definition '{DefinitionId}'");
                continue;
            }

            var need = new Need(
                needDef.Id,
                needDef.Name,
                needDef.Initial,
                needDef.DecayRate,
                needDef.Critical,
                needDef.Low,
                needDef.High);
            NeedsSystem.AddNeed(need);
        }

        if (definition.Needs.Count > 0)
        {
            var needNames = string.Join(", ", definition.Needs.Select(n => n.Name));
            Log.Print($"{Name}: Initialized needs from definition: {needNames}");
        }
    }

    /// <summary>
    /// Initialize skills from the definition's Skills array.
    /// Called BEFORE traits are created to ensure skills exist when traits initialize.
    /// </summary>
    /// <param name="definition">The being definition containing skills.</param>
    private void InitializeSkillsFromDefinition(BeingDefinition definition)
    {
        if (SkillSystem == null || definition.Skills == null || definition.Skills.Count == 0)
        {
            return;
        }

        foreach (var skillStart in definition.Skills)
        {
            if (string.IsNullOrEmpty(skillStart.Id))
            {
                Log.Warn($"GenericBeing: Skipping skill with missing Id in definition '{DefinitionId}'");
                continue;
            }

            var skillDef = SkillResourceManager.Instance.GetDefinition(skillStart.Id);
            if (skillDef == null)
            {
                Log.Warn($"GenericBeing: Skill definition '{skillStart.Id}' not found for definition '{DefinitionId}'");
                continue;
            }

            var skill = new Skill(skillDef, skillStart.Level, skillStart.Xp);
            SkillSystem.AddSkill(skill);
        }

        if (definition.Skills.Count > 0)
        {
            var skillNames = string.Join(", ", definition.Skills.Select(s => s.Id));
            Log.Print($"{Name}: Initialized skills from definition: {skillNames}");
        }
    }

    /// <summary>
    /// Override body structure initialization to use the definition's BaseStructure field.
    /// </summary>
    protected override void InitializeBodyStructure()
    {
        if (string.IsNullOrEmpty(DefinitionId))
        {
            // Fall back to default humanoid
            base.InitializeBodyStructure();
            return;
        }

        var definition = BeingResourceManager.Instance.GetDefinition(DefinitionId);
        var baseStructure = definition?.Body?.BaseStructure;

        if (string.IsNullOrEmpty(baseStructure))
        {
            // Default to humanoid if no BaseStructure specified
            base.InitializeBodyStructure();
            return;
        }

        // Load the specified body structure from the resource manager
        if (!BodyStructureResourceManager.Instance.HasDefinition(baseStructure))
        {
            Log.Warn($"GenericBeing '{DefinitionId}': Body structure '{baseStructure}' not found, falling back to humanoid");
            base.InitializeBodyStructure();
            return;
        }

        var bodyStructureDef = BodyStructureResourceManager.Instance.GetDefinition(baseStructure);
        Health?.InitializeFromDefinition(bodyStructureDef);
    }
}
