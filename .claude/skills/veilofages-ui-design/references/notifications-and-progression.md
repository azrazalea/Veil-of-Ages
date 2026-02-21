# Notifications and Progressive Complexity — Design Detail

## Table of contents

- Notification tier details
- Information-flow integration
- Notification customization
- Alert fatigue prevention
- Progressive complexity stages
- Onboarding philosophy
- UI complexity slider

---

## Notification tier details

Alert fatigue is the primary failure mode — RimWorld, Dwarf Fortress, CK3, and Stellaris all struggle with notification overload in late game. Ruthless prioritization is essential.

**Tier 1 — Critical (Red):** Character near death, kingdom under direct attack, undead rebellion. Force pause regardless of speed, modal popup with action options, alarm audio cue. Must be rare — more than once per game-hour means miscalibration.

**Tier 2 — Urgent (Amber):** Needs at dangerous levels, important visitor/emissary, major building completion, familiar returning with critical report. Auto-pause at 4x/8x speed, prominent banner with audio chime at any speed. Clickable to center camera on relevant location/entity.

**Tier 3 — Important (Blue):** New entity joins, seasonal change, resource milestone, non-critical familiar report. Toast in feed, gentle audio pip, no pause. At high speeds, batch: `3 new settlers arrived this season` rather than three separate notifications.

**Tier 4 — Informational (Grey):** Routine social interactions, minor task completions, ambient narrative events. Log entry only, accessible in notification history but not surfaced in active feed. Stores as a dated, searchable narrative chronicle.

**Tier 5 — Montage (Gold):** During multi-year time skips, all events accumulate into a structured summary report presented when montage ends or a decision point interrupts. Groups by category (population, military, resources, narrative) with critical events highlighted.

---

## Information-flow integration

Notifications from distant areas arrive with narrative framing and delay — never as real-time alerts:

> A messenger reports that Province X was attacked [2 days ago]. Details are uncertain.

Familiar reports arrive when the familiar returns, not when the event happens. The notification system itself embodies the information-flow mechanic — a gameplay-relevant information channel, not just UI convenience.

---

## Notification customization

Non-negotiable requirement. As kingdoms grow, players must control notification filtering. Provide per-category toggles:
- Auto-pause behavior on/off per tier
- Audio on/off per tier
- Feed visibility on/off per category

Adopt Oxygen Not Included's Red Alert mode — a global emergency toggle near time controls letting players manually escalate alert state when anticipating danger.

---

## Alert fatigue prevention

- Cap visible toast notifications at 4–5, queue the rest
- Batch similar events at high time speeds
- Suppress informational noise at 4x+ speeds
- Maintain strict tiers — surface only what requires attention
- Multi-channel signaling (FTL pattern): color + audio + animation simultaneously for critical events

---

## Progressive complexity stages

Stage-gate UI elements to appear only when gameplay systems become relevant. Never front-load complexity.

**Stage 1 — Solo necromancer (1–3 entities):** Character HUD only (portrait, needs, health, magic), activity queue, game world. No entity list, no kingdom overview, no minimap. Player learns all four control tiers with their own character.

**Stage 2 — Small coven (4–10 entities):** Entity list panel unlocks. Familiar system activates with tutorial. Notification system appears. Minimap becomes available. Dialogue-based commands. No standing orders — every command is personal.

**Stage 3 — Growing settlement (11–25 entities):** Standing orders unlock (group policies). Kingdom overview panel appears. Supervisor appointments become available. Notification batching activates. Information staleness shows on distant entities.

**Stage 4 — Kingdom (25+ entities):** Full intelligence briefing layout. Chain-of-command UI (orders flow through leaders). Exception-based management: UI highlights only problems and decisions. Statistical summaries replace individual tracking below supervisor level. Multiple familiars for distant territories.

At each stage, the player spends most time at the appropriate abstraction level — micro in Stage 1, increasingly macro by Stage 4 — with the UI gently pushing toward delegation.

---

## Onboarding philosophy

Mirror the game's development phases. Introduce one mechanic at a time with practice time between introductions (cognitive load theory).

- RimWorld's "Learning Helper" (contextual tips triggered by encounters) is the right model
- CK3's approach (tutorial embedded in the UI, pointing at exact locations) is even better

**Empty state design:** Where a panel will eventually appear, show a subtle locked placeholder: a locked icon labeled "Kingdom Overview" that unlocks at Stage 3. Creates anticipation and prevents surprise when new UI elements appear.

---

## UI complexity slider

Settings option: Minimal → Standard → Full.

- **Minimal:** Tier-1 information only, aggressive auto-management
- **Standard:** Recommended progressive disclosure
- **Full:** Every panel, number, and breakdown available immediately

Lets experienced players bypass progressive disclosure while protecting newcomers.
