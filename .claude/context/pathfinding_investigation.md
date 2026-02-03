# Pathfinding System Investigation - Feb 2026

## CORE AXIOMS (MUST FOLLOW)

### 1. No God Knowledge
Entities can ONLY know about things they can perceive/see. They CANNOT:
- Check if destinations are occupied by entities they can't see
- Have pathfinding influenced by entities outside perception range
- Use `IsCellWalkable()` or `EntitiesGridSystem.IsCellOccupied()` for path decisions unless filtering by perception

Entities CAN:
- React to entities they encounter during movement (blocking response)
- Path around entities they can currently perceive/see
- Know terrain/building layout (village residents know village)

### 2. Pathfinding MUST Happen on Think Thread
Complex logic including pathfinding calculations must happen during `Think()` on background threads, NOT during action execution on main thread. This is for performance.

Current suspected bug: PathFinder.CalculatePathForCurrentGoal is called lazily from TryFollowPath, which may execute on main thread during action execution. NEEDS VERIFICATION.

### 3. A* Grid Does Not Include Beings (By Design)
The A* grid only marks `IBlocksPathfinding` entities (buildings) as solid. Beings are dynamic and handled via:
- Blocking response when movement fails
- Perception-aware path recalculation (NOT YET IMPLEMENTED)

### 4. Recalculation is for THROTTLING, Not Limiting
The recalculation counter/cooldown exists to prevent constant recalculation, NOT to limit retry attempts. Entities should keep trying to reach goals, just not spam path calculations every tick.

### 5. Periodic Perception-Based Recalculation
Every N steps/ticks, entity should recalculate path with current perception. This enables:
- "Oh there's a new entity, path around it" behavior
- WITHOUT recalculating every time entity enters/leaves perception

### 6. "Ask to Move" Should Be Last Resort
Asking another entity to move should be weighted MEDIUM-HEAVY:
- If can path around in ~5 steps, do that instead
- If no reasonable path around, try to walk through
- Trust the bumping/blocking system to handle collision
- Don't ask someone to move when you could easily go around

---

## CONFIRMED BUGS

### Bug 1: God Knowledge in GetWalkableInteriorPositions
**File:** `entities/building/Building.cs` line 655
**Problem:** Uses `GridArea.IsCellWalkable()` which checks ALL entity occupancy
```csharp
if (GridArea.IsCellWalkable(absolutePos) && ...)
```
**Impact:** Entities "know" about other entities they can't see
**Fix:** Remove entity check, only check terrain walkability

### Bug 2: God Knowledge in Facility Path Calculation
**File:** `core/lib/Pathfinder.cs` line 937
**Problem:** Uses `IsCellWalkable()` to check occupancy for facility candidates
```csharp
bool isOccupied = !gridArea.IsCellWalkable(adjacentPos);
```
**Impact:** Entities avoid positions occupied by entities they can't see
**Fix:** Remove this check OR filter by perception

### Bug 3: Recalculation Counter Never Resets on Progress
**File:** `core/lib/Pathfinder.cs` lines 496-503
**Problem:** `_recalculationAttempts` only resets when close to goal, not when making progress
**Fix:** Reset when `PathIndex` advances in `TryFollowPath`
```csharp
if (moveSuccessful)
{
    PathIndex++;
    _recalculationAttempts = 0;  // ADD THIS
}
```

### Bug 4: Stuck Counter Doesn't Reset on Valid Path
**File:** `entities/activities/GoToBuildingActivity.cs` lines 95-110
**Problem:** `_stuckTicks` only resets when in queue, not when path is valid
**Fix:**
```csharp
if (!hasValidPath && !_owner.IsInQueue)
{
    _stuckTicks++;
}
else
{
    _stuckTicks = 0;  // Reset on ANY success
}
```

### Bug 5: Empty Path When Already at Target
**File:** `core/lib/Pathfinder.cs` line 634
**Problem:** Returns true but `CurrentPath` empty, causing `HasValidPath()` to fail
**Fix:**
```csharp
if (startPos == _targetPosition)
{
    CurrentPath = [startPos];
    PathIndex = 0;
    _pathNeedsCalculation = false;
    return true;
}
```

### Bug 6 (SUSPECTED): Pathfinding on Wrong Thread
**NEEDS VERIFICATION**
**Problem:** `CalculatePathForCurrentGoal` may be called during action execution (main thread) rather than during Think (background thread)
**Impact:** Performance - pathfinding should be parallelized across entities
**Fix:** Restructure flow:
1. Think proposes path action (no calculation yet)
2. Orchestrator accepts action, sets activity/pathfinder goal
3. NEXT Think: First step attempt triggers path calculation ON THINK THREAD
4. Recalculations also happen in Think via special action

---

## MISSING FEATURE: Perception-Aware Pathfinding

### What Should Happen
When entity calculates/recalculates path:
1. Get entity's current perception data
2. Clone base A* grid
3. Mark perceived entities as solid in cloned grid
4. Calculate path using perception-modified grid
5. Entity paths around what it can SEE

### Infrastructure Already Exists
- `PathFinder.CreatePathfindingGrid()` already clones grids for non-villagers
- `PathFinder.CloneAStarGrid()` copies all solid/weight states
- Perception system provides entity visibility data

### What's Missing
- Passing perception data to PathFinder
- Marking visible entities as solid in cloned grid
- Integration between perception and path calculation

---

## DESIGN DECISIONS

### Blocking Response System (KEEP)
When movement fails due to entity:
1. `MovementController` stores `_lastBlockingEntity`
2. `Think()` calls `ConsumeBlockingEntity()`
3. Traits provide `GetBlockingResponse()`
4. Options: RequestMove, step-aside, queue, report stuck

This respects no-god-knowledge - only reacts to entities encountered.

### Village Residents vs Non-Residents
- Village residents use base A* grid (they know village layout)
- Non-residents get perception-limited grid (fog of war at border)
- BOTH should mark perceived entities as obstacles

---

## TODO LIST

### Phase 1: Fix Counter Bugs (Low Risk)
- [ ] Reset `_recalculationAttempts` when PathIndex advances
- [ ] Reset `_stuckTicks` when path is valid
- [ ] Handle "already at target" with single-element path

### Phase 2: Remove God Knowledge (Medium Risk)
- [ ] Fix `GetWalkableInteriorPositions` - remove entity check
- [ ] Fix facility path calculation - remove `IsCellWalkable` occupancy check
- [ ] Audit all pathfinding code for `IsCellWalkable`/entity occupancy checks

### Phase 3: Verify Threading (IMPORTANT)
- [ ] Trace where `CalculatePathForCurrentGoal` is called from
- [ ] Verify if it runs on think thread or main thread
- [ ] If main thread, restructure flow as described above

### Phase 4: Perception-Aware Pathfinding
- [ ] Pass perception data to PathFinder
- [ ] Modify `CreatePathfindingGrid` to mark perceived entities as solid
- [ ] Ensure recalculations use fresh perception data

### Phase 5: Proactive Periodic Recalculation (Before "Done")
- [ ] Every N steps or ticks, recalculate path using current perception
- [ ] This handles "new entity appeared in my way" without constant recalc
- [ ] The recalculation counter/cooldown is for THROTTLING, not limiting retries
- [ ] Goal: Realistic "oh there's someone, path around" behavior

### Phase 6: "Ask to Move" Weighting
- [ ] Asking someone to move should be weighted MEDIUM-HEAVY
- [ ] If entity can path around in ~5 steps, do that instead of asking
- [ ] If no reasonable path around, try to walk through (trust bumping system)
- [ ] Bumping system handles the actual interaction when paths collide

---

## KEY FILES

| File | Purpose |
|------|---------|
| `core/lib/Pathfinder.cs` | Main pathfinding logic |
| `entities/activities/GoToBuildingActivity.cs` | Building navigation activity |
| `entities/building/Building.cs` | GetWalkableInteriorPositions |
| `world/GridArea.cs` | IsCellWalkable, A* grid |
| `entities/Being.cs` | TryMoveToGridPosition, Think |
| `entities/being_services/MovementController.cs` | Movement execution |
| `entities/EntityThinkingSystem.cs` | Think thread coordination |
| `core/debug/CLAUDE.md` | Debug server documentation |

---

## CONSTANTS TO REVIEW

```
MAXRECALCULATIONATTEMPTS = 3   // Maybe too low?
RECALCULATIONCOOLDOWN = 5      // 5 ticks between recalc attempts
MAXSTUCKTICKS = 50             // 50 ticks before activity fails
```

User noted: "50 ticks really isn't that long in game time" - may need increase.
