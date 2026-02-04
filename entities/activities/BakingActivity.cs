using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for baking at a bakery workplace.
/// Phases:
/// 1. Navigate to workplace building
/// 2. Check if workplace has wheat
/// 3. Bake (consume wheat, produce bread over time)
/// 4. Complete.
/// </summary>
public class BakingActivity : Activity
{
    private enum BakingPhase
    {
        GoingToWork,
        CheckingIngredients,
        Baking,
        Done
    }

    // Amount of wheat consumed per baking cycle
    private const int WHEATPERBAKE = 1;

    // Amount of bread produced per baking cycle
    private const int BREADPERBAKE = 2;

    private readonly Building _workplace;
    private readonly uint _bakeDuration;

    private GoToBuildingActivity? _goToWorkPhase;
    private uint _bakeTimer;
    private BakingPhase _currentPhase = BakingPhase.GoingToWork;
    private ConsumeFromStorageAction? _consumeWheatAction;
    private ProduceToStorageAction? _produceBreadAction;
    private bool _wheatConsumed;

    public override string DisplayName => _currentPhase switch
    {
        BakingPhase.GoingToWork => "Going to bakery",
        BakingPhase.CheckingIngredients => "Checking ingredients",
        BakingPhase.Baking => $"Baking at {_workplace.BuildingType}",
        BakingPhase.Done => "Finished baking",
        _ => "Baking"
    };

    public override Building? TargetBuilding => _workplace;

    /// <summary>
    /// Initializes a new instance of the <see cref="BakingActivity"/> class.
    /// Creates an activity to bake at a workplace.
    /// </summary>
    /// <param name="workplace">The bakery building to work at.</param>
    /// <param name="duration">How many ticks the baking process takes.</param>
    /// <param name="priority">Action priority (default 0).</param>
    public BakingActivity(Building workplace, uint duration, int priority = 0)
    {
        _workplace = workplace;
        _bakeDuration = duration;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        DebugLog("BAKING", $"Started BakingActivity at {_workplace.BuildingName}, priority: {Priority}, bake duration: {_bakeDuration} ticks", 0);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if workplace still exists
        if (!GodotObject.IsInstanceValid(_workplace))
        {
            DebugLog("BAKING", "Workplace no longer valid, failing activity", 0);
            Fail();
            return null;
        }

        return _currentPhase switch
        {
            BakingPhase.GoingToWork => ProcessGoingToWork(position, perception),
            BakingPhase.CheckingIngredients => ProcessCheckingIngredients(),
            BakingPhase.Baking => ProcessBaking(),
            BakingPhase.Done => ProcessDone(),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToWork(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to phase if needed
        if (_goToWorkPhase == null)
        {
            _goToWorkPhase = new GoToBuildingActivity(_workplace, Priority, targetStorage: true);
            _goToWorkPhase.Initialize(_owner);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToWorkPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("BAKING", "Failed to navigate to workplace", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at building
        Log.Print($"{_owner.Name}: Arrived at {_workplace.BuildingType}");
        DebugLog("BAKING", "Arrived at workplace, transitioning to CheckingIngredients", 0);

        _currentPhase = BakingPhase.CheckingIngredients;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessCheckingIngredients()
    {
        if (_owner == null)
        {
            return null;
        }

        // Check how much wheat is available using memory (auto-observes when accessed)
        int wheatAvailable = _owner.GetStorageItemCount(_workplace, "wheat");
        DebugLog("BAKING", $"Checking ingredients: wheat available = {wheatAvailable}, need = {WHEATPERBAKE}", 0);

        if (wheatAvailable < WHEATPERBAKE)
        {
            Log.Print($"{_owner.Name}: No wheat available at {_workplace.BuildingName}, cannot bake");
            DebugLog("BAKING", "No wheat available, failing activity", 0);
            Fail();
            return null;
        }

        // Wheat is available, transition to baking phase
        Log.Print($"{_owner.Name}: Starting to bake at {_workplace.BuildingName}");
        DebugLog("BAKING", $"Wheat available, transitioning to Baking phase (duration: {_bakeDuration} ticks)", 0);

        _currentPhase = BakingPhase.Baking;
        _bakeTimer = 0;
        _wheatConsumed = false;

        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessBaking()
    {
        if (_owner == null)
        {
            return null;
        }

        // First tick of baking: consume wheat
        if (!_wheatConsumed)
        {
            // Check if we already sent a consume action
            if (_consumeWheatAction != null)
            {
                // Action was executed - check result
                if (_consumeWheatAction.ActualQuantity > 0)
                {
                    DebugLog("BAKING", $"Consumed {_consumeWheatAction.ActualQuantity} wheat", 0);
                    _wheatConsumed = true;
                }
                else
                {
                    Log.Warn($"{_owner.Name}: Failed to consume wheat from storage");
                    DebugLog("BAKING", "Failed to consume wheat, failing activity", 0);
                    Fail();
                    return null;
                }

                _consumeWheatAction = null;
            }
            else
            {
                // Create action to consume wheat
                DebugLog("BAKING", $"Consuming {WHEATPERBAKE} wheat from storage", 0);
                _consumeWheatAction = new ConsumeFromStorageAction(
                    _owner,
                    this,
                    _workplace,
                    "wheat",
                    WHEATPERBAKE,
                    Priority);

                return _consumeWheatAction;
            }
        }

        // Progress the baking timer
        _bakeTimer++;

        // Periodic progress log
        DebugLog("BAKING", $"Baking... progress: {_bakeTimer}/{_bakeDuration} ticks");

        // Check if baking is complete
        if (_bakeTimer >= _bakeDuration)
        {
            // Check if we already sent a produce action
            if (_produceBreadAction != null)
            {
                // Action was executed - check result
                if (_produceBreadAction.ActualProduced > 0)
                {
                    var storage = _workplace.GetStorage();
                    Log.Print($"{_owner.Name}: Baked {_produceBreadAction.ActualProduced} bread at {_workplace.BuildingName} (Storage: {storage?.GetContentsSummary() ?? "unknown"})");
                    DebugLog("BAKING", $"Produced {_produceBreadAction.ActualProduced} bread, transitioning to Done", 0);
                }
                else
                {
                    Log.Warn($"{_owner.Name}: Bakery storage full or unavailable, bread lost!");
                    DebugLog("BAKING", "Failed to produce bread (storage full?), transitioning to Done anyway", 0);
                }

                _produceBreadAction = null;
                _currentPhase = BakingPhase.Done;
                return new IdleAction(_owner, this, Priority);
            }
            else
            {
                // Create action to produce bread
                DebugLog("BAKING", $"Baking complete, producing {BREADPERBAKE} bread", 0);
                _produceBreadAction = new ProduceToStorageAction(
                    _owner,
                    this,
                    _workplace,
                    "bread",
                    BREADPERBAKE,
                    Priority);

                return _produceBreadAction;
            }
        }

        // Still baking, idle
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessDone()
    {
        DebugLog("BAKING", "Baking activity complete", 0);
        Complete();
        return null;
    }
}
