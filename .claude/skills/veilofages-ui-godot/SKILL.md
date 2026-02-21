---
name: veilofages-ui-godot
description: >-
  Consult this skill when writing any UI code for Veil of Ages in Godot 4.6 C#.
  Triggers: implementing UI panels or HUD elements, UI update architecture (hybrid
  throttled reads + discrete events), node pooling for dynamic lists, scene structure
  and CanvasLayer stacking, theming and type variations, fog-of-war rendering, input
  handling and click-through prevention, drag-and-drop for queues, custom/nested
  tooltips, toast notifications, animated transitions with Tween. Also consult for
  C# gotchas specific to Godot 4.6 UI. Pair with veilofages-ui-design for design
  decisions.
---

# Veil of Ages — Godot 4.6 C# UI Implementation Guide

## Reference files

Load these as needed — do NOT read all at once:

- **[architecture.md](references/architecture.md)**: Hybrid update model, simulation interfaces, service locator, discrete events, throttled reads, node pooling — all with code examples
- **[components.md](references/components.md)**: Scene structure, CanvasLayer stacking, theming, fog of war, tooltips, drag-and-drop, input handling, notifications, context menus — all with code examples

---

## Architecture

UI panels use a **hybrid update model**:

- **Continuous state** (needs, health, activity progress, resources): panels read C# interface properties on a throttled `_Process` timer at 2–4 fps. The simulation ticks at 8/sec — reading faster than that is pointless, and even 2–4 fps is fine for text and progress bars.
- **Discrete happenings** (notifications, mode changes, queue mutations, familiar reports, combat start/end): coarse C# events via a static event bus. These need immediate UI response — animations, toasts, audio cues, forced pauses.

Panels access simulation state through **C# interfaces** resolved via a **service locator** — never through direct node references or hardcoded paths. Each panel is its own `.tscn` + C# controller, responsible for only the state it displays.

See [architecture.md](references/architecture.md) for full patterns and code.

---

## Rules

1. **Throttle continuous reads to 2–4 fps.** Use `_Process` with a timer. Do not read simulation state every frame — it changes at most 8 times/sec. `_PhysicsProcess` is for physics, never UI.

2. **Use discrete events sparingly and coarsely.** Events like `NotificationRaised`, `ModeChanged`, `QueueMutated` — not per-property events like `PopulationChanged` or `GoldChanged`. If the state changes gradually, read it on the throttle. If something specific happened that the UI must react to immediately, fire an event.

3. **Never destroy and recreate nodes for list updates.** Use `ItemList` for simple text lists. Use a pool with incremental diffing for custom card layouts. `QueueFree` is for permanent removal only.

4. **Guard text assignments:** `if (label.Text != newText) label.Text = newText;` — same-value sets trigger internal Godot updates.

5. **Set `MouseFilter = Ignore`** on every non-interactive Control (backgrounds, decorative panels, overlays). The default `Stop` silently eats mouse events and blocks clicks from reaching the game world.

6. **All gameplay input goes in `_UnhandledInput`.** UI Controls consume input events before they reach `_UnhandledInput`, so UI-vs-world blocking works automatically with no "is mouse over UI?" checks.

7. **Kill existing tweens** before creating new ones on the same property: `_tween?.Kill();`

8. **Use C# `event Action<T>` for events, not Godot signals.** No Variant marshalling overhead, compile-time type safety. Godot signals only when you need editor-time signal wiring or GDScript interop.

---

## C# gotchas — Godot 4.6

| Gotcha | Rule |
|---|---|
| Missing `partial` | Every node class must be `partial` — without it, source generators fail **silently** (exports/signals break, no error) |
| `Dispose()` leaks | Use `QueueFree()` for nodes, `Free()` for non-node GodotObjects — `Dispose()` only releases managed wrapper |
| Lambda subscriptions | Never use lambdas for event connections you need to disconnect — use named methods |
| `_Ready` once only | For pooled/re-parented nodes, put logic in `_EnterTree`/`_ExitTree`, not `_Ready` |
| `[Export]` before `_Ready` | Guard setters accessing children: `if (IsInsideTree()) Refresh();` |
| Freed nodes aren't null | Check `IsInstanceValid(this)` in event handlers — freed nodes throw on access |
| StringName GC pressure | Cache `StringName` constants for hot-path input actions |
| StyleBox sharing | Always `Duplicate()` StyleBoxes before modifying — they are reference types shared across nodes |
