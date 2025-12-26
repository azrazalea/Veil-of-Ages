# Current Work: Daily Rhythm Simulation

## Status: Sleep Schedule Complete, Phase 2 In Progress

## Goal
Make the village simulation interesting enough to watch by adding:
1. Day/night cycle (visual and behavioral) ← DONE
2. Activities system for richer behaviors ← DONE
3. Daily rhythm where entities respond to time of day ← SLEEP DONE

## What Was Built

### Day/Night Cycle (Complete)
- **GameTime.cs** - Seasonal day/night calculations:
  - `DayPhaseType` enum (Dawn, Day, Dusk, Night)
  - Seasonal variation: Spring/Autumn (5 day/7 night), Summer (6/6), Winter (4/8)
  - `DaylightLevel` (0.1-1.0) with smooth transitions
  - Helper properties: `IsDaytime`, `IsNighttime`, `IsDark`, `HasSunlight`
  - `FromTicks(ulong)` static helper for cleaner tick-to-time conversion
- **DayNightCycle.cs** - Visual modulation:
  - CanvasModulate-based world tinting
  - Smooth color transitions between phases
  - Dawn (warm orange), Day (white), Dusk (warm sunset), Night (cool blue)
- **Wiki updated** - Game-time-and-Calendar.md documents the cycle

### Core Infrastructure (Complete)
- **Activity base class** (`/entities/activities/Activity.cs`)
  - ActivityState enum (Running, Completed, Failed)
  - DisplayName, Priority, Initialize(), GetNextAction(), Cleanup()
  - `NeedDecayMultipliers` dictionary for per-need decay rate modifiers
  - `GetNeedDecayMultiplier(needId)` returns multiplier or default 1.0

- **Being integration** (`/entities/Being.cs`)
  - `_currentActivity` field
  - Activity processing in Think() with state checking
  - `GetCurrentActivity()` and `SetCurrentActivity()` methods

- **StartActivityAction** (`/entities/actions/StartActivityAction.cs`)
  - Thread-safe way for traits to start activities

### Movement Activities (Complete)
- **GoToLocationActivity** - Pathfind to a grid position
- **GoToBuildingActivity** - Pathfind to a building (adjacent position)
- **GoToEntityActivity** - DEFERRED (needs BDI/perception integration, documented in CLAUDE.md)

### Consumption Activity (Complete)
- **EatActivity** - Uses GoToBuildingActivity internally, then consumes for duration
- **ConsumptionBehaviorTrait** - Migrated to use EatActivity instead of internal state machine
  - Priority-based hunger: critical hunger (-1) interrupts sleep, low hunger (1) doesn't

### Sleep Activity (Complete - December 2025)
- **SleepActivity** (`/entities/activities/SleepActivity.cs`)
  - Villagers sleep during Night/Dusk phases
  - Auto-wakes at Dawn by calling Complete()
  - Reduces hunger decay to 25% via NeedDecayMultipliers
  - Priority 0 (between critical hunger -1 and low hunger 1)
- **VillagerTrait** - Added Sleeping state and schedule logic
  - Checks GameTime.CurrentDayPhase for Night/Dusk
  - Starts SleepActivity when at home during sleep hours
  - Transitions out of Sleeping state when activity completes
- **BeingNeedsSystem** - Queries current activity for per-need decay multipliers
- **Need.cs** - Decay() now accepts multiplier parameter

## Architecture Summary

### Three Layers
| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE | VillagerTrait chooses to sleep or eat |
| **Activities** | EXECUTE | SleepActivity, EatActivity handle multi-step behavior |
| **Actions** | ATOMIC | MoveAlongPathAction, IdleAction |

### Priority System
| Behavior | Priority | Wins Against |
|----------|----------|--------------|
| Critical hunger | -1 | Everything |
| Sleep | 0 | Low hunger, wandering |
| Low hunger | 1 | Nothing special |
| Wandering | 1 | Nothing special |

### Key Decisions
1. Activities separate from traits (threading safety)
2. ActivityState is source of truth (not null checks)
3. Priority-based implicit interruption (lower number = higher priority)
4. Internal composition (EatActivity uses GoToBuildingActivity)
5. Activity-based need decay multipliers (extensible per-activity effects)
6. Commands will own activities (TODO)

## Files Created/Modified

### New Files
- `/entities/activities/Activity.cs`
- `/entities/activities/GoToLocationActivity.cs`
- `/entities/activities/GoToBuildingActivity.cs`
- `/entities/activities/EatActivity.cs`
- `/entities/activities/SleepActivity.cs`
- `/entities/activities/CLAUDE.md`
- `/entities/actions/StartActivityAction.cs`

### Modified Files
- `/entities/Being.cs` - Activity integration
- `/entities/traits/ConsumptionBehaviorTrait.cs` - Uses EatActivity, priority logic
- `/entities/traits/VillagerTrait.cs` - Sleeping state and schedule logic
- `/entities/being_services/BeingNeedsSystem.cs` - Activity-based decay multipliers
- `/entities/needs/Need.cs` - Decay accepts multiplier
- `/core/lib/GameTime.cs` - Added FromTicks() helper
- `/core/GameController.cs` - Time scale limit raised to 25x
- `/core/ui/dialogue/commands/MoveToCommand.cs` - Added TODO
- `/core/ui/dialogue/commands/FollowCommand.cs` - Added TODO

## Next Steps

### Phase 2 Continued: Daily Life (TODO)
- [ ] Add energy/rest need that SleepActivity restores
- [ ] WorkActivity for farmers (tend crops during day)
- [ ] Work schedules and job-based routines
- [ ] Recreation and entertainment activities

### Commands Integration (TODO)
- [ ] Update MoveToCommand to use GoToLocationActivity
- [ ] Commands own activities, poll state for completion

### GoToEntityActivity (TODO)
- [ ] Implement with proper BDI behavior
- [ ] Use perception/memory for target location
- [ ] Handle target movement and search

## Testing Verified
- Villagers sleep at night/dusk, wake at dawn
- Low hunger doesn't interrupt sleep
- Critical hunger interrupts sleep
- Sleep reduces hunger decay to 25%
- 25x time scale works for fast-forward testing

## Documentation
- See `/entities/activities/CLAUDE.md` for full Activity system docs
- Entity AI roadmap in `/.claude/context/entity_ai_improvement_plan.md`
