using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for processing a reaction (crafting).
/// Goes to workplace (if specified), navigates to storage to take inputs,
/// navigates to facility to execute reaction, then navigates back to storage to deposit outputs.
/// Consumes energy while processing based on reaction's EnergyCostMultiplier.
///
/// Uses a Stateless state machine to manage phase transitions and interruption/resumption.
///
/// States:
/// GoingToBuilding -> GoingToStorageForInputs -> TakingInputs -> GoingToFacility ->
/// ExecutingReaction -> Processing -> GoingToStorageForOutputs -> DepositingOutputs -> Complete
///
/// Interruption behavior (two-zone regression):
/// - Pre-reaction zone (GoingToBuilding through Processing): Interrupted -> GoingToBuilding
/// - Post-reaction zone (GoingToStorageForOutputs, DepositingOutputs): Interrupted -> GoingToStorageForOutputs
/// - Navigation states (GoingToBuilding, GoingToStorageForInputs, GoingToFacility,
///   GoingToStorageForOutputs): PermitReentry for Interrupted and Resumed.
/// </summary>
public class ProcessReactionActivity : StatefulActivity<ProcessReactionActivity.ReactionState, ProcessReactionActivity.ReactionTrigger>
{
    /// <summary>
    /// States representing the phases of processing a reaction.
    /// </summary>
    public enum ReactionState
    {
        GoingToBuilding,
        GoingToStorageForInputs,
        TakingInputs,
        GoingToFacility,
        ExecutingReaction,
        Processing,
        GoingToStorageForOutputs,
        DepositingOutputs,
    }

    /// <summary>
    /// Triggers that cause state transitions.
    /// </summary>
    public enum ReactionTrigger
    {
        ArrivedAtBuilding,
        ArrivedAtStorageForInputs,
        InputsTaken,
        ArrivedAtFacility,
        ReactionExecuted,
        ProcessingComplete,
        ArrivedAtStorageForOutputs,
        OutputsDeposited,
        Interrupted,
        Resumed,
    }

    // Base energy cost per tick while processing (same as WorkFieldActivity)
    // Multiplied by reaction's EnergyCostMultiplier
    private const float BASEENERGYCOSTPERTICK = 0.01f;

    private readonly ReactionDefinition _reaction;
    private readonly Facility? _workplaceStorage;
    private readonly Room? _workplaceRoom;

    // Progress tracking (preserved across interruptions)
    private uint _processTimer;
    private uint _variedDuration;
    private Need? _energyNeed;

    protected override ReactionTrigger InterruptedTrigger => ReactionTrigger.Interrupted;

    protected override ReactionTrigger ResumedTrigger => ReactionTrigger.Resumed;

    public override string DisplayName
    {
        get
        {
            var reactionName = _reaction.Name;
            if (reactionName == null)
            {
                Log.Warn($"ProcessReactionActivity: Reaction '{_reaction.Id}' has no Name");
                reactionName = _reaction.Id ?? string.Empty;
            }

            return _machine.State switch
            {
                ReactionState.Processing => L.TrFmt("activity.PROCESSING", reactionName),
                ReactionState.GoingToStorageForOutputs => L.TrFmt("activity.STORING_OUTPUTS", reactionName),
                ReactionState.DepositingOutputs => L.TrFmt("activity.STORING_OUTPUTS", reactionName),
                _ => L.TrFmt("activity.PREPARING", reactionName)
            };
        }
    }

    public override Room? TargetRoom => _workplaceRoom;

    public override string? TargetFacilityId => _machine.State is ReactionState.GoingToFacility
        or ReactionState.ExecutingReaction
        or ReactionState.Processing
        && _reaction.RequiredFacilities.Count > 0
            ? _reaction.RequiredFacilities[0]
            : null;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        // When at facility states, delegate to the current sub-activity
        if (_machine.State is ReactionState.GoingToFacility
            or ReactionState.ExecutingReaction
            or ReactionState.Processing
            && _currentSubActivity != null)
        {
            return _currentSubActivity.GetAlternativeGoalPositions(entity);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessReactionActivity"/> class.
    /// Create an activity to process a reaction at a workplace.
    /// </summary>
    /// <param name="reaction">The reaction definition to process.</param>
    /// <param name="workplaceStorage">The storage facility for inputs/outputs (can be null if no storage required).</param>
    /// <param name="priority">Action priority.</param>
    public ProcessReactionActivity(ReactionDefinition reaction, Facility? workplaceStorage, int priority = 0)
        : base(ReactionState.GoingToBuilding)
    {
        _reaction = reaction;
        _workplaceStorage = workplaceStorage;
        _workplaceRoom = workplaceStorage?.ContainingRoom;
        Priority = priority;

        // Apply reaction's hunger multiplier (defaults to 1.0 if not specified)
        if (reaction.HungerMultiplier != 1.0f)
        {
            NeedDecayMultipliers["hunger"] = reaction.HungerMultiplier;
        }

        ConfigureStateMachine();
    }

    /// <summary>
    /// Configures the state machine transitions, including interruption/resumption behavior.
    ///
    /// Pre-reaction zone states (GoingToBuilding through Processing) regress to GoingToBuilding on interruption.
    /// Post-reaction zone states (GoingToStorageForOutputs, DepositingOutputs) regress to GoingToStorageForOutputs.
    /// Navigation states use PermitReentry for Interrupted and Resumed to force fresh pathfinder creation.
    /// Sub-activity references are automatically nulled by the base class OnTransitioned callback.
    /// </summary>
    private void ConfigureStateMachine()
    {
        // GoingToBuilding: navigation state for pre-reaction zone
        _machine.Configure(ReactionState.GoingToBuilding)
            .Permit(ReactionTrigger.ArrivedAtBuilding, ReactionState.GoingToStorageForInputs)
            .PermitReentry(ReactionTrigger.Interrupted) // Pre-reaction zone regression
            .PermitReentry(ReactionTrigger.Resumed);     // Re-enter to force fresh pathfinder

        // GoingToStorageForInputs: navigation state for pre-reaction zone
        _machine.Configure(ReactionState.GoingToStorageForInputs)
            .Permit(ReactionTrigger.ArrivedAtStorageForInputs, ReactionState.TakingInputs)
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToBuilding) // Pre-reaction zone regression
            .PermitReentry(ReactionTrigger.Resumed);     // Re-enter to force fresh pathfinder

        // TakingInputs: take items from storage
        _machine.Configure(ReactionState.TakingInputs)
            .Permit(ReactionTrigger.InputsTaken, ReactionState.GoingToFacility)
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToBuilding);  // Pre-reaction zone regression

        // GoingToFacility: navigation state for pre-reaction zone
        _machine.Configure(ReactionState.GoingToFacility)
            .Permit(ReactionTrigger.ArrivedAtFacility, ReactionState.ExecutingReaction)
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToBuilding) // Pre-reaction zone regression
            .PermitReentry(ReactionTrigger.Resumed);     // Re-enter to force fresh pathfinder

        // ExecutingReaction: fire the reaction action (single tick)
        _machine.Configure(ReactionState.ExecutingReaction)
            .Permit(ReactionTrigger.ReactionExecuted, ReactionState.Processing)
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToBuilding);  // Pre-reaction zone regression

        // Processing: wait for processing duration
        _machine.Configure(ReactionState.Processing)
            .Permit(ReactionTrigger.ProcessingComplete, ReactionState.GoingToStorageForOutputs)
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToBuilding);  // Pre-reaction zone regression

        // GoingToStorageForOutputs: navigation state for post-reaction zone
        _machine.Configure(ReactionState.GoingToStorageForOutputs)
            .Permit(ReactionTrigger.ArrivedAtStorageForOutputs, ReactionState.DepositingOutputs)
            .PermitReentry(ReactionTrigger.Interrupted) // Post-reaction zone regression
            .PermitReentry(ReactionTrigger.Resumed);     // Re-enter to force fresh pathfinder

        // DepositingOutputs: deposit items to storage
        _machine.Configure(ReactionState.DepositingOutputs)
            .Permit(ReactionTrigger.OutputsDeposited, ReactionState.GoingToStorageForOutputs) // Unused - Complete() called directly
            .Permit(ReactionTrigger.Interrupted, ReactionState.GoingToStorageForOutputs);  // Post-reaction zone regression
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get energy need for direct energy cost while processing
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");

        // Apply variance to reaction duration (+-15%)
        _variedDuration = ActivityTiming.GetVariedDuration(_reaction.Duration, 0.15f);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check workplace room still exists (if we have one)
        if (_workplaceRoom != null && _workplaceRoom.IsDestroyed)
        {
            Log.Warn($"{_owner.Name}: {_workplaceRoom.Name} destroyed while processing {_reaction.Name}");
            Fail();
            return null;
        }

        return _machine.State switch
        {
            ReactionState.GoingToBuilding => ProcessGoingToBuilding(position, perception),
            ReactionState.GoingToStorageForInputs => ProcessGoingToStorageForInputs(position, perception),
            ReactionState.TakingInputs => ProcessTakingInputs(position, perception),
            ReactionState.GoingToFacility => ProcessGoingToFacility(position, perception),
            ReactionState.ExecutingReaction => ProcessExecutingReaction(),
            ReactionState.Processing => ProcessProcessing(),
            ReactionState.GoingToStorageForOutputs => ProcessGoingToStorageForOutputs(position, perception),
            ReactionState.DepositingOutputs => ProcessDepositingOutputs(position, perception),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToBuilding(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // No workplace needed - skip directly to storage for inputs
        if (_workplaceRoom == null)
        {
            _machine.Fire(ReactionTrigger.ArrivedAtBuilding);
            return new IdleAction(_owner, this, Priority);
        }

        var room = _workplaceRoom; // Local capture for lambda (guaranteed non-null by check above)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("REACTION", $"Navigating to {room.Name} to process {_reaction.Name}", 0);
                return new GoToRoomActivity(room, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        // Arrived at room
        DebugLog("REACTION", $"Arrived at {room.Name} to process {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ArrivedAtBuilding);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessGoingToStorageForInputs(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Check if we have inputs to take
        var inputsToTake = _reaction.Inputs
            .Where(i => !string.IsNullOrEmpty(i.ItemId))
            .ToList();

        // No inputs needed or no workplace storage - skip to taking inputs (which will also skip)
        if (inputsToTake.Count == 0 || _workplaceStorage == null)
        {
            _machine.Fire(ReactionTrigger.ArrivedAtStorageForInputs);
            return new IdleAction(_owner, this, Priority);
        }

        var storage = _workplaceStorage; // Local capture for lambda (guaranteed non-null by check above)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("REACTION", $"Navigating to storage to take inputs for {_reaction.Name}", 0);
                return new GoToFacilityActivity(storage, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Log.Warn($"{_owner.Name}: Could not reach storage for {_reaction.Name}");
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        DebugLog("REACTION", $"Arrived at storage to take inputs for {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ArrivedAtStorageForInputs);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessTakingInputs(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // No workplace storage - skip taking inputs
        if (_workplaceStorage == null)
        {
            _machine.Fire(ReactionTrigger.InputsTaken);
            return new IdleAction(_owner, this, Priority);
        }

        // Check if there are inputs to take (before creating sub-activity)
        if (_currentSubActivity == null)
        {
            var inputsToTake = _reaction.Inputs
                .Where(i => !string.IsNullOrEmpty(i.ItemId))
                .Select(i => (i.ItemId!, i.Quantity))
                .ToList();

            if (inputsToTake.Count == 0)
            {
                _machine.Fire(ReactionTrigger.InputsTaken);
                return new IdleAction(_owner, this, Priority);
            }
        }

        var storage = _workplaceStorage; // Local capture for lambda (guaranteed non-null by check above)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                var inputsToTake = _reaction.Inputs
                    .Where(i => !string.IsNullOrEmpty(i.ItemId))
                    .Select(i => (i.ItemId!, i.Quantity))
                    .ToList();
                DebugLog("REACTION", $"Taking inputs for {_reaction.Name}", 0);
                return new TakeFromStorageActivity(storage, inputsToTake, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Log.Warn($"{_owner.Name}: Failed to take inputs for {_reaction.Name}");
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        DebugLog("REACTION", $"Inputs in inventory for {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.InputsTaken);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessGoingToFacility(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // No facility required - skip to executing reaction
        if (_workplaceRoom == null || _reaction.RequiredFacilities.Count == 0)
        {
            _machine.Fire(ReactionTrigger.ArrivedAtFacility);
            return new IdleAction(_owner, this, Priority);
        }

        var room = _workplaceRoom; // Local capture for lambda (guaranteed non-null by check above)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                string facilityId = _reaction.RequiredFacilities[0];
                DebugLog("REACTION", $"Navigating to {facilityId} to execute {_reaction.Name}", 0);

                // Find the facility in the room, then navigate to it directly
                var targetFacility = room.GetFacility(facilityId);
                if (targetFacility != null)
                {
                    return new GoToFacilityActivity(targetFacility, Priority);
                }

                // Fall back: just go to the room
                return new GoToRoomActivity(room, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Fall back to just proceeding - facility nav failure is not fatal
                Log.Warn($"{_owner.Name}: Could not reach facility for {_reaction.Name}, proceeding anyway");
                _machine.Fire(ReactionTrigger.ArrivedAtFacility);
                return new IdleAction(_owner, this, Priority);
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        DebugLog("REACTION", $"At {_reaction.RequiredFacilities[0]} to process {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ArrivedAtFacility);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessExecutingReaction()
    {
        if (_owner == null)
        {
            return null;
        }

        DebugLog("REACTION", $"Executing {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ReactionExecuted);
        return new ExecuteReactionAction(_owner, this, _reaction, Priority);
    }

    private EntityAction? ProcessProcessing()
    {
        if (_owner == null)
        {
            return null;
        }

        _processTimer++;

        // Spend energy while processing (base cost * reaction multiplier)
        float energyCost = BASEENERGYCOSTPERTICK * _reaction.EnergyCostMultiplier;
        _energyNeed?.Restore(-energyCost);

        if (_processTimer < _variedDuration)
        {
            // Still processing, idle
            return new IdleAction(_owner, this, Priority);
        }

        // Processing complete - transition to output storage
        DebugLog("REACTION", $"Processing complete for {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ProcessingComplete);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessGoingToStorageForOutputs(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // No workplace storage - skip depositing (outputs stay in inventory)
        if (_workplaceStorage == null)
        {
            // No workplace storage means we can't deposit - just complete
            Log.Print($"{_owner.Name}: Completed {_reaction.Name} at {_workplaceRoom?.Name ?? "workplace"}");
            Complete();
            return null;
        }

        var storageForOutputNav = _workplaceStorage; // Local capture for lambda (guaranteed non-null by check above)
        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                DebugLog("REACTION", $"Navigating to storage to deposit outputs for {_reaction.Name}", 0);
                return new GoToFacilityActivity(storageForOutputNav, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Outputs stay in inventory - not a complete failure
                Log.Warn($"{_owner.Name}: Could not reach storage for {_reaction.Name}, outputs remain in inventory");
                Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
                Complete();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        DebugLog("REACTION", $"Arrived at storage to deposit outputs for {_reaction.Name}", 0);
        _machine.Fire(ReactionTrigger.ArrivedAtStorageForOutputs);
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDepositingOutputs(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Complete();
            return null;
        }

        // Check if there are outputs to deposit (before creating sub-activity)
        if (_currentSubActivity == null)
        {
            var outputsToDeposit = _reaction.Outputs
                .Where(o => !string.IsNullOrEmpty(o.ItemId))
                .Select(o => (o.ItemId!, o.Quantity))
                .ToList();

            if (outputsToDeposit.Count == 0)
            {
                Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
                Complete();
                return null;
            }
        }

        var storage = _workplaceStorage;
        if (storage == null)
        {
            Complete();
            return null;
        }

        var (result, action) = RunCurrentSubActivity(
            () =>
            {
                var outputsToDeposit = _reaction.Outputs
                    .Where(o => !string.IsNullOrEmpty(o.ItemId))
                    .Select(o => (o.ItemId!, o.Quantity))
                    .ToList();
                DebugLog("REACTION", $"Depositing outputs for {_reaction.Name}", 0);
                return new DepositToStorageActivity(storage, outputsToDeposit, Priority);
            },
            position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                // Outputs stay in inventory - not a complete failure
                Log.Warn($"{_owner.Name}: Could not deposit all outputs for {_reaction.Name}, outputs remain in inventory");
                Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
                Complete();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                break;
        }

        Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
        Complete();
        return null;
    }
}
