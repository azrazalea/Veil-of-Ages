using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Activity for studying necromancy and dark arts at home.
/// Phases:
/// 1. Navigate to home building (if not already there)
/// 2. Study dark arts (idle, spend energy, gain necromancy + arcane_theory XP)
/// Completes after study duration or if energy is low (to allow sleep).
/// Drains energy faster than normal study (dark arts are demanding).
/// </summary>
public class StudyNecromancyActivity : Activity
{
    private enum StudyPhase
    {
        GoingToStudy,
        Studying
    }

    // Energy cost per tick while actively studying dark arts
    // Necromantic study is more taxing than normal study - 0.015/tick
    private const float ENERGYCOSTPERTICK = 0.015f;

    private readonly Building _home;
    private readonly uint _studyDuration;

    private GoToBuildingActivity? _goToStudyPhase;
    private uint _studyTimer;
    private StudyPhase _currentPhase = StudyPhase.GoingToStudy;
    private Need? _energyNeed;

    public override string DisplayName => _currentPhase switch
    {
        StudyPhase.GoingToStudy => "Going to study dark arts",
        StudyPhase.Studying => "Studying dark arts",
        _ => "Studying dark arts"
    };

    public override Building? TargetBuilding => _home;

    public StudyNecromancyActivity(Building home, uint studyDuration = 400, int priority = 0)
    {
        _home = home;
        _studyDuration = studyDuration;
        Priority = priority;

        // Dark arts study makes you hungrier than normal study
        NeedDecayMultipliers["hunger"] = 1.15f;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);
        _energyNeed = owner.NeedsSystem?.GetNeed("energy");
        DebugLog("ACTIVITY", $"Started StudyNecromancyActivity at {_home.BuildingName}, priority: {Priority}, study duration: {_studyDuration} ticks", 0);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _goToStudyPhase = null;
        _currentPhase = StudyPhase.GoingToStudy;
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        if (!GodotObject.IsInstanceValid(_home))
        {
            Fail();
            return null;
        }

        // Check if energy is low - stop studying to allow sleep
        if (_energyNeed != null && _energyNeed.IsLow())
        {
            DebugLog("ACTIVITY", "Energy is low, completing study to allow sleep", 0);
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

        if (_goToStudyPhase == null)
        {
            _goToStudyPhase = new GoToBuildingActivity(_home, Priority);
            _goToStudyPhase.Initialize(_owner);
        }

        var (result, action) = RunSubActivity(_goToStudyPhase, position, perception);
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

        Log.Print($"{_owner.Name}: Arrived at {_home.BuildingName} to study dark arts");
        DebugLog("ACTIVITY", $"Arrived at home, starting necromancy study phase (duration: {_studyDuration} ticks)", 0);

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
        _energyNeed?.Restore(-ENERGYCOSTPERTICK);
        _owner.SkillSystem?.GainXp("necromancy", 0.012f);
        _owner.SkillSystem?.GainXp("arcane_theory", 0.004f);

        if (_studyTimer >= _studyDuration)
        {
            Log.Print($"{_owner.Name}: Completed necromancy studying session");
            DebugLog("ACTIVITY", $"Necromancy study phase complete after {_studyTimer} ticks", 0);
            Complete();
            return null;
        }

        DebugLog("ACTIVITY", $"Studying dark arts... progress: {_studyTimer}/{_studyDuration} ticks, energy: {_energyNeed?.Value:F1}");
        return new IdleAction(_owner, this, Priority);
    }
}
