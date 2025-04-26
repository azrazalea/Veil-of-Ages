# Building System Rework Plan

## Immediate Implementation (Phase 1)

1. **BuildingTile Class**
   - Create a class to represent individual tiles within a building
   - Properties: TileType (Wall, Floor, Door, Window, etc.), Material, Durability/HP, IsWalkable
   - Methods for interacting with the tile (damage, repair, etc.)
   - Reference to parent Building

2. **Building Class Modifications**
   - Change from a monolithic entity to a container of BuildingTile objects
   - Dictionary mapping grid positions to BuildingTile objects
   - Building metadata (name, purpose, owner)
   - Methods for accessing/modifying individual tiles

3. **JSON Building Template System**
   - Define a JSON format for building templates
   - Create serialization/deserialization for building templates
   - Include basic information like size, tile layout, entrances
   - Store templates in resources folder for easy access

4. **Basic Building Placement**
   - Implement system for placing predefined buildings from templates
   - Register individual tiles with the grid system for pathfinding
   - Ensure proper integration with existing systems

## Long-Term Roadmap (Future Phases)

1. **Room Detection System**
   - Implement flood-fill algorithm to detect enclosed spaces
   - Room class with bounding box, contained tiles, purpose/function
   - Room quality calculations based on contents
   - Room assignment for activities

2. **Building Construction/Modification**
   - Custom tile-by-tile construction
   - Building modification (adding/removing/changing tiles)
   - Construction requirements and validation
   - Building upgrades and repairs

3. **Roof System**
   - Additional layer of tiles representing the roof
   - Conditional visibility based on line of sight
   - Different roof types and materials
   - Visual fading when player has LOS into the building
   
   Implementation Thoughts:
   - Roof tiles should be stored as a separate collection within the Building class
   - Each roof tile would have properties like type, material, transparency
   - When an entity has LOS into a building (through a door/window or by being inside), 
     the roof tiles over visible areas would become transparent or hidden
   - Will need a robust vision/LOS system to determine when tiles inside buildings are visible
   - Could use alpha blending for a gradual fade effect rather than binary visibility
   - Different lighting conditions inside vs. outside buildings when roof is present

4. **Z-Levels System**
   - Support for multi-story buildings
   - Vertical movement between levels (stairs, ladders, etc.)
   - 3D representation in a 2D world
   - Integration with pathfinding and vision systems
   
   Implementation Thoughts:
   - Could implement as a layered approach where each z-level is its own grid
   - Special tiles (stairs, elevators) would connect between layers
   - The core Grid system would need to be extended to include z-coordinate
   - Entities would need more complex pathfinding to navigate between levels
   - Camera system might need updates to handle focusing on specific z-levels
   - Could render higher levels with increasing transparency
   - Might need specialized UI elements to indicate current level and level transitions

5. **Special Building Interactions**
   - Special handling for doors/entrances
   - Furniture and functional objects within buildings
   - Building-specific activities and behaviors
   - Environmental effects (temperature, lighting, etc.)

## Technical Implementation Considerations

- Maintain backward compatibility where possible
- Ensure performance with potentially many individual building tiles
- Consider multi-threading for building-related calculations
- Plan for serialization/deserialization for save/load functionality
- Design with moddability in mind
