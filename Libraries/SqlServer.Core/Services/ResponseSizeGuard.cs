using System.Text.Json;
using Mcp.Common;
using Mcp.Common.Core;

namespace SqlServer.Core.Services;

/// <summary>
/// Response size check result containing size metrics and serialized JSON (if within limits)
/// </summary>
public class ResponseSizeCheck
{
    public bool IsWithinLimit { get; set; }
    public int CharacterCount { get; set; }
    public int EstimatedTokens { get; set; }
    public string? SerializedJson { get; set; }
}

/// <summary>
/// Protects against oversized MCP responses that could violate protocol limits.
/// MCP protocol has a hard limit of ~25,000 tokens. This guard enforces a safe limit
/// of 15,000 tokens to account for MCP wrapper overhead (~35-40%) and provide buffer.
/// </summary>
public class ResponseSizeGuard
{
    // Safe limit accounting for MCP protocol overhead:
    // - MCP hard limit: 25,000 tokens
    // - MCP wrapper overhead: ~35-40% (measured empirically with real queries)
    // - Safe payload limit: 15,000 tokens (15k * 1.4 = 21k, safely under 25k hard limit)
    private const int SafeTokenLimit = 15000;  // Safe limit accounting for MCP overhead
    private const int HardTokenLimit = 25000;  // MCP protocol hard limit
    private const int CharsPerToken = 4;       // Conservative estimate (1 token ≈ 4 chars)

    /// <summary>
    /// Checks if a response object exceeds safe size limits for MCP protocol
    /// </summary>
    /// <param name="responseObject">The object to serialize and check</param>
    /// <param name="toolName">Name of the tool for error reporting</param>
    /// <returns>ResponseSizeCheck with size info and serialized JSON if within limits</returns>
    public ResponseSizeCheck CheckResponseSize(object responseObject, string toolName)
    {
        // Serialize once to measure size
        string jsonResult = JsonSerializer.Serialize(responseObject, SerializerOptions.JsonOptionsIndented);

        int characterCount = jsonResult.Length;
        int estimatedTokens = characterCount / CharsPerToken;
        bool exceeds = estimatedTokens > SafeTokenLimit;

        return new ResponseSizeCheck
        {
            IsWithinLimit = !exceeds,
            CharacterCount = characterCount,
            EstimatedTokens = estimatedTokens,
            SerializedJson = exceeds ? null : jsonResult  // Only include JSON if safe
        };
    }

    /// <summary>
    /// Checks if a string response exceeds safe size limits
    /// </summary>
    public ResponseSizeCheck CheckStringSize(string content, string toolName)
    {
        int characterCount = content.Length;
        int estimatedTokens = characterCount / CharsPerToken;
        bool exceeds = estimatedTokens > SafeTokenLimit;

        return new ResponseSizeCheck
        {
            IsWithinLimit = !exceeds,
            CharacterCount = characterCount,
            EstimatedTokens = estimatedTokens,
            SerializedJson = exceeds ? null : content
        };
    }

    /// <summary>
    /// Creates a standardized error response for oversized results
    /// </summary>
    /// <param name="sizeCheck">The size check result</param>
    /// <param name="explanation">Specific explanation of what caused the large response</param>
    /// <param name="suggestions">Recommended workarounds</param>
    /// <param name="additionalInfo">Optional additional context information</param>
    /// <returns>JSON error response string</returns>
    public string CreateOversizedErrorResponse(
        ResponseSizeCheck sizeCheck,
        string explanation,
        string suggestions,
        object? additionalInfo = null)
    {
        var errorResponse = new
        {
            success = false,
            error = "RESPONSE_TOO_LARGE",
            message = "The query result is too large to return safely through the MCP protocol.",
            details = new
            {
                characterCount = sizeCheck.CharacterCount,
                estimatedTokens = sizeCheck.EstimatedTokens,
                safeLimit = SafeTokenLimit,
                hardLimit = HardTokenLimit,
                percentageOverLimit = ((sizeCheck.EstimatedTokens - SafeTokenLimit) * 100) / SafeTokenLimit
            },
            explanation,
            suggestions,
            additionalInfo
        };

        return JsonSerializer.Serialize(errorResponse, SerializerOptions.JsonOptionsIndented);
    }
}
