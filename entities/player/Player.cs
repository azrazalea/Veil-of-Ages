using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Autonomy;
using VeilOfAges.Entities.Beings;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;
using VeilOfAges.UI;

namespace VeilOfAges.Entities;

/// <summary>
/// The player-controlled necromancer entity.
/// Extends GenericBeing to load configuration from player.json definition.
/// Adds command queue functionality for player-specific input handling.
/// Features autonomous behavior via ScheduleTrait (sleep) and job traits when no commands are queued.
/// Autonomy configuration tracks which traits the player has chosen and applies them.
/// </summary>
public partial class Player : GenericBeing
{
    public const uint MAXCOMMANDNUM = 7;

    private readonly ReorderableQueue<EntityCommand> _commandQueue = new ();

    /// <summary>
    /// Gets the autonomy configuration that manages the player's trait loadout.
    /// Rules map to traits that are added/removed/configured on the player.
    /// </summary>
    public AutonomyConfig AutonomyConfig { get; private set; } = new ();

    /// <summary>
    /// Gets the player's home building from HomeTrait.
    /// </summary>
    public Building? Home => SelfAsEntity().GetTrait<HomeTrait>()?.Home;

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// Constructor sets the definition ID so GenericBeing loads from player.json.
    /// </summary>
    public Player()
    {
        DefinitionId = "player";
    }

    /// <summary>
    /// Sets the player's home building via HomeTrait and notifies ScholarJobTrait.
    /// Called by World after player initialization when assigning the player's house.
    /// </summary>
    /// <param name="home">The building to set as home.</param>
    public void SetHome(Building home)
    {
        // Set home via HomeTrait (single source of truth)
        var homeTrait = SelfAsEntity().GetTrait<HomeTrait>();
        homeTrait?.SetHome(home);

        // Notify ScholarJobTrait (needs to know workplace location)
        var scholarJobTrait = SelfAsEntity().GetTrait<ScholarJobTrait>();
        scholarJobTrait?.SetHome(home);

        // Apply autonomy config now that we have a home
        // (traits added by autonomy may need the home reference)
        AutonomyConfig.Apply(this);
    }

    public override void Initialize(Area gridArea, Vector2I startGridPos, GameController? gameController = null, BeingAttributes? attributes = null, bool debugEnabled = false)
    {
        // Player always has debug enabled for detailed logging
        base.Initialize(gridArea, startGridPos, gameController, attributes, debugEnabled: true);
        Name = "Lilith Galonadel";
        Services.Register(this);

        InitializeAutonomy();
    }

    /// <summary>
    /// Load autonomy rules from JSON definitions via BeingResourceManager.
    /// Falls back to empty config if no JSON config is found.
    /// </summary>
    private void InitializeAutonomy()
    {
        var configDef = Beings.BeingResourceManager.Instance.GetAutonomyConfig("player");
        if (configDef == null)
        {
            Log.Warn("Player: No autonomy config found for 'player', using empty config");
            return;
        }

        var allRules = Beings.BeingResourceManager.Instance.GetAutonomyRules();
        AutonomyConfig = AutonomyConfig.FromDefinitions(configDef, allRules);
    }

    private volatile bool _pendingAutonomyReapply;

    /// <summary>
    /// Request that the autonomy configuration be reapplied next tick.
    /// Removes all autonomy-managed traits, cancels current activity,
    /// and re-applies all currently enabled rules from scratch.
    /// Call this after changing enabled rules or adding/removing rules.
    /// </summary>
    public void ReapplyAutonomy()
    {
        _pendingAutonomyReapply = true;
    }

    public override EntityAction Think(Vector2 currentPosition, ObservationData observationData)
    {
        if (_pendingAutonomyReapply)
        {
            _pendingAutonomyReapply = false;
            return new ReapplyAutonomyAction(this, this);
        }

        if (_commandQueue.Count > 0 && _currentCommand == null)
        {
            _currentCommand = _commandQueue.Dequeue();

            // Note: This runs on a background thread, but C# events are thread-safe to invoke
            GameEvents.FireCommandQueueChanged();
        }

        return base.Think(currentPosition, observationData);
    }

    public bool QueueCommand(EntityCommand command)
    {
        if (_commandQueue.Count >= MAXCOMMANDNUM)
        {
            return false;
        }

        _commandQueue.Enqueue(command);
        GameEvents.FireCommandQueueChanged();

        return true;
    }

    public ReorderableQueue<EntityCommand> GetCommandQueue()
    {
        return _commandQueue;
    }

    public bool HasAssignedCommand()
    {
        return _currentCommand != null;
    }

    public EntityCommand? GetAssignedCommand()
    {
        return _currentCommand;
    }
}
