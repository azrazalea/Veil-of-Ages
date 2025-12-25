# Building System Long-Term Roadmap

Future planned features for the building system. These are ideas for enhancement beyond the current implementation.

## Room Detection System
- Implement flood-fill algorithm to detect enclosed spaces
- Room class with bounding box, contained tiles, purpose/function
- Room quality calculations based on contents
- Room assignment for activities

## Building Construction/Modification
- Custom tile-by-tile construction
- Building modification (adding/removing/changing tiles)
- Construction requirements and validation
- Building upgrades and repairs

## Roof System
- Additional layer of tiles representing the roof
- Conditional visibility based on line of sight
- Different roof types and materials
- Visual fading when player has LOS into the building

**Implementation Thoughts:**
- Roof tiles should be stored as a separate collection within the Building class
- Each roof tile would have properties like type, material, transparency
- When an entity has LOS into a building (through a door/window or by being inside), the roof tiles over visible areas would become transparent or hidden
- Will need a robust vision/LOS system to determine when tiles inside buildings are visible
- Could use alpha blending for a gradual fade effect rather than binary visibility
- Different lighting conditions inside vs. outside buildings when roof is present

## Z-Levels System
- Support for multi-story buildings
- Vertical movement between levels (stairs, ladders, etc.)
- 3D representation in a 2D world
- Integration with pathfinding and vision systems

**Implementation Thoughts:**
- Could implement as a layered approach where each z-level is its own grid
- Special tiles (stairs, elevators) would connect between layers
- The core Grid system would need to be extended to include z-coordinate
- Entities would need more complex pathfinding to navigate between levels
- Camera system might need updates to handle focusing on specific z-levels
- Could render higher levels with increasing transparency
- Might need specialized UI elements to indicate current level and level transitions

## Special Building Interactions
- Special handling for doors/entrances
- Furniture and functional objects within buildings
- Building-specific activities and behaviors
- Environmental effects (temperature, lighting, etc.)

## Technical Considerations
- Maintain backward compatibility where possible
- Ensure performance with potentially many individual building tiles
- Consider multi-threading for building-related calculations
- Plan for serialization/deserialization for save/load functionality
- Design with moddability in mind
