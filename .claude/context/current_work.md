# Current Work: Resource Economy & Village Simulation

## Status: Memory System Complete, Ready for Next Feature

## Recently Completed (January 2026)

### Memory System Implementation (January 2026) - COMPLETE

Implemented a comprehensive memory system so entities only know what they've personally observed. No more omniscient storage knowledge.

#### Golden Rule
Entities ONLY know what's in:
- Their inventory (immediate)
- Their personal memory (observed storage, decays over time)

#### SharedKnowledge System
- Base class for composable, read-only knowledge shared by reference
- Village creates SharedKnowledge and shares with all residents
- Buildings registered during world generation
- Thread-safe queries (return snapshot copies)
- Future: timestamp-based propagation for realistic news spreading

**Key Methods:**
- `GetBuildingsOfType(type)` - Find all buildings of a type
- `GetBuildingByName(name)` - Find building by unique name

#### PersonalMemory System
- Storage observations with 28,000 tick (~2 game day) expiration
- Entity sightings with 7,000 tick (~12 game hour) expiration
- Location memories (extensible)
- Cleanup runs every 32 ticks

**Key Methods:**
- `ObserveStorage(building, items)` - Record what's in a storage
- `GetRememberedStorageContents(building)` - Get remembered contents
- `GetStoragesWithItem(itemId)` - Find buildings remembered to have item

#### Being Storage API
Auto-observing wrapper methods that update memory when used:
- `AccessStorage(building)` - Get storage and observe contents
- `TakeFromStorage(building, itemId, quantity)` - Take items
- `PutInStorage(building, itemId, quantity)` - Store items
- `FindItemLocations(itemId)` - Check inventory + memory
- `HasIdeaWhereToFind(itemId)` - Quick check if any source known

SharedKnowledge helpers:
- `TryFindBuildingOfType(type, out building)` - Find building from shared knowledge
- `FindNearestBuildingOfType(type)` - Find closest building

#### Trait Updates
All traits now use the memory-based storage API:
- **ItemConsumptionBehaviorTrait**: Memory-based food source finding
- **ConsumeItemActivity**: Uses storage wrappers
- **WorkFieldActivity**: Farmer remembers farm contents
- **BakerJobTrait**: Uses storage wrappers for ingredients

#### CheckHomeStorageActivity
New activity triggered when hungry but no food in memory:
1. Travel to home building
2. Observe storage contents (updates memory)
3. Complete - parent trait can now find food in memory

#### Key Files
- `/entities/memory/SharedKnowledge.cs` - Shared knowledge base class
- `/entities/memory/PersonalMemory.cs` - Per-entity memory with decay
- `/world/Village.cs` - Creates and distributes SharedKnowledge
- `/entities/Being.cs` - Storage API wrappers
- `/entities/activities/CheckHomeStorageActivity.cs` - Memory refresh activity
- `/entities/traits/ItemConsumptionBehaviorTrait.cs` - Updated for memory

---

### Building Alignment & Navigation Fixes (ee52e9d) - COMPLETE

Fixed building alignment and doorway navigation issues:

#### Building Alignment Fix
- Buildings now properly align with the tile grid
- Fixed visual alignment issues that caused buildings to appear offset

#### Doorway Blocking Fix
- Entities no longer get stuck when navigating through doorways
- Improved pathfinding around building entrances

#### Trait Navigation Refactor
- Refactored navigation logic within traits for better consistency
- Improved how traits handle movement to and through buildings

### Activity Sub-Activity Pattern Fix (dea245c) - COMPLETE

Fixed a critical bug where activities that composed other activities (like ConsumeItemActivity using GoToBuildingActivity) could be overwritten by other traits when the sub-activity completed immediately.

#### The Problem
When an entity was already at their destination, `GoToBuildingActivity.GetNextAction()` would:
1. Find the goal already reached
2. Call `Complete()` and return `null`
3. The parent activity would return `null` (no action)
4. Other traits (like BakerJobTrait) would submit their actions
5. A `StartActivityAction` from another trait would overwrite the current activity

This caused villagers to get stuck in loops, constantly restarting eating activities that got interrupted by work activities.

#### The Solution: `RunSubActivity()` Helper

Added a helper method to `Activity.cs` that safely runs sub-activities:

```csharp
protected (SubActivityResult result, EntityAction? action) RunSubActivity(
    Activity subActivity,
    Vector2I position,
    Perception perception)
```

Returns one of:
- `SubActivityResult.Continue` - Sub-activity running, use the returned action
- `SubActivityResult.Completed` - Sub-activity finished, proceed to next phase
- `SubActivityResult.Failed` - Sub-activity failed, handle the error

#### Files Modified
- `/entities/activities/Activity.cs` - Added `SubActivityResult` enum and `RunSubActivity()` helper
- `/entities/activities/ConsumeItemActivity.cs` - Refactored to use helper
- `/entities/activities/ProcessReactionActivity.cs` - Refactored to use helper
- `/entities/activities/WorkFieldActivity.cs` - Refactored to use helper
- `/entities/activities/GoToBuildingActivity.cs` - Removed verbose debug logging

### Per-Entity Debug Logging & Home Assignment Fix (921e19f) - COMPLETE

#### Per-Entity Debug Logging
- Added selective debug logging that can be enabled per-entity
- Helps isolate issues with specific villagers without flooding logs

#### Home Assignment Fix
- Fixed issues where entities were not properly assigned to homes
- Ensures villagers have a home building for sleeping and storage

### Resource Economy System (a9e275d) - COMPLETE

Implemented a complete resource economy with items, storage, and production chains.

### Production Chain Flow
1. Farmer works at farm -> produces wheat -> brings harvest home -> deposits to home storage
2. Baker checks home storage -> mills wheat to flour -> bakes flour to bread
3. Villager gets hungry -> checks inventory -> checks home storage -> eats bread
4. Zombie gets hungry -> goes to graveyard -> eats corpse from storage

### Key Components
- **Items**: wheat, flour, bread, corpse (`/entities/items/`, `/resources/items/`)
- **Reactions**: mill_wheat, bake_bread (`/entities/reactions/`, `/resources/reactions/`)
- **Storage**: StorageTrait (buildings), InventoryTrait (beings)
- **Activities**: WorkFieldActivity, ProcessReactionActivity, ConsumeItemActivity
- **Job Traits**: FarmerJobTrait, BakerJobTrait

### Energy Need & Work-Sleep Loop (16a0a4f) - COMPLETE

Added energy as a need that beings must satisfy through sleep, creating a day/night gameplay loop.

### Farmer Job System (7cd1ac8) - COMPLETE

Implemented the farmer job with work activity for field cultivation.

### Strict Analyzers & Code Quality (f84e0ac, f974a04) - COMPLETE

Enabled strict C# analyzers and fixed all warnings and errors for better code quality.

---

## Architecture Summary

### Three Layers
| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE | VillagerTrait chooses to sleep, eat, or work |
| **Activities** | EXECUTE | SleepActivity, WorkFieldActivity, ConsumeItemActivity |
| **Actions** | ATOMIC | MoveAlongPathAction, IdleAction |

### Sub-Activity Pattern
When activities compose other activities, use `RunSubActivity()`:
```csharp
var (result, action) = RunSubActivity(_goToPhase, position, perception);
switch (result)
{
    case SubActivityResult.Failed:
        Fail();
        return null;
    case SubActivityResult.Continue:
        return action;
    case SubActivityResult.Completed:
        break; // Fall through to next phase
}
// Handle arrival...
```

---

## Next Steps: Planned Features

### Memory System Enhancements (Priority: Low)
- **Timestamp-based propagation**: News spreads through social interactions rather than instant sharing
- **Entity memory improvements**: Remember where specific entities were last seen
- **Memory confidence levels**: Recent memories more reliable than old ones

### Other Potential Next Steps
- **Zombie Testing**: Verify zombie corpse consumption from graveyard works correctly
- **Long-term Village Sustainability**: Test that production chain sustains village over extended play
- **Additional Needs**: Consider implementing social needs, entertainment, etc.
- **More Job Types**: Additional professions beyond farmer and baker
- **Trading System**: Beings can exchange items with each other

---

## Testing Checklist
- [x] Farmer produces wheat and brings it home
- [x] Baker mills wheat to flour (after facility fix)
- [x] Baker bakes flour to bread
- [x] Villager eats bread when hungry (after sub-activity fix)
- [x] Activities don't get overwritten when navigation completes immediately
- [x] Building alignment and doorway navigation work correctly
- [x] Per-entity debug logging works
- [x] Home assignment works properly
- [x] Memory system tracks storage observations
- [x] Beings use memory to find food sources
- [x] CheckHomeStorageActivity refreshes memory when needed
- [x] Storage API wrappers auto-update memory
- [ ] Zombie eats corpse from graveyard
- [ ] Production chain sustains village over extended time
- [ ] Memory decay works correctly over time

---

## Older Completed Work

### Day/Night Cycle & UI (eaf2359, d740750) - December 2025
- Implemented day/night cycle with visual changes
- Added UI elements for time display and controls

### Villager Sleep Schedule (3b333da, cfd6c43) - December 2025
- Implemented villagers sleeping at night with priority-based activity interruption
- Added GameTime.FromTicks helper and increased time scale limit

### Activities System (8bbf334) - December 2025
- Added activities as the execution layer between traits and actions
- Added code formatting tools

### Building System Refactor (95638bc) - December 2025
- Major refactor of building code
- Implemented graveyard building for undead sustenance
