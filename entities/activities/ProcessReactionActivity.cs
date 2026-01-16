using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Reactions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for processing a reaction (crafting).
/// Goes to workplace (if specified), checks inputs, waits for duration,
/// consumes inputs, and produces outputs.
/// </summary>
public class ProcessReactionActivity : Activity
{
    private readonly ReactionDefinition _reaction;
    private readonly Building? _workplace;
    private readonly StorageTrait _storage;

    private GoToBuildingActivity? _goToPhase;
    private uint _processTimer;
    private bool _isProcessing;
    private bool _inputsConsumed;

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
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Phase 1: Go to workplace if specified
        if (_workplace != null && !_isProcessing)
        {
            // Check workplace still exists
            if (!GodotObject.IsInstanceValid(_workplace))
            {
                Log.Warn($"{_owner.Name}: Workplace destroyed while heading to process {_reaction.Name}");
                Fail();
                return null;
            }

            // Initialize go-to phase if needed
            if (_goToPhase == null)
            {
                _goToPhase = new GoToBuildingActivity(_workplace, Priority);
                _goToPhase.Initialize(_owner);
            }

            // Check if navigation failed
            if (_goToPhase.State == ActivityState.Failed)
            {
                Fail();
                return null;
            }

            // Check if we've arrived
            if (_goToPhase.State == ActivityState.Completed)
            {
                _isProcessing = true;
                Log.Print($"{_owner.Name}: Arrived at workplace to process {_reaction.Name}");
            }
            else
            {
                // Still navigating
                return _goToPhase.GetNextAction(position, perception);
            }
        }
        else if (_workplace == null && !_isProcessing)
        {
            // No workplace needed, start processing immediately
            _isProcessing = true;
        }

        // Phase 2: Check inputs and process
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

            if (_processTimer >= _reaction.Duration)
            {
                // Processing complete - produce outputs
                ProduceOutputs();
                Log.Print($"{_owner.Name}: Completed {_reaction.Name}");
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
