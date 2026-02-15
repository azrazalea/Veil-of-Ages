# /entities/work_orders

## Purpose

This directory contains the work order system for persistent multi-tick tasks that live on facilities. Work orders represent jobs that can be interrupted and resumed - progress is stored on the facility, not the entity, so any qualified entity can continue the work.

## Files

### WorkOrder.cs
Abstract base class for all work orders.

- **Namespace**: `VeilOfAges.Entities.WorkOrders`
- **Class**: `WorkOrder` (abstract)
- **Key Properties**:
  - `Id` - Unique instance identifier
  - `Type` - Type identifier (e.g., "raise_zombie")
  - `ProgressTicks` / `RequiredTicks` - Progress tracking
  - `IsComplete` - True when ProgressTicks >= RequiredTicks
  - `XpRewards` - List of (skillId, xpPerTick) for multi-skill XP
  - `EnergyDrainPerTick` - Energy cost per tick of work
  - `Parameters` - Arbitrary data dictionary
- **Key Methods**:
  - `Advance(Being worker)` - Increment progress, grant XP, drain energy. Calls `OnComplete(worker)` when finished.
  - `GetProgressString()` - Returns percentage string (e.g., "45%")
  - `OnComplete(Being worker)` - Abstract method for completion logic (spawn zombie, produce items, etc.)
- **Usage Pattern**:
  - Work order created and placed on a facility via `facility.SetWorkOrder(order)`
  - WorkOnOrderActivity advances the order each tick via `Advance(worker)`
  - When `IsComplete` becomes true, activity calls `facility.CompleteWorkOrder()`

### RaiseZombieWorkOrder.cs
Work order for raising a zombie from a corpse on the necromancy altar.

- **Namespace**: `VeilOfAges.Entities.WorkOrders`
- **Class**: `RaiseZombieWorkOrder` (extends `WorkOrder`)
- **Duration**: ~5 in-game hours (3,413 ticks at ~681.74 ticks/hour)
- **XP Rewards**:
  - Necromancy: 0.025 XP per tick
  - Arcane Theory: 0.008 XP per tick
- **Energy Drain**: 0.015 per tick
- **Properties**:
  - `SpawnFacility` - The altar facility where zombie spawns
  - `AltarBuilding` - The building containing the altar
- **Completion**: Logs completion message. Actual zombie spawning is handled by the activity/command that processes the order, since it requires main thread access.

## Design Notes

### Progress Persistence
Work orders live on facilities (`Facility.ActiveWorkOrder`), not on entities. This means:
- Progress persists across entity interruptions (sleep, critical needs, etc.)
- Any entity with the right skills can continue the work
- Multiple entities can work on the same order (progress accumulates)

### Multi-Skill XP
Work orders can grant XP to multiple skills simultaneously. For example, raising a zombie grants both necromancy and arcane_theory XP.

### Energy Drain
Work drains energy directly via `Need.Restore(-amount)`, not via decay multipliers. This creates a clearer "work costs energy" mental model.

### Completion Handling
The `OnComplete(worker)` method is called when progress reaches 100%. However, complex completion logic (spawning entities, scene tree manipulation) should be deferred to the activity/command that processes the order, since `OnComplete` may be called from a background thread.

## Integration

### Facility Integration
Facilities in `Facility.cs` have:
- `ActiveWorkOrder` property
- `SetWorkOrder(order)` method
- `CompleteWorkOrder()` method to clear the order

### Activity Integration
`WorkOnOrderActivity` handles the work loop:
1. Navigate to facility (cross-area if needed)
2. Call `workOrder.Advance(worker)` each tick
3. Check `workOrder.IsComplete` after each advance
4. Call `facility.CompleteWorkOrder()` when done

### Dialogue Integration
`IFacilityInteractable` implementations check `facility.ActiveWorkOrder` to disable/enable options and show progress.

## Creating a New Work Order

1. **Create a subclass** in `/entities/work_orders/` (e.g., `CraftScrollWorkOrder.cs`)

2. **Define constants**:
```csharp
private const int REQUIREDTICKS = 2000;
private static readonly IReadOnlyList<(string, float)> XPREWARDS = new List<(string, float)>
{
    ("scribing", 0.02f),
    ("arcane_theory", 0.01f)
};
private const float ENERGYDRAIN = 0.01f;
```

3. **Call base constructor**:
```csharp
public CraftScrollWorkOrder()
    : base(
        id: $"craft_scroll_{Guid.NewGuid():N}",
        type: "craft_scroll",
        requiredTicks: REQUIREDTICKS,
        xpRewards: XPREWARDS,
        energyDrainPerTick: ENERGYDRAIN)
{
}
```

4. **Implement `OnComplete(worker)`**:
```csharp
protected override void OnComplete(Being worker)
{
    Log.Print($"{worker.Name}: Scroll crafting complete!");
    // Store completion data in Parameters for activity to process
    Parameters["scroll_type"] = "fireball";
}
```

5. **Add facility interaction** (if needed) in a `IFacilityInteractable` implementation to create and set the order.

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Worker entity
- `VeilOfAges.Entities.Skills` - Skill system for XP grants
- `VeilOfAges.Entities.Needs` - Need system for energy drain

### Depended On By
- `VeilOfAges.Entities.Facility` - Stores active work order
- `VeilOfAges.Entities.Activities.WorkOnOrderActivity` - Executes work orders
- `VeilOfAges.Entities.IFacilityInteractable` implementations - Create and check work orders
