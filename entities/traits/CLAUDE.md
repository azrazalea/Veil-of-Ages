# /entities/traits

## Purpose

This directory contains all trait implementations for Being entities. Traits are modular behavior components that define how entities act, react, and interact. The trait system follows a composition pattern where complex behaviors emerge from combining simpler traits.

## Files

### ItemConsumptionBehaviorTrait.cs
Trait that handles need satisfaction by consuming items from inventory or home storage.

**Features:**
- Checks inventory first, then personal memory for food locations
- Priority-based action generation (critical hunger interrupts sleep)
- Starts ConsumeItemActivity when food is available
- Starts CheckStorageActivity when hungry but no food memory (refreshes memory)
- Facility-based food source lookup via `GetFacilitiesByTag(_foodTag)` on SharedKnowledge

**Behavior Flow:**
1. Entity becomes hungry (need is low)
2. Check `HasFoodAvailable()`:
   - Has food in inventory? -> Start ConsumeItemActivity
   - Remembers food in a storage facility matching the food tag? -> Start ConsumeItemActivity (uses `GetFacilitiesByTag` on SharedKnowledge to find candidate facilities)
   - No memory of food? -> Start CheckStorageActivity (go home and observe storage)
3. After CheckStorageActivity completes, memory is updated
4. On next think cycle, if home had food, ConsumeItemActivity can now start

**Constructor Parameters:**
- `needId` - The need to satisfy (e.g., "hunger")
- `foodTag` - Tag to identify food items (e.g., "food", "zombie_food")
- `restoreAmount` - Amount to restore when eating (default 60)
- `consumptionDuration` - Ticks to spend eating (default 244)

**Home Resolution:**
Internally calls `HomeTrait?.Home` to get the home `Room`. Storage is accessed via the room's storage facility.

**Usage:**
```csharp
var consumptionTrait = new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",
    restoreAmount: 60f,
    consumptionDuration: 244
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

**Priority:** -1 relative to parent (runs before main behavior when hungry)

### InventoryTrait.cs
Personal item storage for beings (living and undead entities).

**Features:**
- Volume and weight-based capacity limits
- Over-capacity carry (Rimworld-style: empty inventory can hold 1 item regardless of weight/volume)
- Stack merging for identical items
- Item decay processing
- Encumbrance tracking
- Implements IStorageContainer interface

**Default Capacities:**
- Volume: 0.02 m3 (20 liters, roughly a small backpack)
- Weight: 15 kg (reasonable carry weight)
- Decay modifier: 1.0 (normal decay rate)

**Key Methods:**
- `AddItem(Item)` - Add item, auto-merges with existing stacks
- `RemoveItem(itemDefId, quantity)` - Remove and return items
- `HasItem(itemDefId, quantity)` - Check availability
- `FindItem(itemDefId)` / `FindItemByTag(tag)` - Locate items
- `GetEncumbranceLevel()` - Returns 0-1 based on most restrictive limit
- `ProcessDecay()` - Apply decay to all items, remove spoiled
- `IsOverCapacity` - Property indicating if carrying item that exceeds normal capacity limits

**Usage:**
```csharp
// Add trait to a being
being.SelfAsEntity().AddTraitToQueue<InventoryTrait>(0);

// Use inventory
var inventory = being.SelfAsEntity().GetTrait<InventoryTrait>();
if (inventory?.CanAdd(item) == true)
{
    inventory.AddItem(item);
}
```

### StorageTrait.cs
Storage trait for buildings and non-being entities.

**Features:**
- Configurable volume and weight capacity
- Decay rate modifier (for cold storage, etc.)
- Facility tracking for crafting requirements
- Stack merging for identical items
- Implements IStorageContainer interface

**Constructor Parameters:**
- `volumeCapacity` - Maximum volume in cubic meters
- `weightCapacity` - Maximum weight in kg, -1 for unlimited
- `decayRateModifier` - Decay modifier (0.5 = half decay, 2.0 = double)
- `facilities` - List of available facilities (e.g., "oven", "mill")

**Key Methods:**
- `AddItem(Item)` - Add item with capacity checking
- `RemoveItem(itemDefId, quantity)` - Remove and return items
- `HasItem(itemDefId, quantity)` - Check availability
- `FindItem(itemDefId)` / `FindItemByTag(tag)` - Locate items
- `HasFacility(facility)` - Check for crafting facility
- `ProcessDecay()` - Apply decay modifier to all items

**Usage:**
```csharp
// In Room/Facility setup
var storage = new StorageTrait(
    volumeCapacity: 10.0f,
    weightCapacity: -1,
    decayRateModifier: 0.5f,  // Cold storage
    facilities: ["oven", "counter"]
);
facility.SelfAsEntity().AddTraitToQueue(storage, 0);
```

### LivingTrait.cs
Base trait for living entities.

**Features:**
- Initializes hunger and energy needs in NeedsSystem
- Hunger: 75 initial, 0.02 decay, thresholds 15/40/90
- Energy: 100 initial, 0.008 decay, thresholds 20/40/80

Simple trait that adds needs - actual consumption behavior is handled by ItemConsumptionBehaviorTrait, energy is restored by SleepActivity.

### MindlessTrait.cs
Trait for non-sapient entities.

**Features:**
- Limits dialogue to non-complex commands
- Provides generic dialogue responses
- "Blank stare" initial dialogue
- Silent obedience responses
- Overrides blocking response to push instead of communicate
- Ignores MoveRequest events (doesn't understand communication)

**Blocking Response:**
Mindless beings push entities that block their path instead of asking politely:
```csharp
public override EntityAction? GetBlockingResponse(Being blockingEntity, Vector2I targetPosition)
{
    var pushDirection = (targetPosition - myPos).Sign();
    return new PushAction(_owner, this, blockingEntity, pushDirection, priority: 0);
}
```

**Event Handling:**
Mindless beings ignore MoveRequest events (they don't understand communication):
```csharp
public override bool HandleReceivedEvent(EntityEvent evt)
{
    if (evt.Type == EntityEventType.MoveRequest)
        return true; // Handled (by ignoring)
    return false; // Let default handling take over
}
```

**Command Filtering:**
```csharp
public override bool IsOptionAvailable(DialogueOption option)
{
    if (option.Command == null) return true;
    return !option.Command.IsComplex;
}
```

### UndeadTrait.cs
Base trait for all undead entities.

**Features:**
- Disables pain body system
- Disables living body systems (breathing, blood, digestion, senses)
- Provides idle action as default behavior

**Disabled Systems:**
- Pain, Breathing, BloodPumping, BloodFiltration, Digestion, Sight, Hearing

### UndeadBehaviorTrait.cs
Abstract base for undead with autonomous behavior.

**Features:**
- Common wandering behavior properties
- State timer management
- Helper for wandering within range
- Range checking utilities

**Properties:**
- `WanderProbability` - Chance to start wandering (default 0.2)
- `WanderRange` - Max distance from spawn (default 10.0)
- `IdleTime` - Ticks between decisions (default 10)

**Abstract Method:**
- `ProcessState(position, perception)` - Implemented by subclasses

### SkeletonTrait.cs
Territorial behavior for skeleton entities.

**Features:**
- Territory defense state machine
- Intruder detection and pursuit
- Bone rattle audio integration
- Last-known-position tracking
- Uses GoToLocationActivity for returning to spawn and pursuing intruders

**States:**
- `Idle` - Standing still, chance to wander
- `Wandering` - Moving within territory
- `Defending` - Pursuing intruder using GoToLocationActivity

**Navigation Pattern:**
- Uses `GoToLocationActivity` for position-based navigation (return to spawn, pursue intruder)
- Starts activity with `StartActivityAction`, then checks if activity is running and returns `null` to let activity handle navigation
- Pursuit uses BDI pattern: goes to believed position, not tracking entity directly

**Territory Parameters:**
- `TerritoryRange`: 12 tiles
- `DetectionRange`: 8 tiles
- `IntimidationTime`: 40 ticks

### ZombieTrait.cs
Hunger-driven behavior for zombie entities.

**Features:**
- Brain hunger need initialization
- ItemConsumptionBehaviorTrait composition for feeding
- Groan audio integration
- Wider wander range than skeletons
- Uses GoToLocationActivity for returning to spawn when wandering too far

**States:**
- `Idle` - Standing still, chance to groan and wander
- `Wandering` - Shambling movement, returns to spawn if too far

**Navigation Pattern:**
- Uses `GoToLocationActivity` for returning to spawn position
- Checks if activity is running via `_owner.GetCurrentActivity()` and returns `null` to let activity handle navigation

**Behavior Parameters:**
- `WanderProbability`: 0.3 (more active)
- `WanderRange`: 15 tiles (further range)

**Hunger Configuration:**
- Need: "Brain Hunger", 60 initial, 0.0015 decay
- Food tag: "zombie_food" (corpses)
- Consumption duration: 365 ticks (messy eaters)

### VillagerTrait.cs
Autonomous village life behavior (non-sleep daily routine).

**Features:**
- Uses SharedKnowledge for building/room awareness (via Village)
- State-based daily routine (sleep is handled by ScheduleTrait)
- Home-based food acquisition
- Uses GoToRoomActivity for visiting rooms (home, other rooms)
- Uses GoToLocationActivity for going to village square
- Defers to SleepActivity when detected (returns null)

**States:**
- `IdleAtHome` - At home position, may wander; uses GoToRoomActivity to navigate home
- `IdleAtSquare` - At village center, social time; uses GoToRoomActivity to navigate to well
- `VisitingBuilding` - At a specific room; uses GoToRoomActivity to navigate

**Navigation Pattern:**
- Uses `GoToRoomActivity` for room-based navigation (home, visiting rooms)
- Uses `GoToLocationActivity` for position-based navigation (village square)
- Pattern: start activity with `StartActivityAction`, then check if activity is running and return `null` to let activity handle navigation
- Example: `return new StartActivityAction(_owner, this, visitActivity, priority: 1);`
- Then check: `if (_owner.GetCurrentActivity() is GoToRoomActivity) return null;`

**Room Knowledge:**
Uses SharedKnowledge from Village to find rooms to visit. SharedKnowledge stores `RoomReference` entries rather than `BuildingReference`:
```csharp
var home = _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
var knownRooms = _owner.SharedKnowledge
    .SelectMany(k => k.GetAllRooms())
    .Where(r => r.IsValid && r.Room != home)
    .ToList();
```
Villagers receive SharedKnowledge when added as village residents via `Village.AddResident()`.

### JobTrait.cs (IMPORTANT - Base Class)
Abstract base class for all job traits that work at a room/facility during specific hours.

**Purpose:**
JobTrait **structurally enforces** the correct pattern for job traits:
- Traits DECIDE when to work (check hours, not busy, etc.)
- Activities EXECUTE the work (navigation, storage access, multi-step work)

The key design is that `SuggestAction()` is **sealed** - subclasses cannot override it.
Instead, they must implement `CreateWorkActivity()` to define the actual work.

**Why This Exists:**
This prevents the "baker bug" pattern where traits accessed storage directly before
the entity had arrived at the workplace. By sealing SuggestAction and forcing work
through CreateWorkActivity, we guarantee that:
1. Only time/status checks happen in the trait
2. All storage access happens in activities (which have navigation phases)

**Abstract Members:**
- `WorkActivityType` - Type of the work activity (for "already working" check)
- `CreateWorkActivity()` - Create and return the work activity

**Virtual Members (Override to Customize):**
- `WorkPhases` - Day phases when work happens (default: Dawn, Day)
- `GetWorkplace()` - Get the workplace room (default: `_workplace` field)
- `WorkplaceConfigKey` - Config key for workplace (default: "workplace")
- `DesiredResources` - IDesiredResources implementation (default: empty)
- `GetJobName()` - Human-readable name for debug logging

**Workplace Field:**
`_workplace` is `Room?` (not Building). TraitConfiguration resolves it via `GetRoom()` rather than `GetBuilding()`.

**Usage:**
```csharp
public class MyJobTrait : JobTrait
{
    protected override Type WorkActivityType => typeof(MyWorkActivity);

    protected override Activity? CreateWorkActivity()
    {
        return new MyWorkActivity(_workplace!, duration, priority: 0);
    }
}
```

### FarmerJobTrait.cs
Job trait for farmers who work at assigned farms during daytime.
**Inherits from JobTrait** - enforces the correct pattern.

**Features:**
- Assigned to a specific farm room on construction
- Starts WorkFieldActivity during Dawn/Day phases
- Returns null at night (VillagerTrait handles sleep)
- Context-aware dialogue based on time of day
- Deposits harvest to farmer's home storage

**JobTrait Overrides:**
- `WorkActivityType`: `WorkFieldActivity`
- `WorkplaceConfigKey`: "farm"
- `DesiredResources`: wheat (10)

**Constants:**
- `WORKDURATION`: 1500 ticks (~3.1 real minutes)

**Usage:**
```csharp
var farmerTrait = new FarmerJobTrait(assignedFarmRoom);
typedBeing.SelfAsEntity().AddTraitToQueue(farmerTrait, priority: -1);
```

**Priority:** -1 (runs before VillagerTrait at priority 1)

### BakerJobTrait.cs
Job trait for bakers who work at bakeries during daytime.
**Inherits from JobTrait** - enforces the correct pattern.

**Features:**
- Assigned to a specific workplace room (bakery)
- Starts BakingActivity during Dawn/Day phases
- Returns null at night (VillagerTrait handles sleep)
- Context-aware dialogue based on time of day
- Wheat source lookup uses `GetFacilitiesByTag("grain")` on SharedKnowledge (facility-level tag system)

**JobTrait Overrides:**
- `WorkActivityType`: `BakingActivity`
- `DesiredResources`: wheat (10), bread (5)

**Constants:**
- `WORKDURATION`: 400 ticks (~50 seconds real time)

**Usage:**
```csharp
var bakerTrait = new BakerJobTrait(assignedBakeryRoom);
typedBeing.SelfAsEntity().AddTraitToQueue(bakerTrait, priority: -1);
```

**Priority:** -1 (runs before VillagerTrait at priority 1)

### DistributorJobTrait.cs
Job trait for distributors who move resources between rooms/facilities.
**Inherits from JobTrait** - enforces the correct pattern.

**Features:**
- `_workplace` is `Room?` (not Building)
- Finds GranaryTrait via `_workplace.GetStorageFacility()?.SelfAsEntity().GetTrait<GranaryTrait>()`
- Distributes resources to other rooms during work phases

### HomeTrait.cs
Trait that provides home room reference for entities.

**Features:**
- Stores a `Room` reference internally (`_home`)
- `Home` property returns the home `Room?`
- `IsEntityAtHome()` checks if the entity's current position is inside the home room
- `SetHome(Room)` sets the home room directly and calls `Room.AddResident()` (not Building.AddResident)

**Note:** The `HomeBuilding` convenience property has been removed. Callers that previously used `HomeTrait?.HomeBuilding` must now use `HomeTrait?.Home` and resolve the building via `Room.Owner` if needed.

**Both `SetHome` overloads take `Room`** - there is no longer a `SetHome(Building)` overload.

**Resident Registration:**
HomeTrait calls `Room.AddResident(being)` directly when setting a home. This happens either in `SetHome()` (if `_owner` is already set) or deferred to `Initialize()` (if `Configure()` was called before `Initialize()`). Building.AddResident no longer exists -- all resident management is on Room.

### DormantUndeadTrait.cs
Trait for undead that remain dormant until animated.

**Features:**
- Uses `HomeTrait?.Home` (`Room?`) instead of the removed `HomeTrait?.HomeBuilding`
- Checks `home.IsDestroyed` instead of `GodotObject.IsInstanceValid(home)` for room validity

**Key Pattern:**
```csharp
var home = _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
if (home == null || home.IsDestroyed) return null;
```

### AutomationTrait.cs
Trait that allows toggling between automated and manual behavior.

**Features:**
- `IsAutomated` property - when false, trait SuggestAction() calls are suppressed in Being.Think()
- Exception: critical needs (<=20) force automated behavior even in manual mode
- NPC-compatible: any entity can have this trait, not just the player
- `Toggle()` method to switch modes
- `HasCriticalNeed()` checks if any need is at or below critical threshold
- `ShouldSuppressTraits()` returns true when in manual mode AND no critical needs

**Purpose:**
Player control over entity behavior. When manual mode is active, the entity only executes commands and doesn't act autonomously (unless needs become critical).

### NecromancyStudyJobTrait.cs
Job trait for necromancer's nighttime study of dark arts.
**Inherits from JobTrait** - enforces the correct pattern.

**Features:**
- Studies at nearest necromancy_altar during Night phase only
- Returns null during Dawn/Day/Dusk (other traits handle daytime behavior)
- Work order priority: if altar has active work order, creates WorkOnOrderActivity instead of StudyNecromancyActivity
- Won't start if energy is critical (allows sleep to take over)
- Dynamically finds necromancy_altar via `Being.FindFacilityOfType()` - no configured workplace

**JobTrait Overrides:**
- `WorkActivityType`: `StudyNecromancyActivity`
- `WorkPhases`: Night only
- `GetWorkplace()`: Returns altar's room (found dynamically)

**Constants:**
- `WORKDURATION`: 400 ticks (~50 seconds real time)

**Dialogue:**
- Night: "The veil between worlds grows thin at this hour... I must not be disturbed."
- Day: "The dark arts demand patience. Night will come soon enough."

### ScheduleTrait.cs
Unified sleep/scheduling trait for all living entities (players and NPCs).

**Features:**
- Decides when an entity should sleep based on energy level and time of day
- Tracks sleep state: Awake, GoingHome, Sleeping
- Min-awake cooldown (200 ticks) prevents sleep oscillation after waking
- Night work deferral: entities with `allowNightWork` and an active night JobTrait skip sleep during Night
- Emergency sleep at critical energy regardless of time/location
- Uses HomeTrait.IsEntityAtHome() and `HomeTrait?.Home` (Room?) for at-home detection
- Uses GoToRoomActivity for navigating home to sleep
- Verifies GoToRoomActivity target matches home room (won't confuse other navigation with "going home")

**Sleep Triggers:**
1. Critical energy (any phase) → emergency sleep, priority -1
2. Dusk + low energy → sleep early, priority 0
3. Night + low energy (no night job override) → sleep, priority 0

**Configuration (JSON Parameters):**
- `allowNightWork` (bool, default false): If true, defers sleep when entity has a night-phase JobTrait

**States:**
- `Awake` → entity is not sleeping
- `GoingHome` → navigating to home room for sleep via GoToRoomActivity
- `Sleeping` → SleepActivity is active

**Constants:**
- `MINAWAKETICKS`: 200 ticks minimum after waking before voluntary re-sleep
- `FULLENERGYTHRESHOLD`: 95f - don't initiate sleep if energy above this

### ScholarJobTrait.cs
Job trait for scholars who study at their home during daytime.
**Inherits from JobTrait** - enforces the correct pattern.

**Features:**
- Uses home room as workplace (special case - overrides GetWorkplace())
- Starts StudyActivity during Dawn/Day phases
- Returns null at night (ScheduleTrait/NecromancyStudyJobTrait handle night behavior)
- Context-aware dialogue based on time of day

**JobTrait Overrides:**
- `WorkActivityType`: `StudyActivity`
- `WorkplaceConfigKey`: "home"
- `GetWorkplace()`: Returns `_home` (a `Room`) instead of `_workplace`

**Constants:**
- `WORKDURATION`: 400 ticks (~50 seconds real time)

**Usage:**
```csharp
var scholarTrait = new ScholarJobTrait(homeRoom);
// or
var scholarTrait = new ScholarJobTrait();
scholarTrait.SetHome(homeRoom);
```

### IDesiredResources.cs
Interface for traits that specify desired resource levels at home storage.

**Features:**
- Entities can specify target quantities for items they want stockpiled at home
- Methods to check which resources are below desired levels
- Used by job traits to drive resource fetching behavior

**Key Interface Members:**
- `DesiredResources` - Dictionary mapping item IDs to desired quantities
- `GetMissingResources(storage)` - Returns items below target with needed amounts
- `AreDesiresMet(storage)` - Quick check if all targets are met
- `GetDeficit(storage, itemId)` - Get shortage for a specific item

**Implemented By:**
- `BakerJobTrait` - Wants flour, water, bread
- `FarmerJobTrait` - Wants wheat

**Usage:**
```csharp
// Check if a trait specifies desired resources
if (trait is IDesiredResources desiredResources)
{
    var homeRoom = _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
    var homeStorage = homeRoom?.GetStorageFacility()?.GetStorage();
    var missing = desiredResources.GetMissingResources(homeStorage);

    foreach (var (itemId, neededQty) in missing)
    {
        // Entity needs to fetch neededQty of itemId
    }
}
```

## Trait Hierarchy

```
Trait (base)
  +-- BeingTrait (Being-specific helpers)
        +-- LivingTrait (hunger + energy needs)
        +-- MindlessTrait (dialogue limits)
        +-- ItemConsumptionBehaviorTrait (item-based need satisfaction)
        +-- InventoryTrait (personal item storage, implements IStorageContainer)
        +-- AutomationTrait (toggle automated/manual behavior)
        +-- ScheduleTrait (unified sleep/scheduling for all living entities)
        +-- VillagerTrait (village daily routine, non-sleep)
        +-- HomeTrait (home Room reference + IsEntityAtHome)
        +-- DormantUndeadTrait (dormant until animated, uses Room-based home)
        +-- JobTrait (ABSTRACT - sealed SuggestAction, implements IDesiredResources)
              +-- FarmerJobTrait (farming work, WorkFieldActivity)
              +-- BakerJobTrait (baking work, BakingActivity)
              +-- DistributorJobTrait (resource distribution, Room/Facility-based)
              +-- ScholarJobTrait (studying work, StudyActivity)
              +-- NecromancyStudyJobTrait (necromancy study, StudyNecromancyActivity/WorkOnOrderActivity)
        +-- UndeadTrait (undead properties)
              +-- UndeadBehaviorTrait (abstract, wandering)
                    +-- SkeletonTrait (territorial)
                    +-- ZombieTrait (hunger-driven)
  +-- StorageTrait (building/facility storage, implements IStorageContainer)

Interfaces:
  +-- IDesiredResources (desired resource stockpile specification)
```

## Key Classes

| Trait | Description |
|-------|-------------|
| `ItemConsumptionBehaviorTrait` | Item-based need satisfaction from inventory/home |
| `InventoryTrait` | Personal item storage for beings |
| `StorageTrait` | Building/facility item storage |
| `LivingTrait` | Living entity needs (hunger, energy) |
| `MindlessTrait` | Non-sapient dialogue limits |
| `AutomationTrait` | Toggle between automated and manual behavior |
| `UndeadTrait` | Base undead properties |
| `UndeadBehaviorTrait` | Abstract wandering behavior |
| `SkeletonTrait` | Territorial skeleton behavior |
| `ZombieTrait` | Hunger-driven zombie behavior |
| `ScheduleTrait` | Unified sleep/scheduling for all living entities |
| `VillagerTrait` | Village daily routine (non-sleep) |
| `HomeTrait` | Home Room reference + IsEntityAtHome() |
| `DormantUndeadTrait` | Dormant undead using Room-based home |
| `JobTrait` | **Abstract base for all job traits** - sealed SuggestAction enforces pattern |
| `FarmerJobTrait` | Work at assigned farm room during day (extends JobTrait) |
| `BakerJobTrait` | Work at assigned bakery room during day (extends JobTrait) |
| `DistributorJobTrait` | Distribute resources between rooms/facilities (extends JobTrait) |
| `ScholarJobTrait` | Study at home room during day (extends JobTrait) |
| `NecromancyStudyJobTrait` | Study necromancy at altar during night (extends JobTrait) |
| `IDesiredResources` | Interface for traits that specify desired home stockpile levels |

## Important Notes

### Trait Priority
Lower priority values execute first. Typical ordering:
- -1: Job traits (FarmerJobTrait, BakerJobTrait) - run before main behavior
- 0: LivingTrait, base traits, storage traits
- 1: Main behavior trait (VillagerTrait, MindlessTrait)
- 2: Specific behavior (SkeletonTrait, ZombieTrait)
- Priority - 1: Consumption trait (needs to override when hungry)

### Storage System (IStorageContainer)
Both `InventoryTrait` and `StorageTrait` implement `IStorageContainer`:
```csharp
public interface IStorageContainer
{
    bool CanAdd(Item item);
    bool AddItem(Item item);
    Item? RemoveItem(string itemDefId, int quantity);
    bool HasItem(string itemDefId, int quantity = 1);
    int GetItemCount(string itemDefId);
    Item? FindItem(string itemDefId);
    Item? FindItemByTag(string tag);
    IEnumerable<Item> GetAllItems();
    void ProcessDecay();
}
```

### Trait Composition Pattern
Complex behaviors compose simpler traits:
```csharp
// VillagerTrait adds:
_owner?.SelfAsEntity().AddTraitToQueue<LivingTrait>(0, initQueue);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
```

### State Timer Pattern
Many traits use `_stateTimer` for decision pacing:
```csharp
if (_stateTimer > 0)
    _stateTimer--;

if (_stateTimer == 0)
{
    // Make decision, reset timer
    _stateTimer = IdleTime;
}
```

### Action Priority in Traits
Traits return actions with appropriate priorities:
- Idle actions: 0-1 (lowest priority)
- Movement actions: 1 (normal)
- Defending actions: -2 (high priority)
- Emergency actions: negative values
- Critical needs: -1 (interrupts sleep)

### Job Trait Pattern (CRITICAL)

Job traits (FarmerJobTrait, BakerJobTrait, etc.) must follow this pattern to avoid bugs.

**Rule: Traits DECIDE, Activities EXECUTE**

Job traits should ONLY:
1. Check if work should happen (time of day, not already in work activity)
2. Create and return a work activity via StartActivityAction

Job traits should NEVER:
- Access storage directly (storage requires physical proximity)
- Manage navigation (activities handle this)
- Check storage contents in SuggestAction (use memory checks only)

**Correct Pattern (FarmerJobTrait):**
```csharp
public override EntityAction? SuggestAction(...)
{
    // Check if already working - let activity handle everything
    if (_owner.GetCurrentActivity() is WorkFieldActivity) return null;

    // Check work hours
    if (gameTime.CurrentDayPhase is not(DayPhaseType.Dawn or DayPhaseType.Day)) return null;

    // Create activity - it handles ALL work logic (navigation, storage, work phases)
    var workActivity = new WorkFieldActivity(_workplace!, WORKDURATION, priority: 0);
    return new StartActivityAction(_owner, this, workActivity, priority: 0);
}
```

**Wrong Pattern (causes bugs):**
```csharp
public override EntityAction? SuggestAction(...)
{
    // BAD: Accessing storage in trait - entity might not be at workplace!
    var storage = _owner.AccessStorage(_workplace);
    if (storage != null && storage.HasItem("wheat")) { ... }

    // BAD: This returns null when entity is far from workplace
    // The trait thinks there's no wheat, but really it just can't see the storage
}
```

**Why This Matters:**
- `AccessStorage()` returns null if entity isn't adjacent to the facility
- Traits run every tick; navigation takes many ticks
- Phase-based activities ensure storage access only happens when entity has arrived

**Reference Implementations:**
- `WorkFieldActivity.cs` - Farming with GoingToWork/Working/TakingWheat/GoingHome/Depositing phases
- `BakingActivity.cs` / `ProcessReactionActivity.cs` - Crafting with navigation and storage phases

**Basic Job Trait Workflow:**
1. Check if owner is valid and not already in a work activity
2. Check if work hours (Dawn/Day)
3. Start appropriate work activity (activity handles everything else)
4. Return null at night (let VillagerTrait handle sleep)

### Room/Facility vs Building (Phase 5C)

All job traits and home-tracking traits now use `Room` and `Facility` instead of `Building` directly:

- `_workplace` in all JobTrait subclasses is `Room?` (not `Building?`)
- TraitConfiguration resolves workplace via `GetRoom()` instead of `GetBuilding()`
- `HomeTrait.Home` returns `Room?` - the `HomeBuilding` property was removed
- Storage access goes through `room.GetStorageFacility()` to get the Facility
- `DistributorJobTrait` accesses GranaryTrait via `_workplace.GetStorageFacility()?.SelfAsEntity().GetTrait<GranaryTrait>()`

**Correct patterns post-Phase-5C:**
```csharp
// Home as Room
var homeRoom = _owner?.SelfAsEntity().GetTrait<HomeTrait>()?.Home;
if (homeRoom == null || homeRoom.IsDestroyed) return null;

// Accessing storage via Facility
var facility = homeRoom.GetStorageFacility();
var storage = entity.CanAccessFacility(facility) ? facility.GetStorage() : null;

// Finding granary via facility trait
var granary = _workplace?.GetStorageFacility()?.SelfAsEntity().GetTrait<GranaryTrait>();
```

### Undead Detection
Checking if an entity is undead:
```csharp
if (entity.SelfAsEntity().HasTrait<UndeadTrait>())
```

### Activity-Based Navigation Pattern
Traits should use activities for navigation instead of manual pathfinding. This centralizes navigation logic and handles edge cases.

**Pattern for starting navigation:**
```csharp
// Start an activity to navigate somewhere
var activity = new GoToLocationActivity(targetPosition, priority: 1);
return new StartActivityAction(_owner, this, activity, priority: 1);
```

**Pattern for checking if navigation is in progress:**
```csharp
// Check if activity is still running - return null to let it handle things
if (_owner.GetCurrentActivity() is GoToLocationActivity)
{
    return null;  // Activity handles navigation
}
```

**Common navigation activities:**
- `GoToRoomActivity` - Navigate to a room's interior (finds door, enters)
- `GoToLocationActivity` - Navigate to a specific grid position

**Note:** `GoToBuildingActivity` has been replaced by `GoToRoomActivity` in VillagerTrait and ScheduleTrait. Use `GoToRoomActivity` for room-based navigation in new code.

**Activity state checking (for traits that manage activities directly):**
```csharp
if (_myActivity != null)
{
    if (_myActivity.State == Activity.ActivityState.Running)
    {
        return _myActivity.GetNextAction(position, perception);
    }
    if (_myActivity.State == Activity.ActivityState.Completed)
    {
        _myActivity = null;  // Clear completed activity
        // Handle arrival
    }
    if (_myActivity.State == Activity.ActivityState.Failed)
    {
        _myActivity = null;  // Clear failed activity
        // Handle failure (retry or give up)
    }
}
```

### Blocking Response Pattern
When an entity's movement is blocked by another entity, traits can define custom response behavior by overriding `GetBlockingResponse()`:

```csharp
public override EntityAction? GetBlockingResponse(Being blockingEntity, Vector2I targetPosition)
{
    // Return an action to respond to being blocked, or null for default behavior
    // Default behavior is RequestMoveAction (polite communication)
    return new PushAction(_owner, this, blockingEntity, pushDirection, priority: 0);
}
```

The blocking response system:
1. Movement fails and stores the blocking entity
2. Next tick, `Being.Think()` iterates through traits calling `GetBlockingResponse()`
3. First non-null response is used; otherwise default behavior (RequestMoveAction)
4. This ensures blocking interactions cost a turn

### Event Handling Pattern
Traits can intercept events received from other entities by overriding `HandleReceivedEvent()`:

```csharp
public override bool HandleReceivedEvent(EntityEvent evt)
{
    if (evt.Type == EntityEventType.MoveRequest)
    {
        // Handle the event (or ignore it)
        return true; // Return true = handled, skip default behavior
    }
    return false; // Return false = let other traits or default behavior handle it
}
```

The event handling system:
1. Entity receives an event (MoveRequest, EntityPushed, etc.)
2. `Being.HandleEvent()` iterates through traits calling `HandleReceivedEvent()`
3. If any trait returns true, default handling is skipped
4. Otherwise, default behavior runs (step aside, stumble, etc.)

### Audio Integration Pattern
Traits trigger audio via deferred calls:
```csharp
(_owner as MindlessSkeleton)?.CallDeferred("PlayBoneRattle");
(_owner as MindlessZombie)?.CallDeferred("PlayZombieGroan");
```

## Creating a New Trait

### Step-by-Step Guide

1. **Create the file** in `/entities/traits/` (e.g., `MyNewTrait.cs`)

2. **Choose the base class**:
   - `BeingTrait` - For most entity behaviors
   - `Trait` - For non-being entities (like StorageTrait)
   - `UndeadBehaviorTrait` - For undead with wandering behavior (abstract, override `ProcessState`)

3. **Basic trait template**:
```csharp
namespace VeilOfAges.Entities.Traits;

public class MyNewTrait : BeingTrait
{
    private enum State { Idle, Active }
    private State _currentState = State.Idle;
    private int _stateTimer = 0;

    public override void Initialize(Being owner, object? data)
    {
        base.Initialize(owner, data);
        // Add any sub-traits here:
        // _owner?.SelfAsEntity().AddTraitToQueue<OtherTrait>(Priority - 1, initQueue);
    }

    public override EntityAction? SuggestAction(Vector2I currentPosition, Perception perception)
    {
        // Decrement timer
        if (_stateTimer > 0) _stateTimer--;

        // State machine logic
        switch (_currentState)
        {
            case State.Idle:
                if (_stateTimer == 0)
                {
                    // Make decision, transition states
                    _stateTimer = 10; // Reset timer
                }
                return new IdleAction(_owner!, this, priority: 1);

            case State.Active:
                // Return appropriate action
                return new IdleAction(_owner!, this, priority: 0);
        }

        return null;
    }
}
```

4. **Add the trait to a Being** in the Being's `_Ready()` method:
```csharp
SelfAsEntity().AddTraitToQueue<MyNewTrait>(1); // priority 1
```

### Key Considerations

- **Priority**: Lower values execute first (0 = highest priority)
- **Action priorities**: Return actions with appropriate priority (-2 for urgent, 0-1 for normal)
- **State timer**: Use `_stateTimer` pattern to pace decisions
- **Thread safety**: Use `CallDeferred()` for any Godot scene operations
- **Composition**: Complex traits should compose simpler traits via `AddTraitToQueue()`

### Common Patterns

**Adding item-based consumption behavior**:
```csharp
// In Initialize():
var consumptionTrait = new ItemConsumptionBehaviorTrait(
    "hunger",
    "food",
    restoreAmount: 60f,
    consumptionDuration: 244
);
_owner?.SelfAsEntity().AddTraitToQueue(consumptionTrait, Priority - 1, initQueue);
// Home Room is resolved automatically via HomeTrait.Home
```

**Detecting other entities**:
```csharp
foreach (var entity in perception.GetEntitiesOfType<Being>())
{
    if (entity.SelfAsEntity().HasTrait<UndeadTrait>()) continue; // Skip undead
    // React to living entity
}
```

**Creating a job trait (using JobTrait base class)**:
```csharp
public class MyJobTrait : JobTrait
{
    private const uint WORKDURATION = 400;

    // Required: specify the activity type for "already working" check
    protected override Type WorkActivityType => typeof(MyWorkActivity);

    // Optional: customize the config key (default is "workplace")
    // TraitConfiguration will resolve this via GetRoom()
    protected override string WorkplaceConfigKey => "workplace";

    // Optional: specify desired resources for home storage
    public override IReadOnlyDictionary<string, int> DesiredResources => _desiredResources;
    private static readonly Dictionary<string, int> _desiredResources = new()
    {
        { "my_resource", 5 }
    };

    public MyJobTrait() { }

    public MyJobTrait(Room workplace)
    {
        _workplace = workplace;
    }

    // Required: create the work activity
    // This is the ONLY way to define work - you cannot access storage here
    protected override Activity? CreateWorkActivity()
    {
        return new MyWorkActivity(_workplace!, WORKDURATION, priority: 0);
    }
}
```

**Why use JobTrait instead of BeingTrait directly?**
- `SuggestAction()` is **sealed** - you cannot accidentally access storage
- Work hours checking is handled automatically
- "Already in activity" checking is handled automatically
- Movement interruption checking is handled automatically
- The pattern is enforced at compile time, not just by convention

**Navigating to a room using activity**:
```csharp
// Start navigation to a room
var goToRoomActivity = new GoToRoomActivity(targetRoom, priority: 1);
return new StartActivityAction(_owner, this, goToRoomActivity, priority: 1);

// In subsequent calls, check if activity is still running
if (_owner.GetCurrentActivity() is GoToRoomActivity)
{
    return null;  // Let activity handle navigation
}
// Activity completed - we've arrived at the room
```

**Navigating to a position using activity**:
```csharp
// Start navigation to a specific position
var goToLocationActivity = new GoToLocationActivity(targetPosition, priority: 1);
return new StartActivityAction(_owner, this, goToLocationActivity, priority: 1);

// In subsequent calls, check if activity is still running
if (_owner.GetCurrentActivity() is GoToLocationActivity)
{
    return null;  // Let activity handle navigation
}
// Activity completed - we've arrived at the position
```

**Managing activity state directly (for more control)**:
```csharp
private GoToLocationActivity? _navigationActivity;

public override EntityAction? SuggestAction(Vector2I pos, Perception perception)
{
    // Check if we're in the middle of navigation
    if (_navigationActivity != null)
    {
        if (_navigationActivity.State == Activity.ActivityState.Running)
        {
            return _navigationActivity.GetNextAction(pos, new Perception());
        }
        if (_navigationActivity.State == Activity.ActivityState.Completed)
        {
            _navigationActivity = null;
            // Handle arrival...
        }
        if (_navigationActivity.State == Activity.ActivityState.Failed)
        {
            _navigationActivity = null;
            // Handle failure, maybe retry...
        }
    }

    // Start new navigation if needed
    if (needToNavigate)
    {
        _navigationActivity = new GoToLocationActivity(target, priority: 1);
        _navigationActivity.Initialize(_owner);
        return _navigationActivity.GetNextAction(pos, new Perception());
    }

    return new IdleAction(_owner, this);
}
```

## Dependencies

### Depends On
- `VeilOfAges.Entities.BeingTrait` - Base class
- `VeilOfAges.Entities.Actions` - Action types
- `VeilOfAges.Entities.Activities` - Activity types (WorkFieldActivity, ConsumeItemActivity, GoToRoomActivity, etc.)
- `VeilOfAges.Entities.Beings.Health` - Body systems
- `VeilOfAges.Entities.Items` - Item and ItemDefinition classes
- `VeilOfAges.Entities.Needs` - Need system
- `VeilOfAges.Entities.Reactions` - Reaction system (for BakerJobTrait)
- `VeilOfAges.Entities.Sensory` - Perception
- `VeilOfAges.Core.Lib` - PathFinder, GameTime
- `VeilOfAges.UI` - Dialogue system

### Depended On By
- All Being subclasses in `/entities/beings/`
- Entity spawning systems
- Building system (for StorageTrait)
