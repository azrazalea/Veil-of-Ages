# Entity AI Improvement Plan

## Current State Analysis

The entity AI system has evolved significantly:
- **Being.cs**: Core entity foundation with attributes, health, traits, perception
- **Traits System**: Modular behaviors (VillagerTrait, UndeadTrait, etc.)
- **Actions System**: Priority-based action execution
- **Activities System**: Multi-step behavior coordination (EatActivity, SleepActivity, etc.)
- **Needs System**: Hunger need with strategy pattern and activity-based decay multipliers
- **Sensory System**: Perception, memory, line-of-sight

**Current Flow**: Needs → Traits → **Activities** → Actions

## Development Phases (In Order)

### **Phase 1: Activities System** ✅ COMPLETE
**Status**: Implemented December 2025

**What was built**:
- `Activity` base class with lifecycle (Running/Completed/Failed states)
- Priority-based action selection (lower number = higher priority)
- `StartActivityAction` for traits to delegate to activities
- `EatActivity` - Navigate to food source, consume, restore hunger
- `SleepActivity` - Idle during night, auto-wake at dawn
- Activity-based need decay multipliers (`NeedDecayMultipliers` dictionary)
- `IdleAction` for activities that just wait

**Architecture**: Traits DECIDE what to do → Activities EXECUTE multi-step behaviors → Actions are ATOMIC

**Key Files**:
- `entities/activities/Activity.cs` - Base class
- `entities/activities/EatActivity.cs` - Food consumption
- `entities/activities/SleepActivity.cs` - Sleep behavior
- `entities/actions/StartActivityAction.cs` - Bridge from traits

### **Phase 2: Expanded Daily Life** 🔄 IN PROGRESS
**Priority**: High - Creates engaging village simulation

**Completed**:
- ✅ Sleep cycles and day/night rhythms (villagers sleep at night/dusk, wake at dawn)
- ✅ Activity-based need modifiers (sleep reduces hunger decay to 25%)
- ✅ Priority-based interruption (critical hunger interrupts sleep, low hunger doesn't)
- ✅ Day/night visual cycle with smooth transitions
- ✅ Time/date HUD with speed controls (up to 25x)

**Remaining**:
- Work schedules and job-based routines
- Seasonal activities and weather responses
- Home life activities (cooking, cleaning, family)
- Recreation and entertainment systems
- Energy/rest need that sleep restores

**Benefits**:
- Village feels alive with realistic daily patterns
- Natural contexts for player interaction
- Foundation for social systems

### **Phase 3: Social & Interaction Systems**
**Priority**: Medium-High - Creates emergent village dynamics

**Implementation**:
- Relationship system (trust, friendship, reputation)
- Social activities (gossip, trade, group gatherings)
- Dynamic dialogue system (context-aware conversations)
- Faction system (village politics and conflicts)
- Social needs (loneliness, belonging, status)

**Benefits**:
- Emergent storylines from social interactions
- Player integration into village society
- Dynamic village politics and conflicts

### **Phase 4: Expanded Needs & Emotions**
**Priority**: Medium - Adds psychological depth

**Implementation**:
- Psychological needs (safety, belonging, achievement)
- Emotional states affecting decision-making
- Environmental needs (comfort, cleanliness, beauty)
- Need interactions and conflicts
- Long-term goals and life ambitions

**Benefits**:
- Richer, more believable personalities
- Complex decision-making scenarios
- Deeper simulation depth

## Technical Notes

**Activities System Design**:
- Activities as temporary traits with start/pause/resume/complete lifecycle
- Priority-based interruption (higher priority needs can pause current activity)
- Trait delegation: VillagerTrait says "get food", Activity handles the execution
- Player command integration: Commands trigger activities, not low-level actions

**Integration Points**:
- Activities use existing action system for execution
- Activities report progress to traits and player UI
- Activities can be saved/loaded for game persistence
- Activities can chain together for complex behaviors

## Success Metrics

- **Phase 1**: ✅ NPCs can perform multi-step behaviors (like "get food from farm")
- **Phase 2**: 🔄 Village has realistic daily rhythms and work patterns
- **Phase 3**: Emergent social events and relationship dynamics
- **Phase 4**: NPCs display believable emotional responses and long-term goals

## Next Steps

Continue Phase 2 - Expanded Daily Life:
- Add energy/rest need that SleepActivity restores
- Implement work schedules (farmers tend crops during day, etc.)
- Add more activities that use the need decay multiplier system