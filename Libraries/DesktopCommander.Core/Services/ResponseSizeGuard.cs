using System.Text.Json;
using DesktopCommander.Core.Common;
using Microsoft.Extensions.Logging;

namespace DesktopCommander.Core.Services;

/// <summary>
/// Service to protect against oversized MCP tool responses
/// </summary>
public class ResponseSizeGuard(ILogger<ResponseSizeGuard> logger)
{
    // Token limits
    private const int SafeTokenLimit = 20000;  // Safe limit below hard limit
    private const int HardTokenLimit = 25000;  // MCP protocol hard limit
    private const int CharsPerToken = 4;       // Conservative estimate: 1 token ≈ 4 characters

    // Character limits (derived from token limits)
    public const int SafeCharLimit = SafeTokenLimit * CharsPerToken;    // 80,000 chars
    public const int HardCharLimit = HardTokenLimit * CharsPerToken;    // 100,000 chars

    /// <summary>
    /// Check if a response object would exceed safe size limits
    /// </summary>
    /// <param name="responseObject">The object to be serialized and returned</param>
    /// <param name="toolName">Name of the tool for logging</param>
    /// <returns>Check result with details</returns>
    public ResponseSizeCheck CheckResponseSize(object responseObject, string toolName)
    {
        try
        {
            // Serialize the response to measure its size
            string jsonResult = JsonSerializer.Serialize(responseObject, SerializerOptions.JsonOptionsIndented);
            int characterCount = jsonResult.Length;
            int estimatedTokens = characterCount / CharsPerToken;

            bool exceeds = estimatedTokens > SafeTokenLimit;

            if (exceeds)
            {
                logger.LogWarning(
                    "Tool {ToolName} response exceeds safe size: {Tokens} tokens ({Chars} chars)",
                    toolName,
                    estimatedTokens,
                    characterCount);
            }
            else
            {
                logger.LogDebug(
                    "Tool {ToolName} response size OK: {Tokens} tokens ({Chars} chars)",
                    toolName,
                    estimatedTokens,
                    characterCount);
            }

            return new ResponseSizeCheck
            {
                IsWithinLimit = !exceeds,
                CharacterCount = characterCount,
                EstimatedTokens = estimatedTokens,
                SafeTokenLimit = SafeTokenLimit,
                HardTokenLimit = HardTokenLimit,
                SerializedJson = jsonResult
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking response size for tool {ToolName}", toolName);

            // If we can't check, assume it's OK and let it through
            return new ResponseSizeCheck
            {
                IsWithinLimit = true,
                CharacterCount = 0,
                EstimatedTokens = 0,
                SafeTokenLimit = SafeTokenLimit,
                HardTokenLimit = HardTokenLimit,
                SerializedJson = null,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Check if a string response would exceed safe size limits
    /// </summary>
    /// <param name="content">The string content to check</param>
    /// <param name="toolName">Name of the tool for logging</param>
    /// <returns>Check result with details</returns>
    public ResponseSizeCheck CheckStringSize(string content, string toolName)
    {
        int characterCount = content.Length;
        int estimatedTokens = characterCount / CharsPerToken;
        bool exceeds = estimatedTokens > SafeTokenLimit;

        if (exceeds)
        {
            logger.LogWarning(
                "Tool {ToolName} string content exceeds safe size: {Tokens} tokens ({Chars} chars)",
                toolName,
                estimatedTokens,
                characterCount);
        }

        return new ResponseSizeCheck
        {
            IsWithinLimit = !exceeds,
            CharacterCount = characterCount,
            EstimatedTokens = estimatedTokens,
            SafeTokenLimit = SafeTokenLimit,
            HardTokenLimit = HardTokenLimit,
            SerializedJson = content
        };
    }

    /// <summary>
    /// Create a standardized "response too large" error result
    /// </summary>
    /// <param name="check">The size check result</param>
    /// <param name="context">Context about what was being returned</param>
    /// <param name="suggestion">Suggestion for how to avoid the issue</param>
    /// <returns>Serialized error JSON</returns>
    public static string CreateOversizedErrorResponse(
        ResponseSizeCheck check,
        string context,
        string suggestion)
    {
        var errorResult = new
        {
            success = false,
            error = "Response too large",
            message = $"{context} Response size of {check.EstimatedTokens:N0} tokens " +
                     $"({check.CharacterCount:N0} characters) exceeds the safe limit of " +
                     $"{SafeTokenLimit:N0} tokens.",
            details = new
            {
                characterCount = check.CharacterCount,
                estimatedTokens = check.EstimatedTokens,
                safeTokenLimit = SafeTokenLimit,
                hardTokenLimit = HardTokenLimit,
                percentOfLimit = (double)check.EstimatedTokens / SafeTokenLimit * 100
            },
            suggestion
        };

        return JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsIndented);
    }

    /// <summary>
    /// Helper to create error response with additional metrics
    /// </summary>
    public static string CreateOversizedErrorResponse(
        ResponseSizeCheck check,
        string context,
        string suggestion,
        object additionalMetrics)
    {
        var errorResult = new
        {
            success = false,
            error = "Response too large",
            message = $"{context} Response size of {check.EstimatedTokens:N0} tokens " +
                     $"({check.CharacterCount:N0} characters) exceeds the safe limit of " +
                     $"{SafeTokenLimit:N0} tokens.",
            details = new
            {
                characterCount = check.CharacterCount,
                estimatedTokens = check.EstimatedTokens,
                safeTokenLimit = SafeTokenLimit,
                hardTokenLimit = HardTokenLimit,
                percentOfLimit = (double)check.EstimatedTokens / SafeTokenLimit * 100
            },
            metrics = additionalMetrics,
            suggestion
        };

        return JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsIndented);
    }

    /// <summary>
    /// Estimate tokens from character count without serialization
    /// </summary>
    public static int EstimateTokens(int characterCount)
    {
        return characterCount / CharsPerToken;
    }

    /// <summary>
    /// Check if a character count would exceed limits
    /// </summary>
    public static bool WouldExceedLimit(int characterCount)
    {
        return EstimateTokens(characterCount) > SafeTokenLimit;
    }
}

/// <summary>
/// Result of a response size check
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
    /// Estimated token count (chars / 4)
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
    /// The serialized JSON (if check succeeded)
    /// </summary>
    public string? SerializedJson { get; init; }

    /// <summary>
    /// Error message if check failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// How close to the limit (as percentage)
    /// </summary>
    public double PercentOfLimit => (double)EstimatedTokens / SafeTokenLimit * 100;
}
