---
name: veilofages-ui-design
description: >-
  Consult this skill for any UI/UX design decisions in Veil of Ages — what information
  to display, how to structure panels, what to show in which control mode, how the
  information-flow/staleness mechanic affects the UI, the four control tiers, activity
  queue behavior, notification design, kingdom management panels, familiar relay UI,
  progressive complexity stages, or any question about what the UI should do or show.
  Pair with veilofages-ui-godot for Godot 4.6 C# implementation.
---

# Veil of Ages UI/UX Design Reference

The game's UI unifies roguelike tile combat, Sims-style autonomy queues, TTRPG character depth, and kingdom management — all filtered through a "no god knowledge" information system where the player only sees what their character could realistically know.

## Reference files

Load these as needed — do NOT read all at once:

- **[control-modes.md](references/control-modes.md)**: Detailed behavior for each control tier, mode transitions, activity queue mechanics, and urgency/calm UI registers
- **[information-flow.md](references/information-flow.md)**: Four-state staleness model, familiar relay UI, kingdom management panel, advisor system, composite field-of-view, player annotations
- **[notifications-and-progression.md](references/notifications-and-progression.md)**: Five notification tiers with behavior specs, progressive complexity stages, onboarding philosophy, alert fatigue prevention

---

## Non-negotiable constraints

These are core to the game's identity. Never violate them.

**No god knowledge.** Every data point must trace to a source: direct LOS observation, familiar report, NPC dialogue, or magic. If you cannot name the source, the UI must not show it.

**Active pause is sacred.** The player can open any panel, issue any command, read any tooltip, and make any decision while paused. Never restrict or dismiss panels on unpause without explicit player action.

**Silent overrides are bugs.** Whenever the autonomy system interrupts a player-queued action, the reason must be shown (`⚠ Interrupted: Hunger critical — eating`). Never cancel a queued item without visible explanation.

**Consistent interaction grammar — never break these:**
- Left-click = select
- Right-click = context menu
- Hover = tooltip (CK3-style nested tooltips with stat breakdowns)
- Shift+click = add to queue end
- Plain click (in Queue mode) = insert at queue front
- Drag = reorder

---

## Screen layout

```
┌──────────────────────────────────────────────────────────────┐
│ [Date/Season]  [⏸ 1x 2x 4x 8x ⏩]  [MODE STRIP]  [Alerts] │
├──────────────────────────────────────────────────────────────┤
│              │                              │                 │
│  [Entity/    │                              │  [Notification  │
│  Knowledge   │       GAME WORLD             │   Feed]         │
│   List]      │       (Tile Map)             │  [Activity      │
│  (collaps-   │                              │   Queue]        │
│   ible)      │                              │  [Minimap /     │
│              │                              │   Orders Log]   │
├──────────────┴──────────────────────────────┴────────────────┤
│ [Portrait + Sigil]  [5 Need Circles]  [Moodles]  [Context]  │
└──────────────────────────────────────────────────────────────┘
```

- **Always visible:** top bar (time/speed/mode/alerts), bottom-left character cluster (portrait, necromantic sigil, need circles, moodle icons)
- **Collapsible via hotkey:** left sidebar (entity/knowledge list), right sidebar (minimap + orders log)
- **Context Zone (bottom-center):** transforms based on active control mode — see [control-modes.md](references/control-modes.md)
- **Right edge:** Moodle-style condition icons (Project Zomboid pattern) — appear only when active, 4 severity levels via color intensity
- **Game world (center):** sacred — no HUD chrome, only minimal floating status icons above entities

---

## Quick reference: four control modes

| Mode | Color | Context Zone shows | Cursor | Key detail |
|---|---|---|---|---|
| Autonomy Profiles | Blue | Active profile name + edit button | Arrow | Weighted priority sliders with quick-swap presets |
| Activity Queue | Green | Horizontal drag-and-drop queue strip | Arrow + plus | Max 8–10 visible; ghost entries show AI's next choice |
| Immediate Action | Amber | Queue with top slot pulsing | Arrow + ! | Spring-loaded (held key, not toggle); interrupted item paused, never deleted |
| Manual Override | Red | Roguelike action bar + ability hotbar | Crosshair | Zoom-in animation + red tint; world continues running |

Mode transitions: animated (slide/fade, never snap), visual + audio cue, deliberate input required. See [control-modes.md](references/control-modes.md).

---

## Quick reference: information staleness

| State | Condition | Visual treatment | Number display |
|---|---|---|---|
| Unknown | Never observed | Black / parchment edge | Nothing shown |
| Stale-old | Weeks/months ago | Heavy desaturation, sepia, question marks | `~200–300 (Steward, 3 months ago)` |
| Stale-recent | Days ago | Light desaturation, subtle fog, timestamp | `~340 (2 days ago)` |
| Observed | Current LOS | Full color, full detail | `Food: 347 units` |

Apply to every UI element: minimap, entity list, resource counts, NPC status. Growing uncertainty halos around last-known positions. Familiar vision tinted blue-green; living messenger vision in warm amber. See [information-flow.md](references/information-flow.md).

---

## Quick reference: notification tiers

| Tier | Color | Behavior | Examples |
|---|---|---|---|
| Critical | Red | Force pause; modal banner; alarm audio | Near death, undead rebellion, kingdom attack |
| Urgent | Amber | Auto-pause at 4x+; prominent toast + audio | Dangerous need levels, critical familiar report |
| Important | Blue | Toast + pip; no pause; batches at high speed | New settler, building complete |
| Informational | Grey | Log entry only | Routine completions, minor social events |
| Montage | Gold | Structured summary on montage end | All events during time skip |

Distant notifications arrive with delay + narrative framing. See [notifications-and-progression.md](references/notifications-and-progression.md).

---

## Character status (bottom-left cluster)

**Ambient (always on, zero cognitive cost):** Necromantic sigil shifts color: green-teal → amber → crimson. Screen-edge vignette at low health. Audio shift at low mental fortitude.

**Compact persistent (glanceable):** Five need circles (hunger, thirst, sleep, social, mental fortitude) — fill level (green/amber/red) + trend arrow. No numbers on HUD. Moodle icons appear only when conditions are active; clean HUD = everything fine.

**Character sheet (Tab or portrait click):** Full RPG stats, detailed need values, conditions with durations. Moodlet explanations: `+15 Mental Fortitude: Raised undead servant (2 days ago)`

---

## Anti-patterns — never do these

- Show data the character couldn't know (real-time distant stats, unscouted tiles, NPC status without recent contact)
- Cancel a queued item silently with no explanation
- Allow any ambiguity about which control tier is active
- Keep the HUD always cluttered — peaceful periods must feel minimal so problems create contrast
- Give the player omniscient information "by accident" in any panel, tooltip, or map view
- Let notification volume scale linearly with kingdom size — batch, filter, and tier ruthlessly
- Make managing the UI more demanding than making strategic decisions ("playing the UI instead of the game")
- Neglect the quiet state — a calm HUD with few icons is positive reinforcement
- Set non-interactive background Controls to default MouseFilter — always set Ignore (see Godot skill)
