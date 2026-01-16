# Current Work: Resource Economy & Village Simulation

## Status: Resource System Complete, Memory System Next

## Recently Completed (January 2026)

### Resource, Storage & Production System - COMPLETE

Built a comprehensive resource economy with items, storage, and a wheat-to-bread production chain.

#### Item System (`/entities/items/`)
- **ItemDefinition.cs** - JSON-serializable templates with volume, weight, decay, nutrition, tags
- **Item.cs** - Runtime instances with stacking and decay mechanics
- **ItemResourceManager.cs** - Singleton loader from `resources/items/`
- **Items defined**: wheat, flour, bread, corpse

#### Reactions System (`/entities/reactions/`)
- **ReactionDefinition.cs** - JSON templates with inputs, outputs, duration, facility requirements
- **ReactionResourceManager.cs** - Singleton loader from `resources/reactions/`
- **Reactions defined**:
  - mill_wheat (2 wheat → 1 flour, requires mortar_and_pestle)
  - bake_bread (2 flour → 1 bread, requires oven)

#### Storage System (`/entities/traits/`)
- **IStorageContainer.cs** - Interface for unified storage access
- **StorageTrait.cs** - Building storage with volume capacity, decay modifier, facilities list
- **InventoryTrait.cs** - Being inventory with volume/weight limits

#### Production Activities (`/entities/activities/`)
- **WorkFieldActivity.cs** - Multi-phase farmer workflow:
  1. Go to farm
  2. Work (produce 3 wheat to farm storage)
  3. Take wheat from farm (4-6 units)
  4. Go home
  5. Deposit wheat to home storage
- **ProcessReactionActivity.cs** - Generic reaction processor that checks facilities and processes inputs→outputs

#### Job Traits (`/entities/traits/`)
- **FarmerJobTrait.cs** - Works at assigned farm during Dawn/Day, passes home to WorkFieldActivity
- **BakerJobTrait.cs** - Checks home storage for reactions, prioritizes baking over milling

#### Item-Based Consumption
- **ConsumeItemActivity.cs** - Checks inventory first, then goes home, then consumes item
- **ItemConsumptionBehaviorTrait.cs** - Replaces building-based consumption, uses food tags
- **VillagerTrait** - Uses "food" tag, gets home from `_home` property
- **ZombieTrait** - Uses "zombie_food" tag, gets corpses from graveyard storage

#### Village Setup (`/world/generation/VillageGenerator.cs`)
- Spawns 2 villagers per house (farmer + baker)
- Sets home ownership via `VillagerTrait.SetHome()`
- Stocks houses with initial bread (3-5 loaves)
- Stocks graveyards with corpses (5-10) for zombies

#### Building Navigation Improvements
- **Building.GetWalkableInteriorPositions()** - Queries GridArea (source of truth) for walkable tiles
- **PathFinder** - `SetBuildingGoal()` now navigates to interior positions when available
- **VillagerTrait** - Uses `SetBuildingGoal` so villagers go inside homes, not just to perimeter

#### Resource Manager Initialization (`/core/GameController.cs`)
All resource managers initialized centrally in `_Ready()`:
```csharp
TileResourceManager.Instance.Initialize();
ItemResourceManager.Instance.Initialize();
ReactionResourceManager.Instance.Initialize();
```

### Production Chain Flow
1. Farmer works at farm → produces wheat → brings harvest home → deposits to home storage
2. Baker checks home storage → mills wheat to flour → bakes flour to bread
3. Villager gets hungry → checks inventory → checks home storage → eats bread
4. Zombie gets hungry → goes to graveyard → eats corpse from storage

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

### Use Cases
- Baker checks memory before starting reaction - "do I remember having enough wheat?"
- Villager decides which storage to check first based on remembered contents
- Farmer decides whether to bring more wheat home based on remembered home storage levels

---

## Architecture Summary

### Three Layers
| Layer | Role | Examples |
|-------|------|----------|
| **Traits** | DECIDE | VillagerTrait chooses to sleep, eat, or work |
| **Activities** | EXECUTE | SleepActivity, WorkFieldActivity, ConsumeItemActivity |
| **Actions** | ATOMIC | MoveAlongPathAction, IdleAction |

### Key Systems
| System | Purpose |
|--------|---------|
| Items | Stackable resources with decay |
| Storage | Building and being containers |
| Reactions | Input→output transformations |
| Activities | Multi-step behaviors |
| Needs | Drive entity decisions |

---

## Files Modified in Resource System

### New Files
- `/entities/items/ItemDefinition.cs`
- `/entities/items/Item.cs`
- `/entities/items/ItemResourceManager.cs`
- `/entities/items/IStorageContainer.cs`
- `/entities/items/CLAUDE.md`
- `/entities/reactions/ReactionDefinition.cs`
- `/entities/reactions/ReactionResourceManager.cs`
- `/entities/reactions/ItemQuantity.cs`
- `/entities/reactions/CLAUDE.md`
- `/entities/traits/StorageTrait.cs`
- `/entities/traits/InventoryTrait.cs`
- `/entities/traits/BakerJobTrait.cs`
- `/entities/traits/ItemConsumptionBehaviorTrait.cs`
- `/entities/activities/ProcessReactionActivity.cs`
- `/entities/activities/ConsumeItemActivity.cs`
- `/resources/items/wheat.json`
- `/resources/items/flour.json`
- `/resources/items/bread.json`
- `/resources/items/corpse.json`
- `/resources/reactions/mill_wheat.json`
- `/resources/reactions/bake_bread.json`

### Modified Files
- `/entities/building/Building.cs` - Storage trait, GetWalkableInteriorPositions()
- `/entities/building/BuildingTemplate.cs` - Storage configuration
- `/entities/traits/VillagerTrait.cs` - Home ownership, InventoryTrait, ItemConsumptionBehaviorTrait
- `/entities/traits/ZombieTrait.cs` - Graveyard home, ItemConsumptionBehaviorTrait
- `/entities/activities/WorkFieldActivity.cs` - Wheat production and transport
- `/entities/traits/FarmerJobTrait.cs` - Passes home to WorkFieldActivity
- `/core/lib/PathFinder.cs` - Interior building navigation
- `/core/GameController.cs` - Resource manager initialization
- `/world/generation/VillageGenerator.cs` - 2 villagers per house, job assignment, initial stocking
- `/resources/buildings/templates/simple_house.json` - Storage and facilities
- `/resources/buildings/templates/graveyard.json` - Storage for corpses

---

## Testing Checklist
- [ ] Farmer produces wheat and brings it home
- [ ] Baker mills wheat to flour
- [ ] Baker bakes flour to bread
- [ ] Villager eats bread when hungry
- [ ] Zombie eats corpse from graveyard
- [ ] Villagers navigate inside their homes (not just to perimeter)
- [ ] Initial bread in houses provides first meals
- [ ] Production chain sustains village over time
