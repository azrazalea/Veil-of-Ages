# Tile System Implementation

## Overview
The tile system has been restructured to use a more data-driven approach with JSON resources for tile types, materials, and atlas sources. This allows for greater flexibility in creating building tiles and reusing assets.

## New Classes

### TileMaterialDefinition
- Represents a material definition that can be applied to any tile type
- Properties include durability modifier and sensory modifiers
- Loaded from JSON resources in resources/tiles/materials/

### TileDefinition
- Represents a tile type (wall, floor, door, etc.) without specifying the material
- Contains default properties like walkability, durability, and atlas coordinates
- Loaded from JSON resources in resources/tiles/definitions/

### TileAtlasSourceDefinition
- Defines an atlas source for tiles, including texture path, tile size, and spacing
- Loaded from JSON resources in resources/tiles/atlases/

### TileResourceManager
- Singleton responsible for loading and managing all tile resources
- Handles creation of building tiles from tile definitions and materials
- Sets up TileSets with all required atlas sources for TileMaps

## Modified Classes

### Building
- Updated to use the TileResourceManager for initializing its TileMap
- Added method to properly initialize and setup the tile resources
- Added method to create building tiles from a template using the resource system

## Resources Structure

### Directory Layout
- resources/tiles/materials/ - Contains material definitions (wood.json, stone.json, etc.)
- resources/tiles/definitions/ - Contains tile type definitions (wall.json, floor.json, door.json, etc.)
- resources/tiles/atlases/ - Contains atlas source definitions (buildings_main.json, etc.)

### Resource Naming
- Material IDs are simple lowercase names (wood, stone, metal)
- Tile definition IDs are type names (wall, floor, door)
- When creating a tile, we combine a tile definition with a material

## Implementation Details

### Tile Creation Process
1. The Building class now loads its template as before
2. For each tile in the template, it uses TileResourceManager to create a building tile
3. The resource manager combines the tile type with the material to create a properly configured tile
4. This allows the same tile definition to be used with different materials
5. The visual appearance can be determined by the material and tile type combination

### Tilemap Optimization
- Atlas sources are shared across all tilemaps using the same source ID
- Textures are only loaded once and reused
- This reduces memory usage and improves performance

## Future Improvements
- Add support for material-specific atlas coordinates
- Implement tile variants based on surrounding tiles
- Add support for animated tiles
- Develop a visual editor for creating building templates