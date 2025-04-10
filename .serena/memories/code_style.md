# Code Style and Conventions

## General Conventions
- **C# Language Version**: 12.0 (as specified in .csproj)
- **Nullable Reference Types**: Enabled (nullable context is enabled)

## Naming Conventions
- **Classes**: PascalCase (e.g., `GameController`, `EntityThinkingSystem`)
- **Methods**: PascalCase (e.g., `ProcessGameTick`, `RegisterEntity`)
- **Properties**: PascalCase (e.g., `TimeScale`, `MaxPlayerActions`)
- **Private Fields**: camelCase with underscore prefix (e.g., `_timeSinceLastTick`, `_processingTick`)
- **Parameters**: camelCase (e.g., `gameTimeInCentiseconds`, `delta`)
- **Local Variables**: camelCase (e.g., `totalCentiseconds`, `animatedSprite`)
- **Constants**: ALL_CAPS with underscores (e.g., `CENTISECONDS_PER_SECOND`, `DAYS_PER_MONTH`)
- **Namespaces**: PascalCase (e.g., `VeilOfAges.Core`, `VeilOfAges.Entities`)

## Documentation
- XML documentation is used for public methods and classes
- Summary tags describe the purpose of methods and classes
- Parameter tags document parameters
- Return tags explain return values

## Code Organization
- **Inheritance**: Abstract base classes are used for shared functionality
- **Interfaces**: Interfaces define contracts (e.g., `IEntity`)
- **Partial Classes**: Used with Godot's node system
- **Records**: Used for immutable data structures (e.g., `BeingAttributes`)

## Threading Considerations
- Entity thinking system uses multithreading for performance
- Data structures passed to threaded subsystems must be immutable
- Godot Nodes are NOT thread safe - extra caution needed when working with them

## Godot-Specific Patterns
- Use of Export attribute for inspector-configurable properties
- Node-based architecture aligned with Godot's scene system
- Signal-based communication for loose coupling when appropriate

## Other Conventions
- Prefer nullable reference types with ? suffix for nullable references
- Use expression-bodied members for simple properties and methods
- C# 9.0+ features like records and init-only setters are utilized
- Async/await pattern used for asynchronous operations