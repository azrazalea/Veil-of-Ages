# .claude Directory

## Purpose

Configuration and context directory for Claude Code (Anthropic's CLI tool). Contains settings, permission configurations, and project context files.

## Structure

```
.claude/
├── settings.local.json   # Local permission settings
└── context/              # Project context files (migrated from .serena)
    ├── CLAUDE.md         # Context directory documentation
    ├── project_purpose.md    # Core game vision and design
    ├── code_style.md         # Coding conventions
    └── entity_ai_improvement_plan.md  # AI roadmap
```

## Files

### settings.local.json
Local permission settings for Claude Code. Currently configured with:
- **Allowed commands**: `find` and `ls` bash commands are pre-approved
- **Denied commands**: None explicitly denied

### context/
Contains project context files that provide high-level guidance:
- **project_purpose.md**: Game vision, design philosophy, development phases
- **code_style.md**: Naming conventions, threading notes, Godot patterns
- **entity_ai_improvement_plan.md**: Planned AI improvements

## Important Notes

- `settings.local.json` should typically be in `.gitignore` as it contains local preferences
- Context files are loaded automatically when working in this project
- The root `/CLAUDE.md` provides the main project instructions
- Directory-level `CLAUDE.md` files provide module-specific context

## Related Files

- `/CLAUDE.md` - Main project instructions and overview
- `/wiki/` - Human-readable documentation (GitHub wiki submodule)
- Individual directory `CLAUDE.md` files provide code context
