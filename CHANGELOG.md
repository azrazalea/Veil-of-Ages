# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Tag-based item taking in TakeFromStorageActivity (resolves tag to item ID after arriving)
- Service locator (`Services.cs`) for decoupled access to GameController and Player
- Event bus (`GameEvents.cs`) with UITickFired, SimulationPauseChanged, TimeScaleChanged, CommandQueueChanged, AutomationToggled, DialogueStateChanged events
- Programmatic UI theme (`NecromancerTheme.cs`) with dark necromancer palette
- Top bar panel with location name, date, time of day, and clickable speed controls
- Character panel showing player name, current activity, and MANUAL/AUTO automation indicator
- Needs panel showing 2-3 most critical needs with trend arrows and color-coded bars (hides when all satisfied)
- Command queue panel with pooled labels and gold-highlighted current command
- CanvasLayer stacking: UILayer (10), ModalLayer (30), TooltipLayer (100)
- Localized area names (`AreaDisplayName` on GridArea)
- Localized command display names for all 13 command types
- Translation keys for HUD strings (automation indicator, command queue, area names)

### Fixed
- Cross-area navigation for all activities: studying, working, eating, baking, hiding, and distribution rounds now navigate across area boundaries
- Entities in cellar can now navigate back to village for any activity (eating, studying, working, etc.)

### Changed
- PathFinder handles cross-area navigation internally; navigation activities work seamlessly across areas without special wrappers
- CheckStorageActivity and TakeFromStorageActivity simplified from 3 phases to 2 (PathFinder handles cross-area routing)
- ConsumeItemActivity refactored to use TakeFromStorageActivity for cross-area food fetching
- DistributorRoundActivity simplified from 10 to 8 states using CheckStorageActivity
- FetchCorpseCommand uses SharedKnowledge building lookup instead of WorldNavigator (BDI-compliant)
- HUD refactored: self-contained panels subscribe to GameEvents instead of being updated by PlayerInputController
- PlayerInputController stripped to pure input handling (~485 lines); uses Services instead of GetNode, `_UnhandledInput` instead of `_Input`
- Dialogue converted from CanvasLayer to Control, parented under ModalLayer
- UI panels use event-driven updates (UITickFired every 2 sim ticks) instead of per-frame _PhysicsProcess

### Removed
- NavigateToBuildingActivity (replaced by cross-area capable GoToBuildingActivity)
- GoToWorldPositionActivity (PathFinder handles cross-area routing internally)
- NavigationHelper utility (no longer needed; use GoToBuildingActivity/GoToFacilityActivity directly)

### Added
- Localization infrastructure with PO/Gettext translation system (`locale/en.po`)
- Static localization helper `L.cs` for non-Node classes with caching
- Localized all static UI strings (HUD labels, time descriptions, need status)
- Localized dialogue system (options, context menu, facility interactions)
- Localized all activity display names (47 translation keys)
- Added `LocalizedName`/`LocalizedDescription` to ItemDefinition, SkillDefinition, and BeingDefinition
- Translation key extraction tool (`tools/generate_pot.py`)
- GridFab building template format: visual grid-based building layouts replacing coordinate-heavy JSON (all 7 building types converted)
- Shared palette system for building tile alias definitions with palette inheritance (base → culture-specific)
- Urizen black-background atlas variant (`urizen_black`) for solid wall tiles
- Explicit ground-layer dirt tiles under wood floors in house templates (visible through transparent floor tiles)
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
- Wall tiles now use Urizen black-background atlas (`urizen_black`) instead of transparent with blue tint hack
- Floor tiles now use proper Urizen transparent-background atlas for layered rendering
- Oven decoration now uses custom stone_hearth sprite instead of Kenney placeholder
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
- Blue tint hack (`DefaultTint: #8888AA`) from wall tiles — no longer needed with proper black-bg atlas
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
