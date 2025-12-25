# .serena Directory (DEPRECATED)

## Purpose

This directory contains files from **Serena**, a previous AI coding assistant that was used during early development. The project has since migrated to Claude Code.

## Status

**DEPRECATED** - Content has been migrated to `.claude/context/`. This directory can be deleted.

### Migrated Files
- `project_purpose.md` -> `.claude/context/project_purpose.md`
- `code_style.md` -> `.claude/context/code_style.md`
- `entity_ai_improvement_plan.md` -> `.claude/context/entity_ai_improvement_plan.md`

### Incorporated into CLAUDE.md files
- `building_system_rework_plan.md` -> `/entities/building/CLAUDE.md`
- `tile_system_implementation.md` -> `/resources/tiles/CLAUDE.md`
- `villager_entity_analysis.md` -> `/entities/beings/human/CLAUDE.md`
- `tech_stack.md` -> Root `/CLAUDE.md`

### Can be deleted
- `cache/` - Auto-generated cache
- `serena_replace_lines_usage.md` - Serena-specific, obsolete
- `suggested_commands.md` - Outdated
- `task_completion_checklist.md` - Process-specific

## Structure

```
.serena/
├── cache/           # Cached analysis data (can be deleted)
│   └── csharp/      # C# parsing cache
└── memories/        # Project knowledge and context
    ├── project_purpose.md           # Core game vision and phases
    ├── tech_stack.md                # Technology overview
    ├── code_style.md                # Coding conventions
    ├── building_system_rework_plan.md  # Building architecture plans
    ├── tile_system_implementation.md   # Tile system documentation
    ├── villager_entity_analysis.md     # Villager AI analysis
    ├── entity_ai_improvement_plan.md   # AI improvement roadmap
    ├── suggested_commands.md           # Dev command reference
    ├── task_completion_checklist.md    # QA checklist
    └── serena_replace_lines_usage.md   # (Serena-specific, obsolete)
```

## Valuable Content

The `/memories/` subdirectory contains important project documentation that should be migrated:

| File | Content | Suggested New Location |
|------|---------|------------------------|
| project_purpose.md | Game vision, design principles, dev phases | Root README or /docs |
| tech_stack.md | Technology overview | Root CLAUDE.md |
| code_style.md | Coding conventions | Root CLAUDE.md or CONTRIBUTING.md |
| building_system_rework_plan.md | Building architecture | /entities/building/CLAUDE.md |
| tile_system_implementation.md | Tile system docs | /resources/tiles/CLAUDE.md |
| villager_entity_analysis.md | Villager AI details | /entities/beings/human/CLAUDE.md |
| entity_ai_improvement_plan.md | AI roadmap | /entities/CLAUDE.md |

## Migration Recommendation

Consider:
1. Moving architectural plans into relevant directory CLAUDE.md files
2. Consolidating project-wide docs into root CLAUDE.md or a /docs folder
3. Deleting the cache folder (auto-generated, not needed)
4. Removing Serena-specific files (serena_replace_lines_usage.md)
