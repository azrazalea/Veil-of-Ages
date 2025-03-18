# Assets Directory

This directory is intended to hold game assets used by Veil of Ages.

## Minifantasy Assets

The primary assets used in development are the Minifantasy asset packs by Krishna Palacio, which are **not included** in this public repository due to license restrictions.

### Important License Note

The Minifantasy assets are licensed under a Commercial License that:
1. Allows use in commercial and non-commercial games
2. Allows editing for use in your project
3. Does **NOT** allow redistribution or reselling of assets
4. Requires crediting Krishna Palacio
5. Requires sending Krishna Palacio a link to the completed project

### How to Add Assets to Your Local Copy

If you have legally purchased the Minifantasy assets, you can add them to your local copy in the following ways:

#### Option 1: Direct Placement
Place the assets directly in subdirectories of this assets folder:

```
assets/minifantasy/
├── entities/
│   ├── necromancer/
│   ├── undead/
│   └── ...
├── buildings/
│   ├── graveyard/
│   └── ...
├── terrain/
│   ├── forest/
│   └── ...
└── ...
```

#### Option 2: Git Submodule (Recommended for Teams)
For teams working on the project, we recommend using a private git repository as a submodule:

```bash
# From the project root
git submodule add <private-repo-url> assets/minifantasy
```

## Alternative Free Assets

If you don't have the Minifantasy assets, you can use free alternatives:

1. [Liberated Pixel Cup (LPC) Base Assets](https://opengameart.org/content/liberated-pixel-cup-lpc-base-assets-sprites-map-tiles)
2. [16x16 RPG Tileset](https://opengameart.org/content/16x16-rpg-tileset)

Note that these will require adaptations to work with the existing game code.

## Asset Structure

The code expects assets organized in the following structure:

```
assets/minifantasy/
├── entities/
│   ├── necromancer/
│   │   ├── Necromancer_Idle.png
│   │   ├── Necromancer_Walk.png
│   │   └── ...
│   ├── undead/
│   │   ├── skeleton-warrior/
│   │   │   ├── Idle_Activation_Deactivation.png
│   │   │   └── ...
│   │   └── zombie/
│   │       ├── ZombieIdle.png
│   │       └── ...
├── buildings/
│   ├── graveyard/
│   │   ├── Tileset.png
│   │   └── ...
└── terrain/
    ├── forest/
    │   ├── Ground_dark.png
    │   └── ...
    └── water/
        ├── Shallow_Water_Tileset.png
        └── ...
```
