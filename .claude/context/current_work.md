# Current Work: Daily Rhythm Simulation

## Status: Day/Night Cycle Complete, Activities Next

## Goal
Make the village simulation interesting enough to watch by adding:
1. Day/night cycle (visual and behavioral) ← DONE
2. Activities system for richer behaviors (sleep, work, socialize)
3. Daily rhythm where entities respond to time of day

## What Was Built

### Day/Night Cycle (Complete)
- **GameTime.cs** - Seasonal day/night calculations:
  - `DayPhaseType` enum (Dawn, Day, Dusk, Night)
  - Seasonal variation: Spring/Autumn (5 day/7 night), Summer (6/6), Winter (4/8)
  - `DaylightLevel` (0.1-1.0) with smooth transitions
  - Helper properties: `IsDaytime`, `IsNighttime`, `IsDark`, `HasSunlight`
- **DayNightCycle.cs** - Visual modulation:
  - CanvasModulate-based world tinting
  - Smooth color transitions between phases
  - Dawn (warm orange), Day (white), Dusk (warm sunset), Night (cool blue)
- **Wiki updated** - Game-time-and-Calendar.md documents the cycle

### Core Infrastructure (Complete)
- **Activity base class** (`/entities/activities/Activity.cs`)
  - ActivityState enum (Running, Completed, Failed)
  - DisplayName, Priority, Initialize(), GetNextAction(), Cleanup()

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

## Architecture Summary

### Three Layers
| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE | VillagerTrait finds food source |
| **Activities** | EXECUTE | EatActivity handles go-to + consume |
| **Actions** | ATOMIC | MoveAlongPathAction, IdleAction |

### Key Decisions
1. Activities separate from traits (threading safety)
2. ActivityState is source of truth (not null checks)
3. Priority-based implicit interruption
4. Internal composition (EatActivity uses GoToBuildingActivity)
5. Commands will own activities (TODO)

## Files Created/Modified

### New Files
- `/entities/activities/Activity.cs`
- `/entities/activities/GoToLocationActivity.cs`
- `/entities/activities/GoToBuildingActivity.cs`
- `/entities/activities/EatActivity.cs`
- `/entities/activities/CLAUDE.md`
- `/entities/actions/StartActivityAction.cs`

### Modified Files
- `/entities/Being.cs` - Activity integration
- `/entities/traits/ConsumptionBehaviorTrait.cs` - Uses EatActivity
- `/entities/traits/VillagerTrait.cs` - Updated constructor
- `/entities/traits/ZombieTrait.cs` - Updated constructor
- `/core/ui/dialogue/commands/MoveToCommand.cs` - Added TODO
- `/core/ui/dialogue/commands/FollowCommand.cs` - Added TODO

## Next Steps

### Phase 4: Commands Integration (TODO)
- [ ] Update MoveToCommand to use GoToLocationActivity
- [ ] Commands own activities, poll state for completion

### Phase 5: New Behaviors (TODO)
- [ ] SleepActivity with day/night integration
- [ ] WorkActivity for farmers
- [ ] SocializeActivity for villager interactions

### Phase 6: GoToEntityActivity (TODO)
- [ ] Implement with proper BDI behavior
- [ ] Use perception/memory for target location
- [ ] Handle target movement and search

## Testing Needed
- Run game and verify villagers eat correctly using new Activity system
- Check that EatActivity navigates to farms and restores hunger
- Verify no threading issues during Think() cycle

## Documentation
- See `/entities/activities/CLAUDE.md` for full Activity system docs
- Future GoToEntityActivity design documented there
- Future consumption variants (vampire feeding, etc.) documented there
