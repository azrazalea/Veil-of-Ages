# /entities/player/autonomy

## Purpose

This directory contains the autonomy configuration system for the player entity. The autonomy system is a **configuration layer** that manages which traits the player has - it does NOT implement behaviors itself. Traits handle all actual behavior (work phases, activities, navigation, etc.) using the same code as any NPC.

## Architecture

### Design Principle: Configuration, Not Behavior

The autonomy system manages trait presence and configuration on the player entity. When a rule is enabled, the corresponding trait is added to the player via TraitFactory. When disabled, the trait could be removed. This avoids duplicating behavior logic - the same `ScholarJobTrait` that could work for an NPC scholar works identically on the player.

### Application Flow

1. `Player.InitializeAutonomy()` registers default rules during initialization
2. `Player.SetHome()` calls `AutonomyConfig.Apply(player)` after home is set
3. `Apply()` iterates enabled rules:
   - If player already has the trait (e.g., from player.json), marks as applied
   - If player doesn't have it, creates via `TraitFactory` and initializes
4. Traits handle everything else through the normal trait system

### Default Configuration

| Rule ID | Trait Type | Active Phases (UI info) | Source |
|---|---|---|---|
| study_research | ScholarJobTrait | Dawn, Day | player.json |

Sleep and idle are handled by `PlayerBehaviorTrait` (in player.json), not by autonomy rules.

## Files

### AutonomyRule.cs
A single rule mapping an ID to a trait type. Contains:
- `Id` - Unique rule identifier
- `DisplayName` - For UI display
- `TraitType` - The trait class name to manage (e.g., "ScholarJobTrait")
- `Priority` - Display/evaluation ordering
- `Enabled` - Toggle without deleting
- `ActiveDuringPhases` - Informational for UI (trait defines its own phases)

### AutonomyConfig.cs
Rule list container that can apply rules to a player by adding traits:
- `AddRule()`, `RemoveRule()`, `SetEnabled()`, `ReorderRule()`
- `Apply(Being player)` - Creates and adds traits for enabled rules
- Uses `TraitFactory.CreateTrait()` for trait creation
- Tracks which rules have been applied to avoid duplicates

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - Entity reference
- `VeilOfAges.Entities.Traits` - TraitFactory, TraitConfiguration, HomeTrait
- `VeilOfAges.Entities.Beings` - TraitDefinition
- `VeilOfAges.Core.Lib` - GameTime, DayPhaseType, Log

### Depended On By
- `VeilOfAges.Entities.Player` - Owns AutonomyConfig, calls Apply()
