# Decorations

## Purpose

Defines interactive and visual decoration objects placed inside buildings. Decorations are distinct from buildings — they are individual furniture/objects that provide facilities for entity activities (e.g., ovens for baking, beds for sleeping, querns for milling).

## Structure

```
decorations/
├── definitions/    # JSON files defining each decoration type
└── animations/     # JSON files defining decoration animations
```

## Definitions

Each JSON in `definitions/` defines a decoration type:

| File | Description |
|------|-------------|
| `bed.json` | Sleeping facility (double) |
| `single_bed.json` | Sleeping facility (single) |
| `chest.json` | Storage container |
| `oven.json` | Cooking/baking facility |
| `quern.json` | Grain milling facility |
| `tombstone.json` | Graveyard marker |
| `trapdoor.json` | Grid transition point (e.g., Scholar's House → cellar) |
| `tree.json` | Environmental decoration |

## Animations

JSON files in `animations/` define frame-based animations for decorations:

| File | Description |
|------|-------------|
| `oven_idle.json` | Idle animation for the oven |

## Dependencies

### Depended on by:
- Building templates (`/resources/buildings/templates/`) reference decorations by name
- `DecorationResourceManager` loads and instantiates decorations
- Entity activities interact with decorations as facilities (e.g., `ProcessReactionActivity` uses ovens/querns)
