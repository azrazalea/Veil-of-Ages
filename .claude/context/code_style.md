# Code Style and Conventions

## Compiler Settings (Strict)

The project enforces strict code quality through compiler settings in `Veil of Ages.csproj`:

### Warning and Error Policy
- **WarningLevel**: 9999 (maximum - all possible warnings enabled)
- **AnalysisLevel**: latest (newest C# analysis rules)
- **TreatWarningsAsErrors**: true (warnings break the build)
- **EnforceCodeStyleInBuild**: true (style violations break the build)

**Philosophy**: All warnings should be fixed, not suppressed. If a warning seems incorrect, understand why it's being raised before working around it.

### Code Analyzers
- **Roslynator.Analyzers**: Extended code analysis and refactoring suggestions
- **StyleCop.Analyzers**: Enforces consistent code style

### Auto-Formatting
- `dotnet format` runs automatically before every build
- Code is formatted to match `.editorconfig` rules (if present) and analyzer defaults

### Nullable Reference Types
Nullable reference types are **enabled** project-wide. This means:
- All reference types are non-nullable by default
- Use `Type?` suffix to mark a type as nullable
- The compiler tracks null flow and warns about potential null dereferences
- **Do not suppress null warnings** - fix the underlying issue:
  - Initialize fields properly (in constructor or with default values)
  - Use null checks or the null-conditional operator (`?.`)
  - Use `ArgumentNullException.ThrowIfNull()` for parameters that must not be null
  - Use the `!` null-forgiving operator only when you can prove the value is not null

### Common Warning Fixes
| Warning | Issue | Fix |
|---------|-------|-----|
| CS8618 | Non-nullable field not initialized | Initialize in constructor or make nullable |
| CS8601 | Possible null reference assignment | Add null check or make target nullable |
| CS8602 | Dereference of possibly null reference | Add null check before access |
| CS8604 | Possible null reference argument | Validate argument or make parameter nullable |

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

## No Backwards Compatibility Hacks

**NEVER create backwards compatibility shims, fallbacks, or deprecation wrappers.** When refactoring:

- **DELETE old methods/code completely** - don't mark as `[Obsolete]`
- **Let the compiler find all usages** - it will error on missing methods
- **Fix every call site** - don't leave any using old patterns
- **No re-exports or aliases** - if something is renamed, update all references

**Why:** Backwards compat shims hide bugs that can lurk for months or years. The compiler is your friend - let it tell you what needs updating. Being "lazy" with deprecation attributes creates technical debt and hidden failures.

**Examples of what NOT to do:**
- `[Obsolete] public void OldMethod() => NewMethod();`
- `public Type OldName => NewName; // alias for backwards compat`
- `// removed` comments where code used to be
- Keeping unused parameters with `_` prefix "in case needed later"

## Other Conventions
- Prefer nullable reference types with ? suffix for nullable references
- Use expression-bodied members for simple properties and methods
- C# 9.0+ features like records and init-only setters are utilized
- Async/await pattern used for asynchronous operations