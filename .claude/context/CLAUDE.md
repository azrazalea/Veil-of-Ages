# Claude Context Directory

## Purpose

This directory contains project context files that are automatically loaded when Claude Code opens files from this project. These provide high-level guidance, design principles, and implementation notes.

## Files

### project_purpose.md
Core game vision and design principles:
- Game overview (2D necromancer kingdom sim)
- Core design philosophy (meaningful moral choices, living/undead coexistence)
- Development phases roadmap (7 phases from Core Mechanics to Multi-Faction)
- Unique game elements (Activity-Priority Control, mixed populations)

### code_style.md
Coding conventions and patterns:
- Naming conventions (PascalCase classes, _camelCase private fields)
- Documentation standards (XML docs for public APIs)
- Threading considerations (entity thinking is multi-threaded)
- Godot-specific patterns (Export attributes, signals)

### entity_ai_improvement_plan.md
Planned improvements to entity AI system:
- State machine enhancements
- Need system expansions
- Behavior priorities
- Memory and perception improvements

### building_system_roadmap.md
Future building system features and ideas:
- Room detection system (flood-fill, room quality)
- Building construction/modification (tile-by-tile)
- Roof system (visibility, LOS-based fading)
- Z-levels system (multi-story, vertical pathfinding)
- Special building interactions (doors, furniture, environment)

### current_work.md
**Claude's current work-in-progress and the primary source for "what to work on next".**

This file tracks:
- What Claude is currently implementing
- Design decisions made during the work
- Implementation phases and progress
- File locations and class structures
- Next steps and TODO items

**IMPORTANT**: When starting a new session or unsure what to work on, **read this file first**. It contains the prioritized next steps for the project. Update it as work progresses and when completing major milestones.

## Architecture Decisions (December 2025)

A comprehensive architecture review was completed. Key decisions and implementation plans are now documented in the relevant CLAUDE.md files:

- **LOS Implementation**: Grid-based approach, plan in `entities/being_services/CLAUDE.md`
- **Event Queue Pattern**: Alternative to Godot signals, plan in `entities/CLAUDE.md`
- **BDI Architecture**: Documented in `entities/CLAUDE.md` (intentional design)
- **Perception Improvements**: Future plans in `entities/sensory/CLAUDE.md`
- **Grid System**: Custom dicts appropriate for discrete grid game (no changes needed)
- **Godot Usage**: Custom data structures preferred over physics for this game type

## What NOT to Put Here

- Implementation details that belong in directory CLAUDE.md files
- Human-readable documentation (use /wiki/ submodule instead)
- Temporary notes or task lists

## Related Locations

- **Directory CLAUDE.md files**: Per-directory context for each code module
- **/wiki/**: Human-readable documentation (GitHub wiki submodule)
- **/CLAUDE.md**: Root project instructions and overview
