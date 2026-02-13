namespace VeilOfAges.Core.Lib;

/// <summary>
/// Interface for JSON-loadable resource definitions.
/// Implemented by definition types managed by ResourceManager subclasses.
/// </summary>
public interface IResourceDefinition
{
    /// <summary>
    /// Gets unique identifier for this definition.
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// Validate this definition has required fields and constraints.
    /// Returns true if valid, false otherwise.
    /// </summary>
    bool Validate();
}
