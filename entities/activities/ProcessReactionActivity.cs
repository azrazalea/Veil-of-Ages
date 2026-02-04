using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for processing a reaction (crafting).
/// Goes to workplace (if specified), navigates to storage to take inputs,
/// navigates to facility to execute reaction, then navigates back to storage to deposit outputs.
/// Consumes energy while processing based on reaction's EnergyCostMultiplier.
///
/// Flow:
/// 1. Navigate to building (GoToBuildingActivity)
/// 2. Navigate to storage (GoToBuildingActivity with targetStorage: true)
/// 3. Take inputs: Storage -> Inventory (TakeFromStorageActivity)
/// 4. Navigate to facility (GoToFacilityActivity)
/// 5. Execute reaction: Inventory -> Inventory (ExecuteReactionAction)
/// 6. Wait for processing duration
/// 7. Navigate to storage (GoToBuildingActivity with targetStorage: true)
/// 8. Deposit outputs: Inventory -> Storage (DepositToStorageActivity).
/// </summary>
public class ProcessReactionActivity : Activity
{
    // Base energy cost per tick while processing (same as WorkFieldActivity)
    // Multiplied by reaction's EnergyCostMultiplier
    private const float BASEENERGYCOSTPERTICK = 0.01f;

    private readonly ReactionDefinition _reaction;
    private readonly Building? _workplace;

    // Navigation phases
    private GoToBuildingActivity? _goToBuildingPhase;
    private GoToBuildingActivity? _goToStorageForInputsPhase;
    private GoToFacilityActivity? _goToFacilityPhase;
    private GoToBuildingActivity? _goToStorageForOutputsPhase;

    // Storage transfer phases
    private TakeFromStorageActivity? _takeInputsPhase;
    private DepositToStorageActivity? _depositOutputsPhase;

    // State tracking
    private uint _processTimer;
    private uint _variedDuration;
    private bool _isAtBuilding;
    private bool _atStorageForInputs;
    private bool _inputsTaken;
    private bool _isAtFacility;
    private bool _reactionExecuted;
    private bool _atStorageForOutputs;
    private bool _outputsDeposited;
    private Need? _energyNeed;

    public override string DisplayName => _reactionExecuted
        ? $"Processing {_reaction.Name}"
        : $"Preparing {_reaction.Name}";
    public override Building? TargetBuilding => _workplace;
    public override string? TargetFacilityId => _isAtFacility && _reaction.RequiredFacilities.Count > 0
        ? _reaction.RequiredFacilities[0]
        : null;

    public override List<Vector2I> GetAlternativeGoalPositions(Being entity)
    {
        // When at the facility, delegate to the facility navigation sub-activity
        if (_isAtFacility && _goToFacilityPhase != null)
        {
            return _goToFacilityPhase.GetAlternativeGoalPositions(entity);
        }

        return new List<Vector2I>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessReactionActivity"/> class.
    /// Create an activity to process a reaction at a workplace.
    /// </summary>
    /// <param name="reaction">The reaction definition to process.</param>
    /// <param name="workplace">The building with required facilities (can be null if no facilities required).</param>
    /// <param name="storage">The storage to use for inputs/outputs (kept for compatibility but not used directly).</param>
    /// <param name="priority">Action priority.</param>
    public ProcessReactionActivity(ReactionDefinition reaction, Building? workplace, StorageTrait storage, int priority = 0)
    {
        _reaction = reaction;
        _workplace = workplace;
        Priority = priority;

        // Apply reaction's hunger multiplier (defaults to 1.0 if not specified)
        if (reaction.HungerMultiplier != 1.0f)
        {
            NeedDecayMultipliers["hunger"] = reaction.HungerMultiplier;
        }
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

        // Check workplace still exists (if we have one)
        if (_workplace != null && !GodotObject.IsInstanceValid(_workplace))
        {
            Log.Warn($"{_owner.Name}: Workplace destroyed while processing {_reaction.Name}");
            Fail();
            return null;
        }

        // Phase 1: Go to workplace building if specified and not yet there
        if (_workplace != null && !_isAtBuilding)
        {
            // Initialize go-to-building phase if needed
            if (_goToBuildingPhase == null)
            {
                _goToBuildingPhase = new GoToBuildingActivity(_workplace, Priority);
                _goToBuildingPhase.Initialize(_owner);
            }

            // Run the navigation sub-activity
            var (result, action) = RunSubActivity(_goToBuildingPhase, position, perception);
            switch (result)
            {
                case SubActivityResult.Failed:
                    Fail();
                    return null;
                case SubActivityResult.Continue:
                    return action;
                case SubActivityResult.Completed:
                    // Fall through to mark as arrived
                    break;
            }

            // We've arrived at the building
            _isAtBuilding = true;
            DebugLog("REACTION", $"Arrived at {_workplace.BuildingName} to process {_reaction.Name}", 0);
        }
        else if (_workplace == null)
        {
            // No workplace needed
            _isAtBuilding = true;
        }

        // Phase 2: Navigate to storage to take inputs
        if (_isAtBuilding && !_atStorageForInputs)
        {
            if (_workplace != null)
            {
                // Check if we have inputs to take
                var inputsToTake = _reaction.Inputs
                    .Where(i => !string.IsNullOrEmpty(i.ItemId))
                    .ToList();

                if (inputsToTake.Count == 0)
                {
                    // No inputs needed, skip storage navigation
                    _atStorageForInputs = true;
                }
                else
                {
                    if (_goToStorageForInputsPhase == null)
                    {
                        _goToStorageForInputsPhase = new GoToBuildingActivity(_workplace, Priority, targetStorage: true);
                        _goToStorageForInputsPhase.Initialize(_owner);
                        DebugLog("REACTION", $"Navigating to storage to take inputs for {_reaction.Name}", 0);
                    }

                    var (result, action) = RunSubActivity(_goToStorageForInputsPhase, position, perception);
                    switch (result)
                    {
                        case SubActivityResult.Failed:
                            Log.Warn($"{_owner.Name}: Could not reach storage for {_reaction.Name}");
                            Fail();
                            return null;
                        case SubActivityResult.Continue:
                            return action;
                        case SubActivityResult.Completed:
                            _atStorageForInputs = true;
                            DebugLog("REACTION", $"Arrived at storage to take inputs for {_reaction.Name}", 0);
                            break;
                    }
                }
            }
            else
            {
                // No workplace
                _atStorageForInputs = true;
            }
        }

        // Phase 3: Take inputs from storage to inventory
        if (_atStorageForInputs && !_inputsTaken)
        {
            if (_workplace == null)
            {
                // No workplace - skip taking inputs (legacy/edge case)
                _inputsTaken = true;
            }
            else
            {
                // Initialize take-inputs phase if needed
                if (_takeInputsPhase == null)
                {
                    var inputsToTake = _reaction.Inputs
                        .Where(i => !string.IsNullOrEmpty(i.ItemId))
                        .Select(i => (i.ItemId!, i.Quantity))
                        .ToList();

                    if (inputsToTake.Count == 0)
                    {
                        // No inputs to take
                        _inputsTaken = true;
                    }
                    else
                    {
                        _takeInputsPhase = new TakeFromStorageActivity(_workplace, inputsToTake, Priority);
                        _takeInputsPhase.Initialize(_owner);
                        DebugLog("REACTION", $"Taking inputs for {_reaction.Name}", 0);
                    }
                }

                if (_takeInputsPhase != null)
                {
                    var (result, action) = RunSubActivity(_takeInputsPhase, position, perception);
                    switch (result)
                    {
                        case SubActivityResult.Failed:
                            Log.Warn($"{_owner.Name}: Failed to take inputs for {_reaction.Name}");
                            Fail();
                            return null;
                        case SubActivityResult.Continue:
                            return action;
                        case SubActivityResult.Completed:
                            _inputsTaken = true;
                            DebugLog("REACTION", $"Inputs in inventory for {_reaction.Name}", 0);
                            break;
                    }
                }
            }
        }

        // Phase 4: Navigate to facility before executing reaction
        if (_inputsTaken && !_isAtFacility)
        {
            // Check if reaction requires a facility
            if (_workplace != null && _reaction.RequiredFacilities.Count > 0)
            {
                // Initialize go-to-facility phase if needed
                if (_goToFacilityPhase == null)
                {
                    // Use the first required facility
                    string facilityId = _reaction.RequiredFacilities[0];
                    _goToFacilityPhase = new GoToFacilityActivity(_workplace, facilityId, Priority);
                    _goToFacilityPhase.Initialize(_owner);
                    DebugLog("REACTION", $"Navigating to {facilityId} to execute {_reaction.Name}", 0);
                }

                // Run the navigation sub-activity
                var (result, action) = RunSubActivity(_goToFacilityPhase, position, perception);
                switch (result)
                {
                    case SubActivityResult.Failed:
                        // Fall back to just being at the building
                        Log.Warn($"{_owner.Name}: Could not reach facility for {_reaction.Name}, proceeding anyway");
                        _isAtFacility = true;
                        break;
                    case SubActivityResult.Continue:
                        return action;
                    case SubActivityResult.Completed:
                        _isAtFacility = true;
                        DebugLog("REACTION", $"At {_reaction.RequiredFacilities[0]} to process {_reaction.Name}", 0);
                        break;
                }
            }
            else
            {
                // No facility required
                _isAtFacility = true;
            }
        }

        // Phase 5: Execute reaction (inventory -> inventory)
        if (_isAtFacility && !_reactionExecuted)
        {
            _reactionExecuted = true;
            DebugLog("REACTION", $"Executing {_reaction.Name}", 0);
            return new ExecuteReactionAction(_owner, this, _reaction, Priority);
        }

        // Phase 6: Wait for processing duration
        if (_reactionExecuted && _processTimer < _variedDuration)
        {
            _processTimer++;

            // Spend energy while processing (base cost * reaction multiplier)
            float energyCost = BASEENERGYCOSTPERTICK * _reaction.EnergyCostMultiplier;
            _energyNeed?.Restore(-energyCost);

            // Still processing, idle
            return new IdleAction(_owner, this, Priority);
        }

        // Phase 7: Navigate to storage then deposit outputs
        if (_reactionExecuted && !_outputsDeposited)
        {
            if (_workplace == null)
            {
                // No workplace - skip depositing (outputs stay in inventory)
                _outputsDeposited = true;
            }
            else
            {
                // Phase 7a: Navigate to storage first (handles RequireAdjacent)
                if (!_atStorageForOutputs)
                {
                    if (_goToStorageForOutputsPhase == null)
                    {
                        _goToStorageForOutputsPhase = new GoToBuildingActivity(_workplace, Priority, targetStorage: true);
                        _goToStorageForOutputsPhase.Initialize(_owner);
                        DebugLog("REACTION", $"Navigating to storage to deposit outputs for {_reaction.Name}", 0);
                    }

                    var (result, action) = RunSubActivity(_goToStorageForOutputsPhase, position, perception);
                    switch (result)
                    {
                        case SubActivityResult.Failed:
                            // Outputs stay in inventory - not a complete failure
                            Log.Warn($"{_owner.Name}: Could not reach storage for {_reaction.Name}, outputs remain in inventory");
                            _outputsDeposited = true;
                            break;
                        case SubActivityResult.Continue:
                            return action;
                        case SubActivityResult.Completed:
                            _atStorageForOutputs = true;
                            DebugLog("REACTION", $"Arrived at storage to deposit outputs for {_reaction.Name}", 0);
                            break;
                    }
                }

                // Phase 8: Deposit outputs to storage
                if (_atStorageForOutputs && !_outputsDeposited)
                {
                    // Initialize deposit-outputs phase if needed
                    if (_depositOutputsPhase == null)
                    {
                        var outputsToDeposit = _reaction.Outputs
                            .Where(o => !string.IsNullOrEmpty(o.ItemId))
                            .Select(o => (o.ItemId!, o.Quantity))
                            .ToList();

                        if (outputsToDeposit.Count == 0)
                        {
                            // No outputs to deposit
                            _outputsDeposited = true;
                        }
                        else
                        {
                            _depositOutputsPhase = new DepositToStorageActivity(_workplace, outputsToDeposit, Priority);
                            _depositOutputsPhase.Initialize(_owner);
                            DebugLog("REACTION", $"Depositing outputs for {_reaction.Name}", 0);
                        }
                    }

                    if (_depositOutputsPhase != null)
                    {
                        var (result, action) = RunSubActivity(_depositOutputsPhase, position, perception);
                        switch (result)
                        {
                            case SubActivityResult.Failed:
                                // Outputs stay in inventory - not a complete failure
                                Log.Warn($"{_owner.Name}: Could not deposit all outputs for {_reaction.Name}, outputs remain in inventory");
                                _outputsDeposited = true;
                                break;
                            case SubActivityResult.Continue:
                                return action;
                            case SubActivityResult.Completed:
                                _outputsDeposited = true;
                                break;
                        }
                    }
                }
            }
        }

        // All phases complete
        if (_outputsDeposited)
        {
            Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
            Complete();
            return null;
        }

        return null;
    }
}
