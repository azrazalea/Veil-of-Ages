# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Custom game logo (hourglass with magic energy) as application icon, boot splash, and README branding
- Claude Code skills for contributing (`/veilofages-contribute`) and releasing (`/veilofages-release`)
- Runtime sprite layer API for dynamic equipment visuals (SetSpriteLayer, RemoveSpriteLayer)
- Updated player default appearance with boots, gloves, and new sprite selections
- Cellar building with dirt walls, floors, and ladder decoration
- Ladder and necromancy altar decoration definitions
- Dirt wall variant for wall tiles (DCSS stone_2_brown0)
- ISubActivityRunner interface for shared sub-activity support between activities and commands
- ScheduleTrait: unified sleep/scheduling for all living entities (players and NPCs)
- HomeTrait.IsEntityAtHome() method for centralized at-home detection

### Changed
- Entity sprites now use static Sprite2D + AtlasTexture instead of AnimatedSprite2D + SpriteFrames
- Entity sprite definitions moved from `resources/entities/animations/` to simpler format in `resources/entities/sprites/`
- Being.SpriteLayers is now a named dictionary for O(1) layer lookup
- Cellar generation now uses JSON building template instead of programmatic creation
- FetchResourceActivity now supports cross-area navigation via WorldNavigator
- FetchCorpseCommand simplified from 7-phase state machine to thin FetchResourceActivity wrapper
- EntityCommand now supports RunSubActivity pattern for composing activities
- Sleep logic unified into ScheduleTrait: replaces duplicated sleep handling in PlayerBehaviorTrait and VillagerTrait
- VillagerTrait no longer manages sleep (handled by ScheduleTrait); Sleeping state removed
- JobTrait.IsWorkHours() is now public (was protected) for cross-trait querying
- JobTrait: added virtual IsWorkActivity() for jobs with multiple activity types
- Player entity uses ScheduleTrait with allowNightWork=true instead of PlayerBehaviorTrait
- NecromancyStudyJobTrait: cached facility lookup with TTL to reduce GC pressure

### Removed
- Old entity animation JSON files (replaced by sprite definitions)
- Unused oven_idle.json decoration animation
- Dead code: SleepActivity.WakeRequested property (was never set by anything)
- PlayerBehaviorTrait (replaced by ScheduleTrait)

### Fixed
- Release archives no longer contain a redundant wrapper folder when extracted on Windows
- Entities now wake from sleep when any non-energy need reaches critical level (prevents sleeping through starvation)
- Sleep oscillation: entities no longer re-sleep immediately after waking (MIN_AWAKE_TICKS cooldown)
- Night workers (necromancer) no longer forced to sleep when they have an active night job
- Villagers no longer sleep based solely on Night phase; sleep now considers energy level
- GoToBuildingActivity for non-sleep purposes no longer confused with "going home to sleep"
- ScheduleTrait: home destroyed mid-sleep no longer causes crash (validates building before access)
- NecromancyStudyJobTrait: WorkOnOrderActivity now properly detected as "already working" (prevents redundant activity creation)
- NecromancyStudyJobTrait: eliminated double FindFacilityOfType lookup per tick

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
