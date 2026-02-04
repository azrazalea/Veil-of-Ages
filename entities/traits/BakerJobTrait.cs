using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Job trait for bakers. Bakers work at their assigned workplace during daytime (Dawn/Day).
/// They bake bread by consuming wheat from the workplace storage.
/// At night, BakerJobTrait returns null and VillagerTrait handles sleep behavior.
/// </summary>
public class BakerJobTrait : BeingTrait, IDesiredResources
{
    private Building? _workplace;

    // Desired resource stockpile for baker's home
    // Bakers want wheat for baking and bread as finished product
    private static readonly Dictionary<string, int> _desiredResources = new ()
    {
        { "wheat", 10 },  // Need wheat for baking bread
        { "bread", 5 } // Keep some bread in stock
    };

    /// <summary>
    /// Gets the desired resource levels for the baker's home storage.
    /// Bakers want to stockpile wheat and bread.
    /// </summary>
    public IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;

    // Work duration in ticks (~50 seconds at 8 ticks/sec)
    private const uint WORKDURATION = 400;

    /// <summary>
    /// Initializes a new instance of the <see cref="BakerJobTrait"/> class.
    /// Parameterless constructor for data-driven entity system.
    /// </summary>
    public BakerJobTrait()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BakerJobTrait"/> class.
    /// Constructor for direct instantiation with a workplace.
    /// </summary>
    public BakerJobTrait(Building workplace)
    {
        _workplace = workplace;
    }

    /// <summary>
    /// Validates that the trait has all required configuration.
    /// Expected parameters:
    /// - "workplace" (Building): The bakery building to work at (recommended but optional).
    /// </summary>
    /// <remarks>
    /// If no workplace is provided, the trait will be non-functional but won't crash.
    /// The baker will simply not suggest any work actions until a workplace is assigned.
    /// </remarks>
    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        // If workplace is already set (direct instantiation), configuration is valid
        if (_workplace != null)
        {
            return true;
        }

        // workplace is recommended but we handle null gracefully in SuggestAction()
        if (config.GetBuilding("workplace") == null)
        {
            Log.Warn("BakerJobTrait: 'workplace' parameter recommended for proper function");
        }

        return true; // Don't fail - we handle missing workplace gracefully
    }

    /// <inheritdoc/>
    public override void Configure(TraitConfiguration config)
    {
        // If workplace is already set (direct instantiation), skip configuration
        if (_workplace != null)
        {
            DebugLog("BAKER", $"Configure: workplace already set = {_workplace.BuildingName}");
            return;
        }

        // Get the workplace building from configuration
        _workplace = config.GetBuilding("workplace");
        DebugLog("BAKER", $"Configure: workplace = {_workplace?.BuildingName ?? "null"}");
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        DebugLog("BAKER", $"SuggestAction: owner={_owner != null}, workplace={_workplace?.BuildingName ?? "null"}, valid={_workplace != null && GodotObject.IsInstanceValid(_workplace)}", 0);

        if (_owner == null || _workplace == null || !GodotObject.IsInstanceValid(_workplace))
        {
            DebugLog("BAKER", $"Early return: owner={_owner != null}, workplace={_workplace != null}, workplaceValid={_workplace != null && GodotObject.IsInstanceValid(_workplace)}", 0);
            return null;
        }

        // Don't interrupt movement
        if (_owner.IsMoving())
        {
            DebugLog("BAKER", "Returning null: IsMoving() = true", 0);
            return null;
        }

        // If already baking, let the activity handle things
        if (_owner.GetCurrentActivity() is BakingActivity)
        {
            DebugLog("BAKER", "Returning null: already in BakingActivity", 0);
            return null;
        }

        // Only work during work hours (Dawn/Day)
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            DebugLog("BAKER", $"Returning null: not work hours, phase={gameTime.CurrentDayPhase}", 0);
            return null; // Let VillagerTrait handle night behavior
        }

        DebugLog("BAKER", $"Work hours, starting BakingActivity at {_workplace.BuildingName}", 0);

        // Start the baking activity - it handles all navigation and storage access
        var bakingActivity = new BakingActivity(_workplace, WORKDURATION, priority: 0);
        return new StartActivityAction(_owner, this, bakingActivity, priority: 0);
    }

    public override string InitialDialogue(Being speaker)
    {
        var gameTime = _owner?.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Day)
        {
            return $"Morning, {speaker.Name}! The oven waits for no one.";
        }

        return $"Evening, {speaker.Name}. Bread's baked for tomorrow.";
    }

    public override string? GenerateDialogueDescription()
    {
        return "I am a baker.";
    }
}
