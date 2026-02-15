using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Unified sleep/scheduling trait for all living entities.
/// Decides when an entity should sleep based on energy level and time of day.
/// Works identically for players and NPCs, replacing sleep logic previously
/// split across PlayerBehaviorTrait and VillagerTrait.
/// </summary>
public class ScheduleTrait : BeingTrait
{
    private const uint MINAWAKETICKS = 200;
    private const float FULLENERGYTHRESHOLD = 95f;

    private enum SleepState
    {
        Awake,
        GoingHome,
        Sleeping
    }

    private SleepState _sleepState = SleepState.Awake;
    private uint _wakeTick;
    private bool _allowNightWork;

    public ScheduleTrait()
    {
    }

    public override bool ValidateConfiguration(TraitConfiguration config)
    {
        return true;
    }

    public override void Configure(TraitConfiguration config)
    {
        _allowNightWork = config.GetBool("allowNightWork") ?? false;
    }

    public override void Initialize(Being owner, BodyHealth? health, Queue<BeingTrait>? initQueue = null)
    {
        base.Initialize(owner, health, initQueue);

        _wakeTick = GameController.CurrentTick;
        IsInitialized = true;
    }

    public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
    {
        if (_owner == null)
        {
            return null;
        }

        var currentActivity = _owner.GetCurrentActivity();
        var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
        var currentPhase = gameTime.CurrentDayPhase;
        uint currentTick = GameController.CurrentTick;

        // Step 1: If currently sleeping, let the activity run
        if (currentActivity is SleepActivity)
        {
            _sleepState = SleepState.Sleeping;
            return null;
        }

        // Step 5: If we were sleeping but activity is no longer SleepActivity, we woke up
        if (_sleepState == SleepState.Sleeping)
        {
            _sleepState = SleepState.Awake;
            _wakeTick = currentTick;
            DebugLog("SCHEDULE", "Woke up, transitioning to Awake", 0);
        }

        // Get energy need
        var energyNeed = _owner.NeedsSystem?.GetNeed("energy");
        bool isCriticalEnergy = energyNeed != null && energyNeed.IsCritical();
        bool isLowEnergy = energyNeed != null && energyNeed.IsLow();
        float energyValue = energyNeed?.Value ?? 100f;

        // Step 2: Min-awake cooldown (unless critical)
        if (!isCriticalEnergy && (currentTick - _wakeTick) < MINAWAKETICKS)
        {
            return null;
        }

        // Step 3: Don't initiate sleep if energy is high enough
        if (energyValue >= FULLENERGYTHRESHOLD && !isCriticalEnergy)
        {
            // If we were going home to sleep but energy recovered, cancel
            if (_sleepState == SleepState.GoingHome)
            {
                _sleepState = SleepState.Awake;
                DebugLog("SCHEDULE", "Energy recovered, cancelling go-home-to-sleep", 0);
            }

            return null;
        }

        // Step 4: Determine shouldSleep
        bool shouldSleep = false;
        int sleepPriority = 0;

        if (isCriticalEnergy)
        {
            shouldSleep = true;
            sleepPriority = -1;
            DebugLog("SCHEDULE", "Critical energy, must sleep");
        }
        else if (currentPhase == DayPhaseType.Dusk && isLowEnergy)
        {
            shouldSleep = true;
            sleepPriority = 0;
            DebugLog("SCHEDULE", "Dusk and low energy, sleeping early");
        }
        else if (currentPhase == DayPhaseType.Night && isLowEnergy)
        {
            // Check if we should defer to a night job
            if (_allowNightWork && HasActiveNightJob(currentPhase))
            {
                shouldSleep = false;
                DebugLog("SCHEDULE", "Night and low energy, but deferring to night job");
            }
            else
            {
                shouldSleep = true;
                sleepPriority = 0;
                DebugLog("SCHEDULE", "Night and low energy, sleeping");
            }
        }

        if (!shouldSleep)
        {
            // If we were going home but no longer need to sleep, cancel
            if (_sleepState == SleepState.GoingHome)
            {
                _sleepState = SleepState.Awake;
            }

            return null;
        }

        // Step 6+7: shouldSleep is true, handle home navigation and sleep
        var homeTrait = _owner.SelfAsEntity().GetTrait<HomeTrait>();
        var home = homeTrait?.Home;

        if (home != null && GodotObject.IsInstanceValid(home))
        {
            // Step 6a: If GoingHome, verify the current activity is still our go-home activity
            if (_sleepState == SleepState.GoingHome)
            {
                if (currentActivity is GoToBuildingActivity goToBuilding &&
                    goToBuilding.TargetBuilding == home)
                {
                    // Still navigating home, let it run
                    return null;
                }

                // Activity changed (interrupted, completed, or different destination)
                _sleepState = SleepState.Awake;
                DebugLog("SCHEDULE", "GoingHome activity changed, re-evaluating", 0);

                // Fall through to re-evaluate
            }

            // Step 6b: At home? Start sleeping
            if (homeTrait!.IsEntityAtHome())
            {
                DebugLog("SCHEDULE", "At home, starting SleepActivity", 0);
                _sleepState = SleepState.Sleeping;
                var sleepActivity = new SleepActivity(priority: sleepPriority);
                return new StartActivityAction(_owner, this, sleepActivity, priority: sleepPriority);
            }

            // Step 6c: Not at home, navigate there
            DebugLog("SCHEDULE", "Not at home, navigating home", 0);
            _sleepState = SleepState.GoingHome;
            var goHomeActivity = new GoToBuildingActivity(home, priority: sleepPriority);
            return new StartActivityAction(_owner, this, goHomeActivity, priority: sleepPriority);
        }

        // Step 7: No home - emergency sleep in place if critical
        if (isCriticalEnergy)
        {
            DebugLog("SCHEDULE", "No home, critical energy, sleeping in place", 0);
            _sleepState = SleepState.Sleeping;
            var sleepActivity = new SleepActivity(priority: sleepPriority);
            return new StartActivityAction(_owner, this, sleepActivity, priority: sleepPriority);
        }

        return null;
    }

    /// <summary>
    /// Check if the entity has any JobTrait with work hours during the given phase.
    /// </summary>
    private bool HasActiveNightJob(DayPhaseType currentPhase)
    {
        if (_owner == null)
        {
            return false;
        }

        foreach (var trait in _owner.Traits)
        {
            if (trait is JobTrait jobTrait && jobTrait.IsWorkHours(currentPhase))
            {
                return true;
            }
        }

        return false;
    }
}
