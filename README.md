# Veil of Ages

![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)
![Godot: 4.4](https://img.shields.io/badge/Godot-4.4-blue)
![Status: Early Development](https://img.shields.io/badge/Status-Early%20Development-yellow)

**Veil of Ages** is a 2D necromancy kingdom simulation where you build a realm where the living and undead coexist. 

## 🌙 Vision

Lead a necromancer's journey from outcast to ruler, creating a unique society where undead serve alongside living citizens. Make meaningful moral choices that shape your character and kingdom without forced alignment. Will you be a benevolent scholar of death magic, a practical survivalist, or a dark overlord? The choice is yours.

While the initial focus is on necromancy, the game's architecture is designed to eventually support multiple faction types, including nature-focused druids, elemental mages, and more.

## 🎮 Current Features

- **Player Character**: Control a necromancer in a reactive tile-based world
- **Undead Creation**: Command skeletons and zombies with distinct behaviors
- **Living NPCs**: Citizens from both your and other kingdoms with personalities that respond to their environment
- **Turn-Based Gameplay**: Hybrid system supporting both direct control and simulation
- **Dynamic World**: Procedurally generated settlements and environments

## 🔮 Planned Features

- **Modular Faction System**: Expand beyond necromancy with multiple magical paths
- **Kingdom Management**: Construct and upgrade buildings, manage resources, research
- **Deep Character Development**: Progress through magical specializations with meaningful choices
- **Rich Personality System**: Followers with distinct needs, desires, and relationships
- **Moral Choice Framework**: Shape your kingdom's philosophy through your actions

## 💻 Technical Highlights

- **Performance-Focused Design**: Multi-threaded entity simulation to handle large kingdoms
- **Agent-Based Architecture**: Autonomous entities with rich behavior systems
- **Trait-Based Character System**: Modular components for complex entity behaviors
- **Data-Driven Design**: Extensive configuration options for modding and expansion

## 🚀 Getting Started

### Requirements

- [Godot 4.4+](https://godotengine.org/download) with Mono/.NET support 
- .NET SDK 8.0 or later
- Visual Studio Code (recommended) with C# Dev Kit and Godot Tools extensions

### Installation

1. Clone the repository:
   ```
   git clone https://github.com/azrazalea/Veil-of-Ages.git
   ```

2. Open the project in Godot 4.4+ by selecting the `project.godot` file

3. Build the C# solution:
   ```
   dotnet build
   ```

4. Run the game from the Godot Editor (F5)

### Basic Controls

- WASD / Arrow Keys: Move character
- E: Interact with objects and characters
- Space: Pause/resume simulation
- -/=: Speed up/slow down time

## 🔧 Development Status

Veil of Ages is in **early development**. Current focus areas:

- Core entity and trait systems
- Basic necromancer gameplay loop
- Town generation with living and undead inhabitants
- Resource gathering and storage systems
- Building foundation systems

## 📜 License

- **Code**: Licensed under [GNU AGPL-3.0](LICENSE.code.md)
- **Assets**: The Minifantasy assets are licensed separately and are not redistributable.

## 🤝 Contributing

Contributions are welcome! Please see our [contributing guidelines](CONTRIBUTING.md) for details.

## 🙏 Acknowledgments

- Krishna Palacio for the Minifantasy asset packs used in development
- The Godot Engine community for their invaluable resources and support
- Inspiration from games like KeeperRL, Rimworld, and Dwarf Fortress
