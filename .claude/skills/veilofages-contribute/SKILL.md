---
name: veilofages-contribute
description: Guide for contributing code to Veil of Ages. Use when making changes, fixing bugs, adding features, or preparing commits. Triggers on tasks like "fix this bug", "add a new system", "commit these changes", or any code change to the codebase.
---

# Contributing to Veil of Ages

## Before Every Commit

1. **Build**: `dotnet build` (only needed if C# files changed — JSON/asset changes don't need a build)
2. **Test in-game**: Either test changes yourself using the debug server (see `/core/debug/CLAUDE.md`), or ask the user to test and confirm the changes work before committing. Don't commit untested code without the user's sign-off.
3. **Update CHANGELOG.md**: Add entries under `[Unreleased]` using Keep a Changelog categories (Added, Changed, Fixed, Removed). Only for user-facing changes.
4. **Update CLAUDE.md**: If you added/removed files in a directory, update that directory's CLAUDE.md to reflect the change

## Architecture Rules

- **Entity Think() on background threads**: Never access Godot scene tree from Think(). Use CallDeferred for main-thread operations.
- **Data-driven design**: Buildings, items, skills, tiles, and reactions are defined in JSON under `/resources/`. Prefer JSON definitions over hardcoded values.
- **Trait-based composition**: Entity behaviors come from traits, not inheritance. Add new behaviors as new trait classes.
- **Priority queue for actions**: Commands priority -1, activities priority 0, traits priority 0-1. Lower number = higher priority.
- **Sub-activities created lazily**: Null check → create → RunSubActivity pattern.
- **Navigation activities**: Use _stuckTicks counter, Fail() after 50 ticks. Don't use RunSubActivity — directly manage PathFinder.

## Code Style

- **Classes/Methods/Properties**: PascalCase
- **Private Fields**: _camelCase with underscore prefix
- **Constants**: ALL_CAPS with underscores
- **Parameters/Locals**: camelCase
- Follow existing patterns in the codebase
- Don't add docstrings, comments, or type annotations to code you didn't change

## Documentation Standards

This project uses directory-level `CLAUDE.md` files. Every major directory has one documenting its purpose, files, patterns, and dependencies. Update the relevant CLAUDE.md when adding new files or modules.

## Commit Process

1. Stage specific files (avoid `git add -A` which may catch sensitive files)
2. Write a clear commit message summarizing the "why"
3. Ensure CHANGELOG.md is updated for user-facing changes
