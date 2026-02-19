# Information Flow and Staleness — Design Detail

## Table of contents

- Four-state information model
- Visual degradation mechanics
- Composite field-of-view
- Intelligence journal
- NPC advisors and reliability
- Communication chain overlay
- Familiar relay UI
- Kingdom management panel
- Player annotation tools
- Entity list as knowledge inventory

---

## Four-state information model

No existing game handles information staleness, delay, and unreliability as a first-class UI concept. This is Veil of Ages' most distinctive UI innovation.

**State 1: Unknown.** Solid darkness on strategic map, empty slots in entity lists. Unexplored areas appear as rough parchment edges (Civilization VI aesthetic) — "here be dragons." Communicates: your character knows nothing about this.

**State 2: Stale (observed but outdated).** Unlike Civilization's frozen snapshot, Veil of Ages visually degrades information over time. Recently-observed areas appear nearly full-color; months-old observations fade toward sepia with question marks over counts. Entity portraits gain a translucent overlay that thickens with time since last contact. The staleness gradient is continuous, not binary.

FTL's tiered sensor system proves players find information tiers intuitive. Stellaris's intel levels (low/medium/high/maximum) show graduated knowledge feels natural. But neither makes information decay — this is Veil of Ages' pioneer mechanic.

**State 3: Reported (received through intermediary).** Information from familiars, messengers, or magic. Displays with source attribution and reliability indicator. A familiar's report is more reliable than a merchant's rumor. Visual: parchment-style frame with quill icon = "this is a report, not direct observation." Uncertain reports show ranges: `~200–300 soldiers`.

**State 4: Observed (direct line of sight).** Full color, full detail, real-time updates. The only state with exact numbers and current status. Contrast between observed and everything else must be immediately apparent.

---

## Visual degradation mechanics

- **Uncertainty halos** around last-known entity positions — circle expands over time representing "could be anywhere within this radius" (inspired by NATO Common Operational Picture systems)
- Progressive desaturation carries necromantic thematic resonance — death draining color from the world
- Into the Breach philosophy: "sacrifice cool ideas for the sake of clarity every time" — the nature of what is unknown must always be clearly communicated

---

## Composite field-of-view

Each vision source is color-tinted to communicate origin without additional UI chrome:
- **Player character's own vision**: full brightness
- **Undead familiar vision**: pale ghostly blue-green
- **Living messenger vision**: warm amber

Technical reference: Cogmind's reference-counted composite FoV system (each cell tracks how many friendly units can see it).

---

## Intelligence journal

An in-world journal replacing omniscient resource counters. Organized by topic (military threats, resource status, population, infrastructure). Each entry stores:
- Source attribution (who reported it)
- Observation date (when the event happened)
- Receipt date (when the player learned it)
- Confidence indicator

Transforms information limitations from frustration into core gameplay: the player becomes a medieval ruler assembling an intelligence picture.

---

## NPC advisors and reliability

Advisors aggregate and summarize their domains:
- Steward → daily resource summaries
- Scout captain → military threat reports
- Seneschal → population and morale

**Advisor competence visibly affects information quality:** Skilled steward = precise numbers; poor one = vague ranges. This creates a natural upgrade path where improving information infrastructure tangibly improves the UI experience.

---

## Communication chain overlay

Optional overlay mode showing lines connecting player to information sources:
- **Solid bright lines** → real-time mental links to directly controlled undead
- **Dashed lines** → periodic messenger reporting
- **Faded dotted lines** → unreliable or slow connections

Color-coded by reliability. Shows the player's intelligence network at a glance.

---

## Familiar relay UI

**Status widget** near minimap: familiar portrait, current location, status line (`Delivering orders to Farm Overseer — ETA: 4 hours`), pending messages queue. Glows when home/available; dotted path line on minimap when in transit.

**Message composition** feels like writing a dispatch: open dialogue-style interface → select recipient → order category (work assignment, military command, policy change, information request) → compose instruction → send. Familiar travels on map (visible progress), delivers, receives response, returns with narrative notification:

> Your familiar returns from the eastern farms. Overseer Maren accepts the order to increase grain production but reports insufficient laborers.

**Order lifecycle visualization:**
`Composing → Sent → In Transit → Delivered → Accepted/Refused/Conditional → In Progress → Completed/Failed`

Each state has distinct icon and color. The active orders log (right sidebar) shows all recent orders with current state. Addresses the core frustration of indirect control systems (identified in Majesty, Black & White, Dungeon Keeper).

---

## Kingdom management panel

Frame as an intelligence briefing, not a god-view dashboard. Information in categories (population, resources, military, morale), each data point with source and freshness:
- `Population: ~340 (Steward's report, 1 month ago)` vs `Population: 347 (Direct count, current)`

**Summary statistics with confidence intervals:** Where RimWorld shows `12 meals, 340 silver`, Veil of Ages shows estimates when imperfect: `Food stores: ample (Steward's assessment)` or `~200–250 units (last inventory: 2 weeks ago)`. Only direct observation yields exact numbers.

**Supervisor abstraction layer:** Selecting a supervisor shows their aggregated domain report covering all subordinate entities. At 5 entities, know everyone directly; at 50, know 5 supervisors directly and everyone else through them. Solves RimWorld's scaling problem (work priority grid unmanageable at 30+ colonists).

---

## Player annotation tools

When information is limited, players want to record their own observations (Civilization VI map tacks pattern). Provide:
- Note system for strategic map
- Mark suspected enemy positions
- Flag rumors as "trusted" or "suspicious"
- Annotate provinces with personal notes

Transforms information management from frustration into engaging gameplay layer.

---

## Entity list as knowledge inventory

The left sidebar shows NPCs the character has information about — NOT all NPCs in the kingdom. Each entry:
- Portrait, name, role, last-known status
- Freshness indicator (clock icon: green=current, amber=days old, red=weeks old, grey=long ago)

NPCs not heard from in a long time: `Farming? (2 months ago)`
