# Veil of Ages: Whispers of Kalixoria

![License: Modified AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)
![Godot: 4.4](https://img.shields.io/badge/Godot-4.4-blue)
![Status: Early Development](https://img.shields.io/badge/Status-Early%20Development-yellow)

**Veil of Ages: _Whispers of Kalixoria_** is a 2D necromancy kingdom simulation where you build a realm where the living and undead coexist. Set your character's priorities, manage their activities, and build a flourishing domain that defies conventional morality.

## 🌙 Vision

Lead a necromancer's journey from outcast to ruler, creating a unique society where undead serve alongside living citizens. Make meaningful moral choices that shape your character and kingdom without forced alignment. Will you be a benevolent scholar of death magic, a practical survivalist, or a dark overlord? The choice is yours.

While the initial focus is on necromancy, the game's architecture is designed to eventually support multiple faction types, including nature-focused druids, elemental mages, and more.

## 🎮 Current Features

- **Strategic Activity System**: Guide your character through an innovative priority-based activity queue rather than direct control, similar to RimWorld's work priorities but centered on a single protagonist
- **Dynamic Time System**: Experience a living world where time flows continuously, with speed controls from pause to ultra-fast time advancement
- **Undead Creation**: Command skeletons and zombies with distinct behaviors and needs
- **Living NPCs**: Citizens react dynamically to their environment with complex need systems
- **Simulation-Based Gameplay**: A deeply simulated world where entities act according to their own motivations even when not directly observed
- **Rich World Generation**: Procedurally generated settlements and environments

## 🔮 Planned Features

- **Autonomy Profiles**: Create customizable behavior patterns for your character, balancing research, kingdom management, and personal needs
- **Kingdom Management**: Construct and upgrade buildings, manage resources, research new technologies
- **Deep Character Development**: Progress through magical specializations with meaningful choices and realistic time investment
- **Montage Mode**: Skip ahead in time while your character advances their skills and your kingdom develops
- **Need Systems**: Complex hierarchies of needs for both living and undead entities that influence their behavior
- **Moral Choice Framework**: Shape your kingdom's philosophy through your actions

## 💻 Technical Highlights

- **Activity-Priority System**: Sophisticated task management similar to Oxygen Not Included, but with deeper automation options
- **Agent-Based Architecture**: Autonomous entities with rich behavior systems inspired by Dwarf Fortress's simulation depth
- **Multi-Threaded Simulation**: Performance-focused design handles large kingdoms with many active entities
- **Trait-Based Character System**: Modular components for complex entity behaviors
- **Time-Based Progression**: True skill development over realistic timeframes, encouraging use of time advancement mechanics

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

- **Right-click**: Interact with objects and entities, revealing available activities
- **Left-click**: Select items, confirm actions, manipulate the activity queue
- **Activity Panel**: View and manage your character's current and planned activities
- **Time Controls**: Pause/play and adjust simulation speed
- **Manual Mode**: Enter direct control for combat and precision tasks when needed

## 🔧 Development Status

Veil of Ages is in **early development**. Current focus areas:

- Core time system implementation
- Activity-priority control framework
- Basic necromancer gameplay loop
- Town generation with living and undead inhabitants
- Need system fundamentals
- Building foundation systems

## 📜 License

- **Code**: Licensed under [Modified GNU AGPL-3.0](LICENSE.code.md) with an exception allowing distribution on commercial game platforms including integration code with them without having to distribute the code of those platforms. All contributors are **required** to release their code contributions under the same license with the exception but are not required to assign copyright to the project owner.
- **Assets**: The Minifantasy assets are licensed separately and are not redistributable.

## 🤝 Contributing

Contributions are welcome! Please see our [contributing guidelines](CONTRIBUTING.md) for details.

## 🙏 Acknowledgments

- Krishna Palacio for the Minifantasy asset packs used in development
- The Godot Engine community for their invaluable resources and support
- Inspiration from games like KeeperRL, RimWorld, Dwarf Fortress, Oxygen not Included, Heroes of Might and Magic, Age of Wonders, and Cultist Simulator
