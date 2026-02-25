namespace CodeAssist.Core.Models;

/// <summary>
/// Represents access to a field or property within a code chunk.
/// </summary>
public sealed record FieldAccess
{
    /// <summary>
    /// Name of the field or property being accessed.
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// Type that contains the field, or null if not resolved.
    /// </summary>
    public string? ContainingType { get; init; }

    /// <summary>
    /// Whether the field is read, written, or both.
    /// </summary>
    public FieldAccessKind Kind { get; init; }

    /// <summary>
    /// Line number where the access occurs (1-indexed).
    /// </summary>
    public int Line { get; init; }
}
