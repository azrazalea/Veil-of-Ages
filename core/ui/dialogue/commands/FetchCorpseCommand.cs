using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands;

/// <summary>
/// Command that fetches a corpse from the nearest graveyard and brings it to the necromancy altar.
/// The altar building is passed via Parameters ("altarBuilding").
///
/// Thin wrapper around FetchResourceActivity which handles the full
/// go→take→return→deposit pattern including cross-area navigation.
/// </summary>
public class FetchCorpseCommand : EntityCommand
{
    public override string DisplayName => L.Tr("command.FETCH_CORPSE");

    private Building? _altarBuilding;
    private FetchResourceActivity? _fetchActivity;

    public FetchCorpseCommand(Being owner, Being commander)
        : base(owner, commander, isComplex: false)
    {
    }

    public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
    {
        // Resolve altar building from parameters on first call
        if (_altarBuilding == null)
        {
            if (!Parameters.TryGetValue("altarBuilding", out var altarObj) || altarObj is not Building altar)
            {
                Log.Warn($"{_owner.Name}: FetchCorpseCommand has no altarBuilding parameter");
                return null;
            }

            _altarBuilding = altar;
        }

        if (!GodotObject.IsInstanceValid(_altarBuilding))
        {
            Log.Warn($"{_owner.Name}: Altar building no longer valid");
            return null;
        }

        // Create sub-activity lazily
        if (_fetchActivity == null)
        {
            // Find graveyard via SharedKnowledge (BDI-compliant — entity only knows
            // what their knowledge sources tell them)
            var graveyardRef = _owner.FindNearestBuildingOfType("Graveyard", _owner.GetCurrentGridPosition());
            if (graveyardRef == null || !graveyardRef.IsValid)
            {
                Log.Warn($"{_owner.Name}: Graveyard building reference is invalid");
                return null;
            }

            _fetchActivity = new FetchResourceActivity(
                graveyardRef.Building!, _altarBuilding, "corpse", 1, priority: -1);
            InitializeSubActivity(_fetchActivity);
        }

        // Drive via RunSubActivity
        var (result, action) = RunSubActivity(_fetchActivity, currentGridPos, currentPerception, priority: -1);
        return result switch
        {
            Activity.SubActivityResult.Continue => action,
            _ => null // Completed or Failed — command done
        };
    }
}
