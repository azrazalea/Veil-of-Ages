# Current Work: Resource Economy & Village Simulation

## Status: Activity Sub-Activity Pattern Fixed, Ready for Testing

## Recently Completed (January 2026)

### Activity Sub-Activity Pattern Fix - COMPLETE

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

If the sub-activity returns `null` but state changed to Completed, the helper detects this and returns `Completed`. If it's in a strange state (Running but null action), it returns an `IdleAction` to "hold the slot".

#### Files Modified
- `/entities/activities/Activity.cs` - Added `SubActivityResult` enum and `RunSubActivity()` helper
- `/entities/activities/ConsumeItemActivity.cs` - Refactored to use helper
- `/entities/activities/ProcessReactionActivity.cs` - Refactored to use helper
- `/entities/activities/WorkFieldActivity.cs` - Refactored to use helper
- `/entities/activities/GoToBuildingActivity.cs` - Removed verbose debug logging

### Other Fixes This Session

#### Building.GetFacilities() Bug Fix
- `GetFacilities()` was returning `storage?.Facilities` (empty) instead of the building-level facilities
- Fixed to return `_facilityPositions.Keys` which is populated from the template's `Facilities` array

#### Debug Logging Cleanup
- Removed verbose pathfinding debug logs (GO_TO_BUILDING, GetDebugSummary calls)
- Removed THINK logs from Being.cs
- Removed verbose BAKER and CONSUME logs
- Kept only essential logs (like "Starting reaction" with default 100-tick interval)

---

## Resource System - COMPLETE (Previous Session)

### Production Chain Flow
1. Farmer works at farm → produces wheat → brings harvest home → deposits to home storage
2. Baker checks home storage → mills wheat to flour → bakes flour to bread
3. Villager gets hungry → checks inventory → checks home storage → eats bread
4. Zombie gets hungry → goes to graveyard → eats corpse from storage

### Key Components
- **Items**: wheat, flour, bread, corpse (`/entities/items/`, `/resources/items/`)
- **Reactions**: mill_wheat, bake_bread (`/entities/reactions/`, `/resources/reactions/`)
- **Storage**: StorageTrait (buildings), InventoryTrait (beings)
- **Activities**: WorkFieldActivity, ProcessReactionActivity, ConsumeItemActivity
- **Job Traits**: FarmerJobTrait, BakerJobTrait

---

## Next Steps: Memory System for Storage Awareness

### Problem
Villagers need to remember what they've seen in storage areas. Currently they have no memory of storage contents - they check each time they need something. This is inefficient and unrealistic.

### Goal
Implement short-term memory for beings to remember storage contents they've recently observed.

### Design Ideas
- **BeingTrait already has `_memory` dictionary** - currently stores entity positions with timestamps
- Extend memory to store storage snapshots: `StorageMemory` with item counts and timestamp
- When a being accesses a storage container, update their memory of its contents
- Memory decays over time (e.g., 1000 ticks = ~2 minutes game time)
- Beings use memory to decide WHERE to look for items, not just IF items exist
- Example: Farmer remembers "home storage had 5 wheat last time I checked"

### Implementation Approach
1. Create `StorageMemoryEntry` record: building reference, item type → quantity map, timestamp
2. Add `_storageMemory` dictionary to BeingTrait keyed by building
3. When accessing storage (ConsumeItemActivity, ProcessReactionActivity), update memory
4. Add `GetRememberedItemCount(building, itemId)` helper
5. Consumption/production traits can use memory to make smarter decisions
6. Memory cleanup in Think() loop (remove stale entries)

---

## Architecture Summary

### Three Layers
| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE | VillagerTrait chooses to sleep, eat, or work |
| **Activities** | EXECUTE | SleepActivity, WorkFieldActivity, ConsumeItemActivity |
| **Actions** | ATOMIC | MoveAlongPathAction, IdleAction |

### Sub-Activity Pattern (NEW)
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

## Testing Checklist
- [x] Farmer produces wheat and brings it home
- [x] Baker mills wheat to flour (after facility fix)
- [x] Baker bakes flour to bread
- [x] Villager eats bread when hungry (after sub-activity fix)
- [x] Activities don't get overwritten when navigation completes immediately
- [ ] Zombie eats corpse from graveyard
- [ ] Production chain sustains village over time
