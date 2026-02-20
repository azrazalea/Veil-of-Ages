using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands;

/// <summary>
/// Command that fetches a corpse from the nearest graveyard and brings it to the necromancy altar.
/// The altar facility is passed via Parameters ("altarFacility").
///
/// Thin wrapper around FetchResourceActivity which handles the full
/// go→take→return→deposit pattern including cross-area navigation.
/// </summary>
public class FetchCorpseCommand : EntityCommand
{
    public override string DisplayName => L.Tr("command.FETCH_CORPSE");

    private Facility? _altarFacility;
    private FetchResourceActivity? _fetchActivity;

    public FetchCorpseCommand(Being owner, Being commander)
        : base(owner, commander, isComplex: false)
    {
    }

    public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
    {
        // Resolve altar facility from parameters on first call
        if (_altarFacility == null)
        {
            if (!Parameters.TryGetValue("altarFacility", out var altarObj) || altarObj is not Facility altar)
            {
                Log.Warn($"{_owner.Name}: FetchCorpseCommand has no altarFacility parameter");
                return null;
            }

            _altarFacility = altar;
        }

        if (!GodotObject.IsInstanceValid(_altarFacility))
        {
            Log.Warn($"{_owner.Name}: Altar facility no longer valid");
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

            var altarBuilding = _altarFacility.Owner;
            if (altarBuilding == null || !GodotObject.IsInstanceValid(altarBuilding))
            {
                Log.Warn($"{_owner.Name}: FetchCorpseCommand: Altar facility has no valid owner building");
                return null;
            }

            var graveyardBuilding = graveyardRef.Building;
            if (graveyardBuilding == null || !GodotObject.IsInstanceValid(graveyardBuilding))
            {
                Log.Warn($"{_owner.Name}: FetchCorpseCommand: Graveyard building reference is invalid");
                return null;
            }

            _fetchActivity = new FetchResourceActivity(
                graveyardBuilding, altarBuilding, "corpse", 1, priority: -1);
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
