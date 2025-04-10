# Tech Stack

## Development Platform
- Godot Engine 4.4.1
- .NET 8.0 (C# scripting)
- Visual Studio Code as the primary IDE

## Project Structure
The codebase is organized into several main directories:
- **core**: Contains the game controller, player input controller, and core systems
  - **lib**: Core utility classes like GameTime, Pathfinder
  - **main**: Core game logic
  - **ui**: User interface components, including dialogue system
- **entities**: Entity-related classes
  - **actions**: Action definitions for entities
  - **beings**: Different being types (human, undead)
  - **being_services**: Services for entity behavior (needs, perception)
  - **building**: Building-related classes
  - **needs**: Need system and strategies
  - **player**: Player-specific classes
  - **sensory**: Perception and observation systems
  - **terrain**: Terrain entity types
  - **traits**: Trait components for entities
- **world**: World-related classes
  - **generation**: World/village generation
  - **grid_systems**: Grid implementation
- **kingdom**: Kingdom management (currently empty)
- **magic**: Magic system (currently empty)
- **assets**: Game assets (fonts, graphics)

## Key Technologies and Features
- Entity-Component-System inspired design
- Multi-threaded entity simulation
- Modular component-based architecture
- Event-based communication system
- Threaded simulation that continues during UI interactions
- Data-driven configuration approach
- Godot's scene system for modular design
- Behavior tree system for autonomy profiles