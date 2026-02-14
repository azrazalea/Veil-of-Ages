# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Claude Code skills for contributing (`/veilofages-contribute`) and releasing (`/veilofages-release`)

### Fixed
- Release archives no longer contain a redundant wrapper folder when extracted on Windows

## [0.1.0] - 2026-02-13

### Added
- Procedurally generated village with roads, lots, and 6 building types (simple house, simple farm, scholar's house, graveyard, granary, well)
- Multi-threaded entity AI with trait-based composition and priority-driven action system
- Need system (hunger, energy) driving autonomous entity behavior and decision-making
- Memory and perception system — entities only know what they've personally observed
- Resource economy with farming, milling, and baking production chain
- Skill system with 5 skills (research, arcane theory, necromancy, farming, baking) and XP progression
- Day/night cycle with seasonal light variation across a custom base-56 calendar
- Time controls from pause to 25x speed
- Entity types: three villager jobs (farmer, baker, distributor), two undead types (zombies, skeletons), and player necromancer
- Cross-platform builds for Windows, Linux, and macOS via GitHub Actions
- JSON-driven data definitions for buildings, items, skills, tiles, and reactions
- Multi-grid support with building interiors (Scholar's House cellar)
- 32x32 multi-atlas tile rendering with layered visual system
- Dialogue system for NPC interaction
- Debug HTTP server for development inspection
