namespace CodeAssist.Core.Models;

/// <summary>
/// Represents a call to a method/function from within a code chunk.
/// </summary>
public sealed record CallReference
{
    /// <summary>
    /// Name of the method being called.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The resolved type the method is called on (Tier 2 / semantic analysis only), or null.
    /// </summary>
    public string? ReceiverType { get; init; }

    /// <summary>
    /// The expression text the method is called on (e.g., "service", "this", "base"), or null.
    /// </summary>
    public string? ReceiverExpression { get; init; }

    /// <summary>
    /// Fully qualified name of the call target (Tier 2 only), or null.
    /// </summary>
    public string? QualifiedName { get; init; }

    /// <summary>
    /// Line number where the call occurs (1-indexed).
    /// </summary>
    public int Line { get; init; }
}
