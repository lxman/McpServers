namespace CodeAssist.Core.Models;

/// <summary>
/// Represents a parameter of a method or function.
/// </summary>
public sealed record ParameterInfo
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type annotation, or null if not present or not resolved.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Default value expression, or null if none.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Whether this is an out parameter (C#).
    /// </summary>
    public bool IsOut { get; init; }

    /// <summary>
    /// Whether this is a ref parameter (C#).
    /// </summary>
    public bool IsRef { get; init; }

    /// <summary>
    /// Whether this is a params parameter (C#).
    /// </summary>
    public bool IsParams { get; init; }
}
