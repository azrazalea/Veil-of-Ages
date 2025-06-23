# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Veil of Ages: Whispers of Kalixoria** - A 2D necromancy kingdom simulation game built with Godot 4.4 and C#. The player controls a necromancer who rises from outcast to ruler, managing both living and undead beings in a complex simulation.

## Development Commands

**Important**: When you need to test, compile, or run any code, ask the user to do it for you rather than attempting to run commands directly.

### Common Commands (for user reference)
```bash
# Build the C# solution
dotnet build

# Build with specific configuration
dotnet build -c Debug
dotnet build -c ExportRelease
```

### Running the Game
- **From Godot Editor**: Open project.godot in Godot 4.4+, press F5
- **Main scene**: Configured as `uid://ba8h7co1n3v3u`

### Export
- Use Godot Editor: Project → Export → Windows Desktop
- Export path: `./Veil of Ages.exe`

## Architecture Overview

### Core Systems

1. **Entity-Component-Trait System** (`/entities/`)
   - `Being.cs`: Base class for all living/undead entities
   - Traits provide modular behaviors (`/entities/traits/`)
   - Activity-Priority System for AI task management (`/entities/actions/`)
   - Need-based decision making (`/entities/needs/`)

2. **World Management** (`/world/`)
   - Grid-based tile system
   - Procedural village generation
   - Material-based terrain properties

3. **Time Simulation** (`/core/lib/`)
   - Complex time system with controllable speed
   - Activity scheduling and execution
   - Multi-threaded simulation support

4. **UI Systems** (`/core/ui/`)
   - Dialogue tree system
   - Command-based entity interactions
   - HUD with time controls

### Key Design Patterns

- **Data-Driven Design**: JSON files in `/resources/` define buildings, tiles, and materials
- **Observer Pattern**: Sensory system for entity perception
- **Priority Queue**: Activity management similar to RimWorld
- **Agent-Based Architecture**: Inspired by Dwarf Fortress

### Project Configuration

- **Language**: C# 12.0 with nullable reference types enabled
- **Framework**: .NET 8.0
- **Namespace**: `VeilOfAges`
- **Godot SDK**: 4.4.1

### Important Files

- `project.godot`: Godot project settings and input mappings
- `Veil of Ages.csproj`: C# project configuration
- `/resources/`: JSON data files for game content
- `/core/GameController.cs`: Main game loop and state management

## Development Notes

- The project is in early development focusing on core simulation mechanics
- No automated tests are currently configured
- Follow established C# conventions and Godot best practices
- License: Modified AGPLv3 with Commercial Platform Exception (code only)
- Minifantasy assets are separately licensed and not redistributable

## Serena Memories

The `.serena` folder contains important project context and previous work. Always read these memories first when starting work to understand:
- Previous design decisions and patterns
- Building system architecture and template structure
- Atlas creation workflows
- Existing integrations and dependencies

## Working with This Codebase

When making changes:
1. Ask the user to test any code modifications
2. Request compilation to verify syntax and type errors
3. Ask for the game to be run to test functionality
4. When in doubt about behavior, ask the user to verify in Godot Editor