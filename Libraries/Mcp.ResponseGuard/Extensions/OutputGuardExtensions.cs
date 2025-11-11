using Mcp.ResponseGuard.Services;

namespace Mcp.ResponseGuard.Extensions;

/// <summary>
/// Extension methods to make OutputGuard easy to use across all MCP tools
/// </summary>
public static class OutputGuardExtensions
{
    /// <summary>
    /// Convert any object to a guarded JSON response
    /// Automatically checks size and returns error if oversized
    /// </summary>
    /// <param name="responseObject">The object to serialize</param>
    /// <param name="guard">The OutputGuard instance</param>
    /// <param name="toolName">Name of the tool for logging</param>
    /// <param name="oversizedSuggestion">Optional suggestion if response is too large</param>
    /// <returns>Serialized JSON string (success or error)</returns>
    public static string ToGuardedResponse<T>(
        this T responseObject,
        OutputGuard guard,
        string toolName,
        string? oversizedSuggestion = null)
    {
        return guard.GuardResponse(responseObject, toolName, oversizedSuggestion);
    }

    /// <summary>
    /// Convert an exception to a standardized error response
    /// </summary>
    /// <param name="exception">The exception to convert</param>
    /// <param name="guard">The OutputGuard instance</param>
    /// <param name="suggestion">Optional suggestion for resolution</param>
    /// <param name="errorCode">Optional error code</param>
    /// <returns>Serialized error JSON</returns>
    public static string ToErrorResponse(
        this Exception exception,
        OutputGuard guard,
        string? suggestion = null,
        string? errorCode = null)
    {
        return guard.CreateErrorResponse(exception, suggestion, errorCode);
    }

    /// <summary>
    /// Create a simple error response from a string message
    /// </summary>
    /// <param name="errorMessage">The error message</param>
    /// <param name="guard">The OutputGuard instance</param>
    /// <param name="details">Optional additional details</param>
    /// <param name="suggestion">Optional suggestion for resolution</param>
    /// <param name="errorCode">Optional error code</param>
    /// <returns>Serialized error JSON</returns>
    public static string ToErrorResponse(
        this string errorMessage,
        OutputGuard guard,
        object? details = null,
        string? suggestion = null,
        string? errorCode = null)
    {
        return guard.CreateErrorResponse(errorMessage, details, suggestion, errorCode);
    }

    /// <summary>
    /// Wrap data in a success response
    /// </summary>
    /// <param name="data">The data to wrap</param>
    /// <param name="guard">The OutputGuard instance</param>
    /// <param name="message">Optional success message</param>
    /// <returns>Serialized success JSON</returns>
    public static string ToSuccessResponse<T>(
        this T data,
        OutputGuard guard,
        string? message = null)
    {
        return guard.CreateSuccessResponse(data, message);
    }
}
