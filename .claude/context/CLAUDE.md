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

## What NOT to Put Here

- Implementation details that belong in directory CLAUDE.md files
- Human-readable documentation (use /wiki/ submodule instead)
- Temporary notes or task lists

## Related Locations

- **Directory CLAUDE.md files**: Per-directory context for each code module
- **/wiki/**: Human-readable documentation (GitHub wiki submodule)
- **/CLAUDE.md**: Root project instructions and overview
