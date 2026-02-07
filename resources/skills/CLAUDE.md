# /resources/skills

## Purpose

JSON skill definitions loaded by `SkillResourceManager` during game initialization. These files define the properties and progression curves for all skills available in the game.

## JSON Format

Each skill definition is a JSON file with the following structure:

```json
{
    "Id": "skill_id",
    "Name": "Display Name",
    "Description": "What this skill represents",
    "Category": "General|Combat|Crafting|Magic|Social",
    "MaxLevel": 100,
    "BaseXpPerLevel": 100,
    "XpScaling": 1.15,
    "AttributeInfluences": {
        "Intelligence": 0.5,
        "Wisdom": 0.3,
        "Willpower": 0.2
    },
    "Tags": ["tag1", "tag2"]
}
```

### Field Descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier used in code to reference this skill |
| `Name` | string | Yes | Display name shown to players |
| `Description` | string | No | Flavor text explaining what the skill is |
| `Category` | string | Yes | One of: General, Combat, Crafting, Magic, Social |
| `MaxLevel` | int | No | Maximum achievable level (default 100) |
| `BaseXpPerLevel` | float | No | Base XP for level 1→2 (default 100) |
| `XpScaling` | float | No | Exponential scaling factor (default 1.15) |
| `AttributeInfluences` | object | No | Map of attribute names to weight values |
| `Tags` | array | No | String tags for filtering and categorization |

### XP Progression Formula

XP required to advance from level N to level N+1:
```
XpForLevel(N) = BaseXpPerLevel * XpScaling^(N-1)
```

Examples:
- Level 1→2: `100 * 1.15^0 = 100 XP`
- Level 2→3: `100 * 1.15^1 = 115 XP`
- Level 10→11: `100 * 1.15^9 = 357 XP`

### Attribute Influence Guidelines

Attribute influence weights should typically sum to ~1.0 for balanced progression. Common patterns:

- **Single-attribute skills**: One attribute at 1.0 (e.g., pure strength)
- **Two-attribute skills**: Primary at 0.6-0.7, secondary at 0.3-0.4
- **Multi-attribute skills**: Distribute across 3+ attributes (see research.json)

Valid attribute names (case-insensitive):
- `Strength`, `Dexterity`, `Constitution`
- `Intelligence`, `Willpower`, `Wisdom`, `Charisma`

## Current Files

| File | Category | Attributes | Description |
|------|----------|------------|-------------|
| `research.json` | General | Int 0.5, Wis 0.3, Will 0.2 | Systematic investigation and learning |
| `arcane_theory.json` | Magic | Int 0.6, Wis 0.4 | Understanding magical principles |
| `necromancy.json` | Magic | Int 0.4, Will 0.4, Wis 0.2 | Controlling undead and death magic |
| `farming.json` | Crafting | Str 0.4, Con 0.3, Wis 0.3 | Crop cultivation and harvest |
| `baking.json` | Crafting | Dex 0.5, Int 0.3, Wis 0.2 | Bread and pastry creation |

## How to Add New Skills

1. Create a new `.json` file in `resources/skills/` or a subdirectory
2. Follow the JSON format above with required fields
3. Choose appropriate Category based on skill domain
4. Set `BaseXpPerLevel` and `XpScaling` to control progression speed
   - Lower BaseXp = faster early levels
   - Higher XpScaling = steeper curve at high levels
5. Define `AttributeInfluences` with weights summing to ~1.0
6. Add descriptive `Tags` for filtering (e.g., "combat", "knowledge", "labor")
7. Restart game - `SkillResourceManager` will auto-load the new file

## Notes

- Files in subdirectories are also loaded (organize by category if desired)
- Validation errors are logged on startup if definitions are invalid
- Duplicate IDs will overwrite - last loaded file wins
- Tag matching is case-insensitive
- Category must match SkillCategory enum exactly
