# Control Modes and Activity Queue — Design Detail

## Table of contents

- Mode indicator strip
- Mode transitions
- Tier 1: Autonomy Profiles
- Tier 2: Activity Queue
- Tier 3: Immediate Action
- Tier 4: Manual Override
- Activity queue mechanics
- Urgency and calm: two UI registers

---

## Mode indicator strip

Persistent strip at top-center, always visible. Four icons in a horizontal row — active mode highlighted by colored glow and border. The mode color permeates the entire Context Zone (bottom-center panel). Cursor also changes per mode, providing dual redundancy so the player always knows their tier.

Key lessons:
- RimWorld's biggest UX complaint: players forget colonists are drafted → never allow that ambiguity
- Battlezone 2: unambiguous mode-switching with layered transitions prevents disorientation
- KeeperRL: mode confusion when switching between RTS and roguelike views

---

## Mode transitions

- Animated, not instant — brief 0.5s camera shift (zoom in for Manual Override, zoom out for Montage)
- Both visual and audio confirmation cue (distinct sound per tier)
- Deliberate input required: hold key for 0.5s rather than single tap to prevent accidental activation
- Achievable via both hotkey and click — one input maximum
- Consider Executive Assault 2 pattern: character must be in specific location (study, throne room) for kingdom management, while Manual Override is available anywhere

---

## Tier 1: Autonomy Profiles (Blue)

Context Zone shows only the active profile name and "edit" button. Profile editor uses **weighted sliders** (not RimWorld's 1–4 numbers) — a pie chart or slider bank showing relative weights for survival needs, work priorities, combat readiness, and social behavior.

Quick-swap presets: "Adventurer," "Scholar," "Survivalist," "Combat Ready" — one-click profile switch.

When autonomy overrides a queued action: `⚠ Interrupted: Hunger critical — eating`

Hovering a priority cell shows why that priority level makes sense given the character's stats (Fluffy's Work Tab mod pattern).

---

## Tier 2: Activity Queue (Green)

Horizontal queue strip at bottom-center. Current task at left, larger and highlighted with progress bar. Upcoming tasks trail right as smaller icons.

- Shift-click objects → add to queue end
- Regular click → insert at position zero
- Drag-and-drop reordering essential
- Each item shows estimated duration where calculable
- KeeperRL auto-queuing: designating a construction area auto-populates sub-tasks in logical order
- Maximum 8–10 visible items with scroll indicator

**Visual distinction:** Player-queued vs AI-suggested actions get different border colors or opacity. Ghost entries at queue bottom show what the AI would do next.

---

## Tier 3: Immediate Action (Amber)

**Spring-loaded** — active only while modifier key is held, auto-deactivates on release (Nielsen Norman Group recommendation to prevent mode slips).

While held, clicking creates a priority-insert task: urgent action at queue position zero with pulsing amber border, existing items shift right with smooth animation. Queue strip briefly flashes. After immediate action completes, queue resumes normally.

FTL's "pause and issue orders during crisis" pattern adapted for a queue system.

---

## Tier 4: Manual Override (Red)

Entry triggers zoom-in animation (XCOM-style), Context Zone transforms to roguelike action bar, screen edges shift toward red. Character becomes directly controllable — WASD or click-to-move, ability hotbar, spell targeting.

**All other entities continue running their autonomy profiles** — the world does not pause. Automatic timeout warning after extended use: `⚠ 3 citizens have unmet needs` — prevents the "forgot to undraft" problem.

---

## Activity queue mechanics

**Current task display:** Leftmost position, larger icon, task name, progress bar, contextual detail (`Mining copper — 65% — ~2 min`). Autonomous overrides get amber frame with reason on hover.

**Queue interruption feedback:**
- Immediate Action pushes: existing items animate rightward
- Autonomy interrupts: interrupted task slides to "paused" position (greyed, pause icon) — will resume when interruption resolves; never deleted
- Impossible task: turns red with X, hover tooltip explains why (resource depleted, path blocked)

**Interaction model:**
- Left-click queued item → see details
- Right-click → context menu (cancel, move to front/back, set recurring)
- Drag → reorder
- Double-click current task → cancel immediately

---

## Urgency and calm: two UI registers

The UI shifts between peaceful management and survival without jarring transitions. Five tools operate simultaneously:

**Color palette shifts:** Calm = muted blues, teals, earth tones. Danger = warm accents, amber highlights, red vignette at screen edges during combat. Reduce HUD density during safe periods (Assassin's Creed Origins hides health bars outside combat).

**Time control integration:** Calm gameplay at 2x–8x. Threats trigger auto-slow or auto-pause (configurable); pause button pulses, speed indicator turns amber. Encourage pause-assess-plan over panic (FTL philosophy).

**Audio design:** Ambient → rising tension → percussive combat. Distinct audio stingers per notification tier for severity identification by sound alone.

**HUD reconfiguration:** Manual Override combat → action bar + prominent health. Non-urgent panels auto-collapse. Post-combat: smooth transition back + "all clear" audio + automatic prompt to check issues developed during fight.

**Mental fortitude as urgency indicator:** Visible in character HUD. High = safe feeling even in dark dungeons; low = persistent tension as player watches meter creep toward behavioral-change threshold (Darkest Dungeon pattern).
