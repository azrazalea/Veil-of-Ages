# Buildings Directory

## Purpose

Contains building-related resources. Currently houses the `templates/` subdirectory for building layout definitions.

## Directory Structure

```
buildings/
└── templates/    # Building layout templates
```

## Files

This directory contains no JSON files directly. All building definitions are in the `templates/` subdirectory.

## Dependencies

- **BuildingTemplate**: `entities/building/BuildingTemplate.cs` - Parses building templates
- **Building**: `entities/building/Building.cs` - Instantiates buildings from templates

## Important Notes

- Building templates are loaded on-demand, not at startup like tile resources
- The `BuildingType` field determines how the game treats the building (House, Farm, Graveyard, etc.)
