# /entities/terrain

## Purpose

This directory contains terrain entity implementations. Terrain entities are static world objects like trees, rocks, and other environmental features that occupy grid space and can be interacted with.

## Subdirectories

### /tree
Contains the Tree entity implementation for forest vegetation.

## Architecture Notes

### Terrain vs Buildings
Terrain entities differ from buildings in several ways:
- Simpler structure (no tile composition)
- Often single-purpose (resource node, obstacle)
- No occupancy or entrance system
- May be harvestable/destructible

### Grid Integration
Terrain entities:
- Occupy grid cells based on their size
- Register with the grid area on initialization
- Unregister on removal (`_ExitTree`)
- Block pathfinding through occupied cells

## Dependencies

### Depends On
- `VeilOfAges.Grid` - Area, Utils
- Godot Node2D

### Depended On By
- World generation systems
- Resource gathering systems (future)
