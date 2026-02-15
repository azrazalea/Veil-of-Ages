# Entity Definitions

## Purpose

Contains JSON files that define entity types. Each file represents a complete entity configuration including attributes, traits, body structure, and audio. Definitions support inheritance via `ParentId`.

## Definition Inheritance

Definitions can inherit from a parent using the `ParentId` field:

```json
{
  "Id": "village_farmer",
  "ParentId": "human_townsfolk",
  "Name": "Village Farmer",
  "Traits": [
    { "TraitType": "FarmerJobTrait", "Priority": -1, "Parameters": {} }
  ],
  "Tags": ["farmer", "worker"]
}
```

**Inheritance Rules:**
- **Identity fields** (Id): Always use child's value
- **Simple fields** (Name, Description, Category, SpriteId): Child wins if specified, else parent
- **Attributes**: Merged field-by-field (child overrides specific attributes)
- **Movement**: Child wins if specified, else parent
- **Traits**: **ADDITIVE** - parent traits first, then child traits appended
- **Body.Modifications**: **ADDITIVE** - parent modifications first, then child
- **Tags**: **ADDITIVE** - union of parent and child tags

## Files

### Base Definitions

#### human_townsfolk.json
Base human villager definition. Used as parent for job-specific villagers.
- Category: Human
- Movement: 0.33 points/tick (3 ticks per tile)
- Traits: ScheduleTrait (priority 0, allowNightWork: false), VillagerTrait (priority 1)
- Tags: human, living, villager

### Job-Specific Villagers (inherit from human_townsfolk)

#### village_farmer.json
Villager who works the fields during the day.
- ParentId: human_townsfolk
- Added Traits: FarmerJobTrait (priority -1)
- Added Tags: farmer, worker
- Runtime Parameters: `workplace`/`farm` (Building)

#### village_baker.json
Villager who bakes bread and mills flour.
- ParentId: human_townsfolk
- Added Traits: BakerJobTrait (priority -1)
- Added Tags: baker, worker
- Runtime Parameters: `workplace` (Building)

#### village_distributor.json
Villager who delivers food from the granary.
- ParentId: human_townsfolk
- Added Traits: DistributorJobTrait (priority -1)
- Added Tags: distributor, worker
- Runtime Parameters: `workplace` (Building)

### Undead Definitions

#### mindless_skeleton.json
Mindless skeletal undead with territorial behavior.
- Category: Undead
- Movement: 0.39 points/tick (~2.5 ticks per tile)
- Traits: MindlessTrait (priority 1), SkeletonTrait (priority 2)
- Body Modifications: RemoveSoftTissues, ScaleBoneHealth (1.5x)
- Tags: undead, mindless, skeletal

### mindless_zombie.json
Mindless zombie with wandering behavior.
- Category: Undead
- Movement: 0.15 points/tick (~6.7 ticks per tile)
- Traits: MindlessTrait (priority 1), ZombieTrait (priority 2)
- Body Modifications: ApplyRandomDecay (2-5 parts, 30-70% damage)
- Tags: undead, mindless, rotting

### player.json
Player-controlled necromancer.
- Category: Human
- Movement: 0.5 points/tick (2 ticks per tile, fastest)
- Traits: ScheduleTrait (priority 0, allowNightWork: true), ScholarJobTrait (priority -1), NecromancyStudyJobTrait (priority -1), AutomationTrait (priority 4)
- Tags: human, player, necromancer, scholar

## Trait Definition Format

```json
{
  "TraitType": "TraitClassName",
  "Priority": 0,
  "Parameters": {
    "key": "value"
  }
}
```

- **TraitType**: Must match the exact C# class name
- **Priority**: Lower values execute first (0 = highest priority)
- **Parameters**: Passed to trait's Configure() method via TraitConfiguration

## Attribute Reference

All attributes use float values (typically 1-20 range):
- **Strength**: Physical power
- **Dexterity**: Agility and coordination
- **Constitution**: Health and endurance
- **Intelligence**: Mental acuity
- **Willpower**: Mental fortitude
- **Wisdom**: Perception and insight
- **Charisma**: Social influence

## Movement Speed Reference

Points per tick (higher = faster):
- Player: 0.5 (2 ticks per tile)
- Skeleton: 0.39 (~2.5 ticks per tile)
- Villager: 0.33 (~3 ticks per tile)
- Zombie: 0.15 (~6.7 ticks per tile)
