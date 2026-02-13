# /entities/terrain

## Purpose

This directory contains terrain entity implementations. Terrain entities are static world objects like trees, rocks, and other environmental features that occupy grid space and can be interacted with.

## Subdirectories

### /tree
Contains the Tree entity implementation for forest vegetation. Note: Tree no longer uses a dedicated scene file - it creates its sprite programmatically from the Kenney atlas.

## Architecture Notes

### Terrain vs Buildings
Terrain entities differ from buildings in several ways:
- Simpler structure (no tile composition)
- Often single-purpose (resource node, obstacle)
- No occupancy or entrance system
- May be harvestable/destructible
- Create sprites programmatically rather than from scenes

### Grid Integration
Terrain entities:
- Occupy grid cells based on their size
- Register with the grid area on initialization
- Unregister on removal (`_ExitTree`)
- Block pathfinding through occupied cells

### Sprite Creation
Terrain entities create their visual representation programmatically:
- No PackedScene required
- Sprites loaded from atlas definitions at runtime
- Uses TileResourceManager to access atlas textures
- GridGenerator creates instances directly without scene files

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Area, Utils
- `VeilOfAges.Entities.Building.TileResourceManager` - Atlas access for sprite creation
- Godot Node2D and Sprite2D

### Depended On By
- World generation systems (`/world/generation/GridGenerator.cs`)
- Resource gathering systems (future)
