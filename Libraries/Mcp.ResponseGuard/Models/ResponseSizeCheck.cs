namespace Mcp.ResponseGuard.Models;

/// <summary>
/// Result of a response size check operation
/// </summary>
public class ResponseSizeCheck
{
    /// <summary>
    /// Whether the response is within safe size limits
    /// </summary>
    public required bool IsWithinLimit { get; init; }

    /// <summary>
    /// Total character count of the serialized response
    /// </summary>
    public required int CharacterCount { get; init; }

    /// <summary>
    /// Estimated token count (chars / CharsPerToken)
    /// </summary>
    public required int EstimatedTokens { get; init; }

    /// <summary>
    /// The safe token limit used for comparison
    /// </summary>
    public required int SafeTokenLimit { get; init; }

    /// <summary>
    /// The hard token limit (MCP protocol limit)
    /// </summary>
    public required int HardTokenLimit { get; init; }

    /// <summary>
    /// The serialized JSON (if check succeeded and within limits)
    /// </summary>
    public string? SerializedJson { get; init; }

    /// <summary>
    /// Error message if check failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// How close to the limit (as percentage)
    /// </summary>
    public double PercentOfLimit => SafeTokenLimit > 0
        ? (double)EstimatedTokens / SafeTokenLimit * 100
        : 0;

    /// <summary>
    /// Whether the response exceeded the hard limit (not just safe limit)
    /// </summary>
    public bool ExceedsHardLimit => EstimatedTokens > HardTokenLimit;
}
