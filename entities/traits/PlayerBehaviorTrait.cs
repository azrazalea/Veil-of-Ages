using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Provides autonomous behavior for the player when their command queue is empty.
/// When the player has no commands queued or assigned, this trait handles basic
/// survival behaviors like sleeping at night and eating when hungry.
/// </summary>
public class PlayerBehaviorTrait : BeingTrait
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerBehaviorTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// Home is managed by HomeTrait.
    /// </summary>
    public PlayerBehaviorTrait()
    {
    }

    /// <summary>
    /// Gets the home building from HomeTrait.
    /// </summary>
    private Building? GetHome() => _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Home is managed by HomeTrait, not directly on PlayerBehaviorTrait.
    /// </summary>
    /// <remarks>
    /// If no home is provided via HomeTrait, the player will function but cannot return home to sleep
    /// or access home storage for food.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // Home is managed by HomeTrait, not PlayerBehaviorTrait
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        // Home is managed by HomeTrait, not PlayerBehaviorTrait
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        if (owner == null || owner.Health == null)
        {
            return;
        }

        // NOTE: LivingTrait, InventoryTrait, HomeTrait, and ItemConsumptionBehaviorTrait
        // are now defined in JSON (player.json) rather than programmatically added here.
        // This follows the ECS architecture where trait composition is data-driven.
        var home = GetHome();
        if (home != null)
        {
            Log.Print($"{_owner?.Name}: Player behavior trait initialized with home {home.BuildingName}");
        }
        else
        {
            Log.Warn($"{_owner?.Name}: Player behavior trait initialized WITHOUT a home");
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Check if the entity is currently inside their home building.
    /// </summary>
    private bool IsAtHome()
    {
        var home = GetHome();
        if (_owner == null || home == null)
        {
            return false;
        }

        Vector2I entityPos = _owner.GetCurrentGridPosition();
        Vector2I homePos = home.GetCurrentGridPosition();
        var interiorPositions = home.GetInteriorPositions();

        foreach (var relativePos in interiorPositions)
        {
            if (entityPos == homePos + relativePos)
            {
                return true;
            }
        }

        return false;
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        // Get the player reference
        if (_owner is not Player player)
        {
            return null;
        }

        // If the player has commands queued or is executing a command, defer to the command system
        if (player.HasAssignedCommand() || player.GetCommandQueue().Count > 0)
        {
            return null;
        }

        // Only process autonomous behavior if movement is complete
        if (_owner.IsMoving())
        {
            return null;
        }

        var currentActivity = _owner.GetCurrentActivity();
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);

        // Check if it's nighttime
        bool isNight = gameTime.CurrentDayPhase is DayPhaseType.Night;

        // Get energy need to check if tired
        var energyNeed = _owner.NeedsSystem?.GetNeed("energy");
        bool isLowEnergy = energyNeed != null && energyNeed.IsLow();

        DebugLog("BEHAVIOR", $"Phase: {gameTime.CurrentDayPhase}, IsNight: {isNight}, LowEnergy: {isLowEnergy}, Activity: {currentActivity?.GetType().Name ?? "none"}");

        // If already sleeping, let the activity handle it
        if (currentActivity is SleepActivity)
        {
            DebugLog("BEHAVIOR", "Already sleeping, returning null");
            return null;
        }

        // If nighttime and energy is low, go home and sleep
        var home = GetHome();
        if (isNight && isLowEnergy && home != null)
        {
            DebugLog("BEHAVIOR", "Night time and low energy, going home to sleep");

            // Check if already navigating home
            if (currentActivity is GoToBuildingActivity)
            {
                return null;
            }

            // If at home, start sleeping
            if (IsAtHome())
            {
                DebugLog("BEHAVIOR", "At home, starting SleepActivity", 0);
                var sleepActivity = new SleepActivity(priority: 1);
                return new StartActivityAction(_owner, this, sleepActivity, priority: 1);
            }

            // Not at home, navigate there
            DebugLog("BEHAVIOR", "Not at home, navigating home", 0);
            var goHomeActivity = new GoToBuildingActivity(home, priority: 1);
            return new StartActivityAction(_owner, this, goHomeActivity, priority: 1);
        }

        // Otherwise, just idle with very low priority (priority 2)
        // This allows any other behavior to easily override
        return new IdleAction(_owner, this, priority: 2);
    }
}
