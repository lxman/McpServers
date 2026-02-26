namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// The kind of relationship a graph edge represents.
/// </summary>
public enum GraphEdgeKind
{
    /// <summary>Source calls target.</summary>
    Calls,

    /// <summary>Source reads a field/property on target.</summary>
    FieldRead,

    /// <summary>Source writes a field/property on target.</summary>
    FieldWrite,

    /// <summary>Source class inherits from target class.</summary>
    Inherits,

    /// <summary>Source class implements target interface.</summary>
    Implements,

    /// <summary>Source HTTP client calls target HTTP server endpoint.</summary>
    HttpEndpoint,

    /// <summary>Interface resolved to concrete type via DI container.</summary>
    DependencyInjection
}
