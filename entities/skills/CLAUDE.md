# /entities/skills

## Purpose

Per-entity skill system for RPG-style progression. Entities gain experience in skills through performing actions, level up when they accumulate enough XP, and can perform skill checks against difficulty thresholds. Skills are influenced by entity attributes (Intelligence, Wisdom, etc.), creating a synergy between base attributes and learned abilities.

## Files

### SkillDefinition.cs
JSON-serializable skill template that defines the properties of a skill type. Contains:
- **Id**: Unique identifier for this skill type (e.g., "research", "farming")
- **Name**: Display name for the skill
- **Description**: Text explaining what the skill represents
- **Category**: Enum categorizing the skill (General, Combat, Crafting, Magic, Social)
- **MaxLevel**: Maximum achievable level (default 100)
- **BaseXpPerLevel**: Base XP required to advance from level 1 to level 2 (default 100)
- **XpScaling**: Exponential scaling factor applied per level (default 1.15)
- **AttributeInfluences**: Dictionary mapping attribute names to weight values (e.g., {"Intelligence": 0.5})
- **Tags**: List of strings for categorization and filtering (e.g., ["knowledge", "academic"])

**Localization:**
- `LocalizedName` - Computed property returning `L.Tr($"skill.{Id}.NAME")`. Display code should use this instead of `Name`.
- `LocalizedDescription` - Computed property returning `L.Tr($"skill.{Id}.DESCRIPTION")`.

**Key Methods:**
- `GetXpForLevel(level)`: Calculate XP required to advance from given level to next. Formula: `BaseXpPerLevel * XpScaling^(level-1)`
- `HasTag(tag)`: Check if skill has a specific tag (case-insensitive)
- `Validate()`: Verify definition has required fields and valid constraints
- `LoadFromJson(path)`: Static method to load definition from JSON file

### Skill.cs
Runtime skill instance tracking an entity's progress in a specific skill. Contains:
- **Definition**: Reference to the SkillDefinition this instance is based on
- **Level**: Current skill level (1 to MaxLevel)
- **CurrentXp**: XP accumulated toward the next level
- **XpToNextLevel**: Calculated XP required to reach next level (from Definition)
- **LevelProgress**: Float from 0 to 1 representing progress toward next level
- **IsMaxLevel**: True if skill has reached MaxLevel

**Key Methods:**
- `AddXp(amount)`: Add XP to this skill, potentially gaining one or more levels. Returns number of levels gained.
- `GetEffectiveLevel(attributes)`: Calculate effective level factoring in attribute bonuses. Each attribute point above 10 adds `(attrValue - 10) * weight * 0.1` to effective level.

### BeingSkillSystem.cs
Per-entity skill manager following the `BeingNeedsSystem` pattern. Composition-based service that tracks all skills for a Being. Contains:
- **_skills**: Dictionary mapping skill ID to Skill instance
- **_owner**: Reference to the Being that owns this skill system

**Key Methods:**
- `AddSkill(skill)`: Add a skill instance to this entity's collection
- `GetSkill(id)`: Retrieve skill by definition ID (returns null if not found)
- `HasSkill(id)`: Check if entity has a specific skill
- `GetAllSkills()`: Get enumerable of all skills entity has
- `GetSkillsByCategory(category)`: Filter skills by category
- `GainXp(skillId, baseXp)`: Grant XP to a skill, auto-creating if entity doesn't have it yet. Applies attribute-based multiplier to base XP. Returns levels gained.
- `SkillCheck(skillId, difficulty)`: Perform a skill check against difficulty value. Auto-pass if effective level >= difficulty, auto-fail if effective level < difficulty * 0.5, otherwise random roll.
- `CalculateAttributeMultiplier(definition)`: Internal method calculating XP multiplier from attributes. Formula: `max(0.5, 1.0 + sum((attrValue - 10) / 10 * weight))`. Minimum multiplier is 0.5 (50% rate).

### SkillResourceManager.cs
Singleton Godot autoload manager for loading skill definitions from JSON. Follows the `ItemResourceManager` pattern. Must be registered as an autoload in `project.godot`.

**Key Properties:**
- `Instance`: Static singleton instance (throws if not initialized as autoload)
- `_definitions`: Dictionary mapping skill ID to SkillDefinition

**Key Methods:**
- `_Ready()`: Loads all skill definitions from `res://resources/skills/` and subdirectories
- `GetDefinition(id)`: Retrieve a skill definition by ID (returns null if not found)
- `GetAllDefinitions()`: Get all loaded skill definitions
- `GetDefinitionsByCategory(category)`: Filter definitions by category
- `GetDefinitionsByTag(tag)`: Filter definitions by tag
- `HasDefinition(id)`: Check if a definition exists

## Key Design Patterns

### Auto-Creation on First XP Gain
Entities don't need to explicitly learn skills. When `BeingSkillSystem.GainXp()` is called for a skill the entity doesn't have, it automatically creates the skill instance starting at level 1.

### Attribute Influence System
Skills are affected by entity attributes in two ways:

1. **XP Gain Multiplier**: When gaining XP, entity attributes modify the rate. Each attribute point above 10 contributes `(attrValue - 10) / 10 * weight` to the multiplier. Example: Intelligence 15 with weight 0.5 adds `(15-10)/10*0.5 = 0.25` to multiplier, making XP gain 1.25x faster. Minimum multiplier is 0.5.

2. **Effective Level Bonus**: When calculating effective level (for skill checks), attributes add bonus levels. Each attribute point above 10 adds `(attrValue - 10) * weight * 0.1` to effective level. Example: Intelligence 15 with weight 0.5 adds `(15-10)*0.5*0.1 = 0.25` bonus levels.

### Skill Check Thresholds
The `SkillCheck()` method uses three tiers:
- **Auto-pass**: Effective level >= difficulty
- **Auto-fail**: Effective level < difficulty * 0.5
- **Random roll**: Otherwise, success chance = effectiveLevel / difficulty

## Threading Considerations

- **BeingSkillSystem**: Accessed from entity think threads (background threads). All methods are thread-safe for read operations since entities only modify their own skill system.
- **SkillResourceManager**: Read-only after `_Ready()` completes. Safe to access from any thread after initialization.
- **Skill instances**: Each entity has its own Skill instances. No cross-entity sharing, so no threading concerns.

## Dependencies

### Depends On
- `VeilOfAges.Entities.Being` - BeingAttributes record type
- `VeilOfAges.Core.Lib` - Log utility and JsonOptions
- Godot engine (Node base class for manager)

### Depended On By
- Traits and activities that grant skill XP (future implementation)
- UI systems for displaying skill progress (future implementation)

## Important Notes

- Attribute influence weights typically sum to ~1.0 for balanced progression
- XP scaling factors above 1.0 create exponential curves (1.15 is moderate, 1.3 is steep)
- Skills are intentionally separate from traits - traits define behaviors, skills define proficiency
- The system is designed for automatic integration: just call `GainXp()` from activities
