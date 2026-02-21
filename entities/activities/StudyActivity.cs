using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for studying at home during daytime.
/// Phases:
/// 1. Navigate to home room (if not already there)
/// 2. Study (idle, spend energy)
/// Completes after study duration or if day phase ends.
/// Drains energy while studying (mental work is taxing).
/// </summary>
public class StudyActivity : Activity
{
    private enum StudyPhase
    {
        GoingToStudy,
        Studying
    }

    // Energy cost per tick while actively studying
    // Mental work is taxing - 0.01/tick
    private const float ENERGYCOSTPERTICK = 0.01f;

    private readonly Room _homeRoom;
    private readonly uint _studyDuration;

    private Activity? _goToStudyPhase;
    private uint _studyTimer;
    private StudyPhase _currentPhase = StudyPhase.GoingToStudy;
    private Need? _energyNeed;

    public override string DisplayName => _currentPhase switch
    {
        StudyPhase.GoingToStudy => L.Tr("activity.GOING_TO_STUDY"),
        StudyPhase.Studying => L.Tr("activity.STUDYING"),
        _ => L.Tr("activity.STUDYING")
    };

    public override Room? TargetRoom => _homeRoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="StudyActivity"/> class.
    /// Create an activity to study at home.
    /// </summary>
    /// <param name="homeRoom">The room to study at (home).</param>
    /// <param name="studyDuration">How many ticks to study (default 400).</param>
    /// <param name="priority">Action priority (default 0).</param>
    public StudyActivity(Room homeRoom, uint studyDuration = 400, int priority = 0)
    {
        _homeRoom = homeRoom;
        _studyDuration = studyDuration;
        Priority = priority;

        // Studying makes you slightly hungry faster
        NeedDecayMultipliers["hunger"] = 1.1f;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);

        // Get energy need - studying directly costs energy (not via decay multiplier)
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");

        DebugLog("ACTIVITY", $"Started StudyActivity at {_homeRoom.Name}, priority: {Priority}, study duration: {_studyDuration} ticks", 0);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _goToStudyPhase = null; // Force fresh pathfinder
        _currentPhase = StudyPhase.GoingToStudy; // Re-navigate home before resuming
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if home room still exists (Room is plain C#, not GodotObject)
        if (_homeRoom.IsDestroyed)
        {
            Fail();
            return null;
        }

        // Check if work time has ended (day phase changed to dusk/night)
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day))
        {
            DebugLog("ACTIVITY", "Study time ended (no longer Dawn/Day), completing activity", 0);
            Complete();
            return null;
        }

        return _currentPhase switch
        {
            StudyPhase.GoingToStudy => ProcessGoingToStudy(position, perception),
            StudyPhase.Studying => ProcessStudying(),
            _ => null
        };
    }

    private EntityAction? ProcessGoingToStudy(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            return null;
        }

        // Initialize go-to phase if needed
        if (_goToStudyPhase == null)
        {
            _goToStudyPhase = new GoToRoomActivity(_homeRoom, Priority);
            _goToStudyPhase.Initialize(_owner);
        }

        // Run the navigation sub-activity
        var (result, action) = RunSubActivity(_goToStudyPhase, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                // Fall through to handle arrival
                break;
        }

        // We've arrived at home
        Log.Print($"{_owner.Name}: Arrived at {_homeRoom.Name} to study");
        DebugLog("ACTIVITY", $"Arrived at home, starting study phase (duration: {_studyDuration} ticks)", 0);

        _currentPhase = StudyPhase.Studying;
        return new IdleAction(_owner, this, Priority);
    }

    private EntityAction? ProcessStudying()
    {
        if (_owner == null)
        {
            return null;
        }

        _studyTimer++;

        // Directly spend energy while studying
        _energyNeed?.Restore(-ENERGYCOSTPERTICK);

        // Grant research skill XP while studying
        _owner.SkillSystem?.GainXp("research", 0.01f);

        if (_studyTimer >= _studyDuration)
        {
            Log.Print($"{_owner.Name}: Completed studying session");
            DebugLog("ACTIVITY", $"Study phase complete after {_studyTimer} ticks", 0);
            Complete();
            return null;
        }

        // Periodic progress log while studying
        DebugLog("ACTIVITY", $"Studying... progress: {_studyTimer}/{_studyDuration} ticks, energy: {_energyNeed?.Value:F1}");

        // Still studying, idle
        return new IdleAction(_owner, this, Priority);
    }
}
