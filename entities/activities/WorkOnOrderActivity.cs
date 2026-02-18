using System;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Entities.WorkOrders;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Generic activity for working on a facility's active work order.
/// Phase 1: Navigate to the facility (cross-area if needed).
/// Phase 2: Work on the order each tick (advance progress, grant XP, drain energy).
/// Exits early if energy goes critical or work order completes.
/// </summary>
public class WorkOnOrderActivity : Activity
{
    private enum Phase
    {
        Navigating,
        Working
    }

    private readonly FacilityReference _facilityRef;
    private readonly Facility _facility;
    private readonly DayPhaseType[] ? _allowedPhases;
    private Phase _currentPhase = Phase.Navigating;
    private Activity? _navigationActivity;

    public override string DisplayName => L.TrFmt("activity.WORKING_ON_ORDER", _facility.ActiveWorkOrder?.Type ?? L.Tr("activity.ORDER_DEFAULT"));

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOnOrderActivity"/> class.
    /// </summary>
    /// <param name="facilityRef">Reference to the facility to work at.</param>
    /// <param name="facility">The facility with the active work order.</param>
    /// <param name="priority">Action priority (default 0).</param>
    /// <param name="allowedPhases">Day phases when work is allowed. If null, work is allowed at any time.</param>
    public WorkOnOrderActivity(FacilityReference facilityRef, Facility facility, int priority = 0, DayPhaseType[] ? allowedPhases = null)
    {
        _facilityRef = facilityRef;
        _facility = facility;
        _allowedPhases = allowedPhases;
        Priority = priority;
    }

    public override void Initialize(Being owner)
    {
        base.Initialize(owner);
    }

    public override EntityAction? GetNextAction(Vector2I position, Perception perception)
    {
        if (_owner == null)
        {
            Fail();
            return null;
        }

        // Check if work order still exists
        if (_facility.ActiveWorkOrder == null)
        {
            DebugLog("WORK_ORDER", "No active work order on facility", 0);
            Complete();
            return null;
        }

        switch (_currentPhase)
        {
            case Phase.Navigating:
                return HandleNavigation(position, perception);
            case Phase.Working:
                return HandleWorking(position, perception);
        }

        return null;
    }

    private EntityAction? HandleNavigation(Vector2I position, Perception perception)
    {
        if (_navigationActivity == null)
        {
            _navigationActivity = NavigationHelper.CreateNavigationToFacility(_owner!, _facilityRef, Priority);
            _navigationActivity.Initialize(_owner!);
        }

        var (result, action) = RunSubActivity(_navigationActivity, position, perception);
        switch (result)
        {
            case SubActivityResult.Failed:
                DebugLog("WORK_ORDER", "Failed to navigate to facility", 0);
                Fail();
                return null;
            case SubActivityResult.Continue:
                return action;
            case SubActivityResult.Completed:
                _navigationActivity = null;
                _currentPhase = Phase.Working;
                DebugLog("WORK_ORDER", "Arrived at facility, starting work", 0);
                return HandleWorking(position, perception);
        }

        return null;
    }

    private EntityAction? HandleWorking(Vector2I position, Perception perception)
    {
        var workOrder = _facility.ActiveWorkOrder;
        if (workOrder == null || workOrder.IsComplete)
        {
            DebugLog("WORK_ORDER", "Work order complete!", 0);
            _facility.CompleteWorkOrder();
            Complete();
            return null;
        }

        // Check energy - exit if critical
        var energyNeed = _owner!.NeedsSystem?.GetNeed("energy");
        if (energyNeed != null && energyNeed.IsCritical())
        {
            DebugLog("WORK_ORDER", "Energy critical, stopping work", 0);
            Complete(); // Complete (not fail) - work order stays on facility for later
            return null;
        }

        // Check time restriction if configured
        if (_allowedPhases != null)
        {
            var gameTime = _owner.GameController?.CurrentGameTime ?? new GameTime(0);
            if (!Array.Exists(_allowedPhases, phase => phase == gameTime.CurrentDayPhase))
            {
                DebugLog("WORK_ORDER", $"Current phase {gameTime.CurrentDayPhase} not in allowed phases, stopping work", 0);
                Complete();
                return null;
            }
        }

        // Advance the work order
        workOrder.Advance(_owner);

        DebugLog("WORK_ORDER", $"Working... {workOrder.GetProgressString()}");

        // Check if just completed
        if (workOrder.IsComplete)
        {
            DebugLog("WORK_ORDER", "Work order just completed!", 0);
            _facility.CompleteWorkOrder();
            Complete();
            return null;
        }

        return new IdleAction(_owner, this, Priority);
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Reset navigation on resume (will re-navigate)
        if (_currentPhase == Phase.Navigating)
        {
            _navigationActivity = null;
        }
    }
}
