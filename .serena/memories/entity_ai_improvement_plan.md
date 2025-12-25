# Entity AI Improvement Plan

## Current State Analysis

The current entity AI system has a solid foundation with:
- **Being.cs**: Core entity foundation with attributes, health, traits, perception
- **Traits System**: Modular behaviors (VillagerTrait, UndeadTrait, etc.)
- **Actions System**: Priority-based action execution
- **Needs System**: Basic hunger need with strategy pattern
- **Sensory System**: Perception, memory, line-of-sight

**Current Flow**: Needs → Traits → Actions

## Identified Gaps

1. **Missing coordination layer**: Traits must micromanage every action
2. **Limited daily life**: Only basic survival behaviors
3. **No social intelligence**: No relationships or social dynamics
4. **Simple psychology**: Only basic needs, no emotions or complex motivations

## Development Phases (In Order)

### **Phase 1: Activities System**
**Priority**: Highest - Essential coordination layer for everything else

**Key Insight**: Activities are temporary traits that coordinate complex behaviors. They're interruptible and can be started by actual traits.

**Implementation**:
- Activity base class (temporary trait with lifecycle management)
- Activity interruption system (priority-based pausing/resuming) 
- Common activities: GetFood, Socialize, Sleep, Work, Explore
- Player integration (commands can trigger activities)
- Progress tracking and completion status

**Architecture Change**: Needs → Traits → **Activities** → Actions

**Benefits**: 
- Traits become decision-makers, not micromanagers
- Shared logic between player commands and NPC behaviors
- Foundation for complex behaviors without trait complexity

### **Phase 2: Expanded Daily Life**
**Priority**: High - Creates engaging village simulation

**Implementation**:
- Sleep cycles and day/night rhythms
- Work schedules and job-based routines
- Seasonal activities and weather responses
- Home life activities (cooking, cleaning, family)
- Recreation and entertainment systems

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

- **Phase 1**: NPCs can perform multi-step behaviors (like "get food from farm")
- **Phase 2**: Village has realistic daily rhythms and work patterns
- **Phase 3**: Emergent social events and relationship dynamics
- **Phase 4**: NPCs display believable emotional responses and long-term goals

## Next Steps

Start with Phase 1 - Activities System implementation. This provides the foundation that all other improvements will build upon.