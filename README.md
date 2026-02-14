# Veil of Ages: Whispers of Kalixoria

<p align="center">
  <img src="assets/custom/logo/output_8x.png" alt="Veil of Ages logo" width="256">
</p>

![Code: AGPL-3.0](https://img.shields.io/badge/Code-AGPL--3.0-blue.svg)
![Assets: CC0](https://img.shields.io/badge/Assets-CC0-green.svg)
![Godot: 4.6](https://img.shields.io/badge/Godot-4.6-blue)
![Status: Pre-Alpha](https://img.shields.io/badge/Status-Pre--Alpha-orange)
![Version: 0.1.0](https://img.shields.io/badge/Version-0.1.0-green)
[![Build](https://github.com/azrazalea/Veil-of-Ages/actions/workflows/build.yml/badge.svg)](https://github.com/azrazalea/Veil-of-Ages/actions/workflows/build.yml)

**Veil of Ages: _Whispers of Kalixoria_** is a 2D necromancy kingdom simulation where you build a realm where the living and undead coexist. Set your character's priorities, manage their activities, and build a flourishing domain that defies conventional morality.

## Vision

Lead a necromancer's journey from outcast to ruler, creating a unique society where undead serve alongside living citizens. Make meaningful moral choices that shape your character and kingdom without forced alignment. Will you be a benevolent scholar of death magic, a practical survivalist, or a dark overlord? The choice is yours.

While the initial focus is on necromancy, the game's architecture is designed to eventually support multiple faction types, including nature-focused druids, elemental mages, and more.

## Current Features

- **Procedural Village Generation**: Settlements with roads, lots, and 6 building types including simple houses, simple farms, scholar's house, graveyard, granary, and well
- **Entity AI System**: Multi-threaded trait-based entity AI with priority-driven action queues — entities act autonomously based on their own motivations
- **Need System**: Hunger and energy needs drive entity behavior, with different satisfaction strategies per entity type
- **Memory and Perception**: Entities only know what they've personally observed — no omniscient AI
- **Resource Economy**: Full production chain from farming wheat to milling flour to baking bread
- **Skill System**: 5 skills (research, arcane theory, necromancy, farming, baking) with XP-based progression
- **Day/Night Cycle**: Seasonal light variation across a custom base-56 calendar (14 hours/day, 28 days/month, 13 months/year)
- **Time Controls**: Pause to 25x speed for managing your kingdom at your own pace
- **Entity Types**: Three villager job types (farmer, baker, distributor), two undead types (zombies and skeletons), and the player necromancer
- **Building Interiors**: Multi-grid support with Scholar's House cellar accessible via trapdoor
- **Cross-Platform**: Automated builds for Windows, Linux, and macOS
- **Data-Driven Design**: Buildings, items, skills, tiles, and reactions defined in JSON for easy modding

## Planned Features

- **Woodcutter and Firewood**: Expand the resource economy with wood gathering and heating
- **Central Granary**: Village-wide food storage and distribution
- **Necromancy Spells**: Raise and command undead with a spell system
- **Combat System**: Defend your domain against threats
- **Kingdom Management**: Construct and upgrade buildings, manage resources, research new technologies
- **Autonomy Profiles**: Customizable behavior patterns for your character
- **Montage Mode**: Skip ahead in time while your character and kingdom develop

## Getting Started

### Download a Release

Pre-built binaries for Windows, Linux, and macOS are available on the [Releases](https://github.com/azrazalea/Veil-of-Ages/releases) page.

### System Requirements

- **OS**: Windows 10+, Linux (x86_64), or macOS 12+ (Intel and Apple Silicon)
- **Runtime**: .NET 8.0 (bundled with release builds)
- **RAM**: 4 GB minimum
- **GPU**: Any GPU with OpenGL 3.3 / Vulkan support

### Development Setup

1. Install [Godot 4.6+](https://godotengine.org/download) with Mono/.NET support
2. Install .NET SDK 8.0 or later
3. Clone the repository:
   ```
   git clone https://github.com/azrazalea/Veil-of-Ages.git
   ```
4. Open the project in Godot by selecting the `project.godot` file
5. Build the C# solution:
   ```
   dotnet build
   ```
6. Run the game from the Godot Editor (F5)

### Basic Controls

- **Left-click**: Move to a location, or interact with adjacent entities
- **Right-click** (or **Ctrl+Left-click**): Open context menu
- **Ctrl+Space**: Toggle pause
- **+ / -**: Speed up / slow down simulation
- **Escape**: Close dialogue or cancel current command

## Development Status

Veil of Ages is in **pre-alpha**. The core simulation is functional with a working village economy, entity AI, and skill system. Current focus areas:

- Village infrastructure improvements (woodcutter, firewood, granary)
- Necromancy spell system
- Combat mechanics

See the [CHANGELOG](CHANGELOG.md) for a detailed history of changes.

## License

- **Code**: Licensed under [Modified GNU AGPL-3.0](LICENSE.code.md) with an exception allowing distribution on commercial game platforms. See [LICENSE.md](LICENSE.md) for full details.
- **Assets**: Visual assets use CC0 (Public Domain) licensed atlas packs. See [LICENSE.md](LICENSE.md) for attribution.

## Contributing

Contributions are welcome! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

## Acknowledgments

- Kenney (kenney.nl) for the 1-Bit Colored Pack
- Chris Hamons, MedicineStorm, and the many DCSS contributing artists for the ProjectUtumno tileset
- Vurmux for the Urizen OneBit V2 tileset
- [gridfab](https://github.com/azrazalea/gridfab) for custom pixel art creation and atlas building
- The Godot Engine community for their invaluable resources and support
- Inspiration from KeeperRL, RimWorld, Dwarf Fortress, Oxygen Not Included, Heroes of Might and Magic, Age of Wonders, Cultist Simulator, Faster Than Light, and Battlezone 2
