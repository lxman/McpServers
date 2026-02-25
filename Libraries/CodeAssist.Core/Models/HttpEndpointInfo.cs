namespace CodeAssist.Core.Models;

/// <summary>
/// Represents an HTTP endpoint that a project serves or consumes.
/// Used for cross-tier data flow linking (e.g., Angular frontend → .NET API backend).
/// </summary>
public sealed record HttpEndpointInfo
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// Route template (e.g., "/api/orders", "/api/orders/{id}").
    /// </summary>
    public required string RouteTemplate { get; init; }

    /// <summary>
    /// Whether this endpoint is served (backend) or consumed (client).
    /// </summary>
    public required HttpEndpointRole Role { get; init; }

    /// <summary>
    /// Name of the method/function that defines or calls this endpoint.
    /// </summary>
    public required string SymbolName { get; init; }

    /// <summary>
    /// Fully qualified symbol name, or null.
    /// </summary>
    public string? QualifiedName { get; init; }

    /// <summary>
    /// File path where the endpoint is defined/used, or null.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Line number (1-indexed) where the endpoint is defined/used.
    /// </summary>
    public int Line { get; init; }
}
