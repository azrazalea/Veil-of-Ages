using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for processing a reaction (crafting).
/// Goes to workplace (if specified), navigates to required facility,
/// checks inputs, waits for duration, consumes inputs, and produces outputs.
/// Consumes energy while processing based on reaction's EnergyCostMultiplier.
/// </summary>
public class ProcessReactionActivity : Activity
{
    // Base energy cost per tick while processing (same as WorkFieldActivity)
    // Multiplied by reaction's EnergyCostMultiplier
    private const float BASEENERGYCOSTPERTICK = 0.01f;

    private readonly ReactionDefinition _reaction;
    private readonly Building? _workplace;
    private readonly StorageTrait _storage;

    private GoToBuildingActivity? _goToBuildingPhase;
    private GoToFacilityActivity? _goToFacilityPhase;
    private uint _processTimer;
    private bool _isAtBuilding;
    private bool _isAtFacility;
    private bool _isProcessing;
    private bool _inputsConsumed;
    private Need? _energyNeed;

    public override string DisplayName => _isProcessing
        ? $"Processing {_reaction.Name}"
        : $"Going to process {_reaction.Name}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessReactionActivity"/> class.
    /// Create an activity to process a reaction at a workplace.
    /// </summary>
    /// <param name="reaction">The reaction definition to process.</param>
    /// <param name="workplace">The building with required facilities (can be null if no facilities required).</param>
    /// <param name="storage">The storage to use for inputs/outputs.</param>
    /// <param name="priority">Action priority.</param>
    public ProcessReactionActivity(ReactionDefinition reaction, Building? workplace, StorageTrait storage, int priority = 0)
    {
        _reaction = reaction;
        _workplace = workplace;
        _storage = storage;
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
            Log.Print($"{_owner.Name}: Arrived at {_workplace.BuildingName} to process {_reaction.Name}");
        }
        else if (_workplace == null)
        {
            // No workplace needed
            _isAtBuilding = true;
        }

        // Phase 2: Go to specific facility if reaction requires one and not yet there
        if (_isAtBuilding && !_isAtFacility)
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
                        Log.Print($"{_owner.Name}: At {_reaction.RequiredFacilities[0]} to process {_reaction.Name}");
                        break;
                }
            }
            else
            {
                // No facility required
                _isAtFacility = true;
            }
        }

        // Phase 3: Start processing once at facility
        if (_isAtFacility && !_isProcessing)
        {
            _isProcessing = true;
            Log.Print($"{_owner.Name}: Ready to process {_reaction.Name} (Storage: {_storage.GetContentsSummary()})");
        }

        // Phase 4: Check inputs and process
        if (_isProcessing)
        {
            // On first processing tick, verify and consume inputs
            if (!_inputsConsumed)
            {
                if (!VerifyAndConsumeInputs())
                {
                    Log.Warn($"{_owner.Name}: Missing inputs for {_reaction.Name}");
                    Fail();
                    return null;
                }

                _inputsConsumed = true;
                Log.Print($"{_owner.Name}: Started processing {_reaction.Name}");
            }

            _processTimer++;

            // Spend energy while processing (base cost * reaction multiplier)
            float energyCost = BASEENERGYCOSTPERTICK * _reaction.EnergyCostMultiplier;
            _energyNeed?.Restore(-energyCost);

            if (_processTimer >= _reaction.Duration)
            {
                // Processing complete - produce outputs
                ProduceOutputs();
                Log.Print($"{_owner.Name}: Completed {_reaction.Name} (Storage: {_storage.GetContentsSummary()})");
                Complete();
                return null;
            }

            // Still processing, idle
            return new IdleAction(_owner, this, Priority);
        }

        return null;
    }

    /// <summary>
    /// Verify inputs are available and consume them.
    /// </summary>
    private bool VerifyAndConsumeInputs()
    {
        if (_reaction.Inputs == null || _reaction.Inputs.Count == 0)
        {
            return true;
        }

        // First verify all inputs are available
        foreach (var input in _reaction.Inputs)
        {
            if (!_storage.HasItem(input.ItemId, input.Quantity))
            {
                return false;
            }
        }

        // Then consume them
        foreach (var input in _reaction.Inputs)
        {
            var removed = _storage.RemoveItem(input.ItemId, input.Quantity);
            if (removed == null)
            {
                // This shouldn't happen since we just verified, but handle it
                Log.Error($"ProcessReactionActivity: Failed to consume {input.Quantity}x {input.ItemId}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Produce output items and add to storage.
    /// </summary>
    private void ProduceOutputs()
    {
        if (_reaction.Outputs == null || _reaction.Outputs.Count == 0)
        {
            return;
        }

        foreach (var output in _reaction.Outputs)
        {
            var itemDef = ItemResourceManager.Instance.GetDefinition(output.ItemId);
            if (itemDef == null)
            {
                Log.Error($"ProcessReactionActivity: Output item '{output.ItemId}' not found");
                continue;
            }

            var item = new Item(itemDef, output.Quantity);
            if (!_storage.AddItem(item))
            {
                Log.Warn($"{_owner?.Name}: Storage full, {output.Quantity}x {output.ItemId} lost!");
            }
        }
    }
}
