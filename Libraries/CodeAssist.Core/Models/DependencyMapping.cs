namespace CodeAssist.Core.Models;

/// <summary>
/// Represents a DI container registration mapping an interface to its implementation.
/// </summary>
public sealed record DependencyMapping
{
    /// <summary>
    /// Fully qualified interface or service type.
    /// </summary>
    public required string InterfaceType { get; init; }

    /// <summary>
    /// Fully qualified concrete implementation type.
    /// </summary>
    public required string ConcreteType { get; init; }

    /// <summary>
    /// Service lifetime (Scoped, Singleton, Transient), or null if unknown.
    /// </summary>
    public string? Lifetime { get; init; }
}
