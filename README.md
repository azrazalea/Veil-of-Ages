# Veil of Ages

![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)
![Godot: 4.4](https://img.shields.io/badge/Godot-4.4-blue)
![Status: Early Development](https://img.shields.io/badge/Status-Early%20Development-yellow)

A 2D tile-based simulation game where you build and manage a unique fantasy kingdom. Initially focused on necromancy, allowing you to lead a settlement with both living and undead citizens, but designed to expand with additional gameplay paths in future updates.

## 🌟 Features

- **Modular Faction System**: Initially focused on necromancy, with plans to add nature-based, elemental, and other faction types in future updates.
- **Morally Nuanced Gameplay**: Make meaningful choices that shape your character and kingdom without forced alignment.
- **Mixed Population Management**: Maintain a kingdom with diverse citizens coexisting based on your faction type.
- **Strategy & Base Building**: Construct and upgrade buildings, manage resources, and expand your domain.
- **Character Development**: Progress your character with skills and specializations through an RPG-like system.
- **Personality System**: Followers have distinct personalities, needs, and desires - not just mindless pawns.
- **Multi-Path Progression**: Research different specializations with unique abilities and effects.

## 📋 Requirements

- [Godot 4.4+](https://godotengine.org/download) with Mono/.NET support 
- .NET SDK 8.0 or later
- Visual Studio Code (recommended) with the following extensions:
  - C# Dev Kit
  - Godot Tools

## 🚀 Getting Started

### Installation

1. Clone the repository (note: assets are not included in the public repository):
   ```
   git clone https://github.com/azrazalea/Veil-of-Ages.git
   ```

2. Open the project in Godot 4.4+:
   - Launch Godot Engine
   - Select "Import Project"
   - Navigate to the cloned repository folder
   - Open the `project.godot` file

### Building From Source

1. Build the C# solution:
   ```
   dotnet build
   ```

2. Run the game from Godot Editor or build it:
   - Press F5 in the Godot Editor to run
   - Or use Project > Export to build for your target platform

## 🎮 Gameplay Guide

### Main Game Modes

- **Simulation Mode**: Time advances automatically while you watch your kingdom develop
- **Manual Control Mode**: Take direct control of your character to explore, interact, and build

### Key Controls

- WASD / Arrow Keys: Move character
- E: Interact with objects and characters
- Space: Pause/resume simulation
- Tab: Speed up/slow down time

## 🧪 Development 

### Project Structure

- `/core/` - Core game systems and controllers
- `/entities/` - All entity-related code (beings, buildings, etc.)
- `/world/` - World generation and management
- `/assets/` - Game assets (not included in public repository)

### Architecture

The game uses a component-based architecture with these key systems:

- **World System**: Manages the tile-based map, zones, and resources
- **Entity System**: Handles all entities including characters and buildings
- **Turn Management**: Controls simulation ticks and action priority
- **Personality System**: Governs traits, relationships, and memory
- **Magic System**: Handles spellcasting, research, and magical effects
- **Faction System**: Provides modular framework for different gameplay styles and kingdom types

While the initial release focuses on necromancy, the architecture is designed to support multiple faction types. This modular approach will allow the addition of nature-focused druids, elemental mages, and other faction types in future updates without requiring major code restructuring.

## 🤝 Contributing

Contributions are welcome! Please note:

1. Make sure to follow the coding style and conventions used in the project
2. Create a branch for your feature or fix
3. Open a pull request with a detailed description
4. Be aware that assets are not included in the public repository and need to be obtained separately

## 📜 License

- **Code**: Licensed under [GNU AGPL-3.0](LICENSE.code)
- **Assets**: Not included in the public repository. The Minifantasy assets used in development are licensed separately and are not redistributable.

## 📬 Contact

- Project Lead: [Your Name](mailto:your.email@example.com)
- GitHub Issues: [Issue Tracker](https://github.com/yourusername/veil-of-ages/issues)

## 🙏 Acknowledgments

- Krishna Palacio for the Minifantasy asset pack used in development
- The Godot Engine community for their invaluable resources and support
- Inspiration from games like KeeperRL, Rimworld, and Dwarf Fortress
