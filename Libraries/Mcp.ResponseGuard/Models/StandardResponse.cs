namespace Mcp.ResponseGuard.Models;

/// <summary>
/// Standard success response format for all MCP tools
/// </summary>
/// <typeparam name="T">Type of data payload</typeparam>
public class StandardResponse<T>
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// The data payload (null if error)
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Optional message for additional context
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Standard error response format for all MCP tools
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Always false for error responses
    /// </summary>
    public bool Success { get; init; } = false;

    /// <summary>
    /// Primary error message
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Optional detailed error information
    /// </summary>
    public object? Details { get; init; }

    /// <summary>
    /// Optional suggestion for how to resolve the error
    /// </summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Error code/category for programmatic handling
    /// </summary>
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Specialized error response for oversized responses
/// </summary>
public class OversizedResponse
{
    /// <summary>
    /// Always false for error responses
    /// </summary>
    public bool Success { get; init; } = false;

    /// <summary>
    /// Error message indicating response too large
    /// </summary>
    public string Error { get; init; } = "Response too large";

    /// <summary>
    /// Detailed message about the size issue
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Size metrics
    /// </summary>
    public required OversizedDetails Details { get; init; }

    /// <summary>
    /// Suggestions for how to avoid the size issue
    /// </summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Optional additional metrics specific to the tool
    /// </summary>
    public object? Metrics { get; init; }
}

/// <summary>
/// Details about response size that exceeded limits
/// </summary>
public class OversizedDetails
{
    /// <summary>
    /// Total character count of the serialized response
    /// </summary>
    public required int CharacterCount { get; init; }

    /// <summary>
    /// Estimated token count (chars / 4)
    /// </summary>
    public required int EstimatedTokens { get; init; }

    /// <summary>
    /// The safe token limit that was exceeded
    /// </summary>
    public required int SafeTokenLimit { get; init; }

    /// <summary>
    /// The hard token limit (MCP protocol limit)
    /// </summary>
    public required int HardTokenLimit { get; init; }

    /// <summary>
    /// Percentage of safe limit that was used
    /// </summary>
    public required double PercentOfLimit { get; init; }
}
