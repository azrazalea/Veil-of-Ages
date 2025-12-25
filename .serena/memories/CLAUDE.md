# Serena Memories Directory (Legacy)

## Purpose

Contains project knowledge files created during development with Serena AI assistant. These files document important design decisions, implementation details, and project context.

## Files

### Project Context
| File | Description |
|------|-------------|
| **project_purpose.md** | Core game vision: 2D necromancer kingdom sim with living/undead coexistence, development phases 1-7 |
| **tech_stack.md** | Tech overview: Godot 4.4.1, .NET 8.0, C# 12, directory structure |
| **code_style.md** | Conventions: naming (PascalCase classes, _camelCase fields), threading notes, Godot patterns |

### System Documentation
| File | Description |
|------|-------------|
| **building_system_rework_plan.md** | Building architecture: BuildingTile class, JSON templates, room detection plans, roof system, z-levels |
| **tile_system_implementation.md** | Tile system: TileMaterialDefinition, TileDefinition, TileAtlasSourceDefinition, TileResourceManager |
| **villager_entity_analysis.md** | Villager AI: state machine (IdleAtHome/IdleAtSquare/VisitingBuilding), need system, improvement directions |
| **entity_ai_improvement_plan.md** | AI roadmap: planned improvements to entity behavior and decision-making |

### Development Aids
| File | Description |
|------|-------------|
| **suggested_commands.md** | Common dev commands for building and running the project |
| **task_completion_checklist.md** | QA checklist for verifying changes |
| **serena_replace_lines_usage.md** | Serena-specific tool docs (obsolete) |

## Important Notes

- These files contain **authoritative design decisions** that should be respected
- The building and tile system docs reflect the **current implementation**
- Villager analysis shows the **current AI state machine** architecture
- Code style conventions are **actively followed** in the codebase

## Migration Status

These files should be integrated into the new CLAUDE.md documentation structure. Key information has been incorporated into:
- Root `/CLAUDE.md` - High-level project info
- `/entities/building/CLAUDE.md` - Building system details
- `/resources/tiles/CLAUDE.md` - Tile system details
- Individual entity documentation files
