# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Veil of Ages: Whispers of Kalixoria** - A 2D necromancy kingdom simulation game built with Godot 4.4 and C#. The player controls a necromancer who rises from outcast to ruler, managing both living and undead beings in a complex simulation.

## Documentation Structure

This project uses directory-level CLAUDE.md files for context injection:

```
/                           # You are here - project overview
├── .claude/context/        # Project context files (design principles, code style)
├── wiki/                   # Human-readable documentation (GitHub wiki submodule)
├── godot-docs/             # Official Godot documentation (submodule)
├── assets/                 # Game assets (sprites, fonts) - see assets/CLAUDE.md
├── core/                   # Game controller, input, time system, UI
│   ├── lib/               # PathFinder, GameTime, utilities
│   └── ui/dialogue/       # Dialogue system and commands
├── entities/              # Entity-Component-Trait system
│   ├── actions/           # IdleAction, MoveAction, etc.
│   ├── being_services/    # Perception, Needs, Movement controllers
│   ├── beings/            # Concrete entity types (human, undead)
│   ├── building/          # Building system and tile resources
│   ├── needs/             # Need system and satisfaction strategies
│   ├── sensory/           # Perception and observation system
│   └── traits/            # Modular behavior components
├── world/                 # World management and generation
│   ├── generation/        # Procedural village/terrain generation
│   └── grid_systems/      # Grid layer implementations
├── resources/             # JSON data files (buildings, tiles, materials)
├── kingdom/               # (Placeholder) Kingdom management
└── magic/                 # (Placeholder) Necromancy system
```

**Every directory has a CLAUDE.md file** providing:
- Purpose and responsibility of that module
- File-by-file documentation with key classes and methods
- Important notes (threading, patterns, gotchas)
- Dependencies (what it uses and what uses it)
- "Creating New X" guides for adding new entities, traits, actions, etc.

Always read the relevant directory's CLAUDE.md before modifying code in that area.

## Development Commands

**Only rebuild when C# code changes.** JSON, asset, and resource-only changes do NOT require a build — Godot loads those at runtime.

```bash
# Build the C# solution (only needed for C# changes)
dotnet build
```

### Export
- Use Godot Editor: Project → Export → Windows Desktop
- Export path: `./Veil of Ages.exe`

## Architecture Overview

### Core Systems

1. **Entity-Component-Trait System** (`/entities/`)
   - `Being.cs`: Abstract base for all living/undead entities
   - Traits provide modular behaviors (composition over inheritance)
   - Activity-Priority System for AI task management
   - Need-based decision making with strategy pattern
   - Multi-threaded entity thinking via `EntityThinkingSystem`

2. **World Management** (`/world/`)
   - Grid-based tile system with 32x32 pixel tiles
   - Procedural village generation with buildings and entities
   - `GridArea` manages discrete world regions with pathfinding

3. **Time Simulation** (`/core/lib/GameTime.cs`)
   - Custom base-56 calendar (14 hours/day, 28 days/month, 13 months/year)
   - Simulation runs at 8 ticks/second at normal speed
   - 36.8 game seconds per real second

4. **Building System** (`/entities/building/`)
   - JSON templates define building layouts
   - Three-layer tile resources: Atlases → Materials → Definitions
   - TileResourceManager handles loading and creation

5. **UI Systems** (`/core/ui/`)
   - Dialogue tree system with command integration
   - Location selection for movement commands
   - HUD with time controls

### Key Design Patterns

- **Data-Driven Design**: JSON files in `/resources/` define buildings, tiles, and materials
- **Strategy Pattern**: Food acquisition varies by entity type (farms vs graveyards)
- **Observer Pattern**: Sensory system for entity perception
- **Priority Queue**: Actions sorted by priority before execution
- **Agent-Based Architecture**: Inspired by Dwarf Fortress

### Threading Model

- Entity `Think()` methods run on background threads
- Actions are queued and executed on main thread
- Godot scene tree operations use `CallDeferred()` for thread safety
- PathFinder grid modifications are main-thread-only

## Project Configuration

- **Language**: C# 12.0 with nullable reference types enabled
- **Framework**: .NET 8.0
- **Namespace**: `VeilOfAges`
- **Godot SDK**: 4.4.1

### Naming Conventions

- **Classes/Methods/Properties**: PascalCase
- **Private Fields**: _camelCase with underscore prefix
- **Constants**: ALL_CAPS with underscores
- **Parameters/Locals**: camelCase

## Key Files

| File | Description |
|------|-------------|
| `project.godot` | Godot project settings and input mappings |
| `Veil of Ages.csproj` | C# project configuration |
| `/core/GameController.cs` | Main game loop and tick processing |
| `/entities/EntityThinkingSystem.cs` | Multi-threaded AI coordinator |
| `/entities/building/TileResourceManager.cs` | Tile/atlas/material loading |
| `/world/World.cs` | World container and entity management |

## Development Notes

- The project is in early development focusing on core simulation mechanics
- No automated tests are currently configured
- Follow established C# conventions and Godot best practices
- License: Modified AGPLv3 with Commercial Platform Exception (code only)

## Project Context

Additional project context is available in:
- **`.claude/context/`**: Design principles, code style, AI improvement plans
  - `project_purpose.md`: Game vision and development phases
  - `code_style.md`: Coding conventions and patterns
  - `entity_ai_improvement_plan.md`: Planned AI enhancements

## Git Submodules

### wiki/ - Project Wiki
GitHub wiki submodule containing human-readable documentation for developers and players.
- **URL**: `https://github.com/azrazalea/Veil-of-Ages.wiki.git`
- **Contents**: Game design docs, technical docs, world lore, development guides
- **Note**: This is a separate git repository; changes require separate commits

### godot-docs/ - Godot Engine Documentation
Official Godot Engine documentation for reference when implementing engine features.
- **URL**: `git@github.com:godotengine/godot-docs.git`
- **Usage**: Search this directory when you need Godot API reference, tutorials, or best practices
- **Note**: Read-only reference; do not modify

## Current Work: Pathfinding Investigation

**READ FIRST:** `.claude/context/pathfinding_investigation.md`

This contains the full investigation with:
- Core axioms (NO GOD KNOWLEDGE, pathfinding on think thread, etc.)
- Confirmed bugs with file:line references
- Phased TODO list

**Tasks #18-26 are active** - use `TaskList` to see them, `TaskGet` for details.

**USE SUBAGENTS HEAVILY** - spawn Task agents for parallel work.

## Working with This Codebase

When making changes:
1. Read the relevant directory CLAUDE.md for context
2. Build with `dotnet build` to verify compilation
3. **Test changes yourself** using the debug server - read `/core/debug/CLAUDE.md` for full documentation on starting/stopping the game, reading logs, and using the HTTP API to inspect game state
4. Check logs for errors after testing - do not ask the user to test for you

## Using Subagents for Implementation

**Use subagents (Task tool) liberally** for complex implementations to preserve main conversation context:

- **When to use subagents**:
  - Creating new files (classes, JSON definitions, documentation)
  - Implementing self-contained features with clear requirements
  - Refactoring existing code with well-defined scope
  - Any task that can be described in a focused prompt

- **Parallel execution**: Launch multiple subagents simultaneously for independent tasks
  - Example: Create ItemDefinition.cs, Item.cs, and JSON files in parallel

- **Provide context**: Give subagents enough information to work independently:
  - Reference existing patterns (e.g., "follow TileResourceManager pattern")
  - Specify namespaces, file paths, and key dependencies
  - Include code snippets or class structures when helpful

- **Benefits**:
  - Saves main conversation context for coordination and decision-making
  - Subagents can read files and understand patterns themselves
  - Multiple files created in parallel speeds up implementation
