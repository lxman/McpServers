using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Models;
using Microsoft.Extensions.Logging;

namespace Mcp.ResponseGuard.Services;

/// <summary>
/// Service to protect against oversized MCP tool responses and standardize error handling
/// </summary>
public class OutputGuard
{
    private readonly ILogger<OutputGuard> _logger;
    private readonly OutputGuardOptions _options;

    /// <summary>
    /// Creates an OutputGuard with default options
    /// </summary>
    public OutputGuard(ILogger<OutputGuard> logger)
        : this(logger, new OutputGuardOptions())
    {
    }

    /// <summary>
    /// Creates an OutputGuard with custom options
    /// </summary>
    public OutputGuard(ILogger<OutputGuard> logger, OutputGuardOptions options)
    {
        _logger = logger;
        _options = options;
    }

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
            string jsonResult = JsonSerializer.Serialize(responseObject, SerializerOptions.JsonOptionsCamelCase);
            int characterCount = jsonResult.Length;
            int estimatedTokens = characterCount / _options.CharsPerToken;

            bool exceeds = estimatedTokens > _options.SafeTokenLimit;

            if (exceeds)
            {
                _logger.LogWarning(
                    "Tool {ToolName} response exceeds safe size: {Tokens} tokens ({Chars} chars) - {Percent:F1}% of limit",
                    toolName,
                    estimatedTokens,
                    characterCount,
                    (double)estimatedTokens / _options.SafeTokenLimit * 100);
            }
            else
            {
                _logger.LogDebug(
                    "Tool {ToolName} response size OK: {Tokens} tokens ({Chars} chars) - {Percent:F1}% of limit",
                    toolName,
                    estimatedTokens,
                    characterCount,
                    (double)estimatedTokens / _options.SafeTokenLimit * 100);
            }

            return new ResponseSizeCheck
            {
                IsWithinLimit = !exceeds,
                CharacterCount = characterCount,
                EstimatedTokens = estimatedTokens,
                SafeTokenLimit = _options.SafeTokenLimit,
                HardTokenLimit = _options.HardTokenLimit,
                SerializedJson = jsonResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking response size for tool {ToolName}", toolName);

            // If we can't check, assume it's OK and let it through
            return new ResponseSizeCheck
            {
                IsWithinLimit = true,
                CharacterCount = 0,
                EstimatedTokens = 0,
                SafeTokenLimit = _options.SafeTokenLimit,
                HardTokenLimit = _options.HardTokenLimit,
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
        int estimatedTokens = characterCount / _options.CharsPerToken;
        bool exceeds = estimatedTokens > _options.SafeTokenLimit;

        if (exceeds)
        {
            _logger.LogWarning(
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
            SafeTokenLimit = _options.SafeTokenLimit,
            HardTokenLimit = _options.HardTokenLimit,
            SerializedJson = content
        };
    }

    /// <summary>
    /// Process a response object and return guarded JSON
    /// Automatically checks size and returns error if oversized
    /// </summary>
    public string GuardResponse<T>(T responseObject, string toolName, string? oversizedSuggestion = null)
    {
        if (responseObject == null)
        {
            return CreateErrorResponse("Response object is null", errorCode: "NULL_RESPONSE");
        }

        ResponseSizeCheck check = CheckResponseSize(responseObject, toolName);

        if (!check.IsWithinLimit)
        {
            return CreateOversizedErrorResponse(
                check,
                $"The response from {toolName} is too large to return.",
                oversizedSuggestion ?? "Try reducing the amount of data requested.");
        }

        return check.SerializedJson!;
    }

    /// <summary>
    /// Create a standardized success response
    /// </summary>
    public string CreateSuccessResponse<T>(T data, string? message = null)
    {
        var response = new StandardResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };

        return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsCamelCase);
    }

    /// <summary>
    /// Create a standardized error response from an exception
    /// </summary>
    public string CreateErrorResponse(Exception exception, string? suggestion = null, string? errorCode = null)
    {
        var response = new ErrorResponse
        {
            Success = false,
            Error = exception.Message,
            Details = new
            {
                exceptionType = exception.GetType().Name,
                stackTrace = exception.StackTrace
            },
            Suggestion = suggestion,
            ErrorCode = errorCode
        };

        return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsCamelCase);
    }

    /// <summary>
    /// Create a standardized error response from a message
    /// </summary>
    public string CreateErrorResponse(string errorMessage, object? details = null, string? suggestion = null, string? errorCode = null)
    {
        var response = new ErrorResponse
        {
            Success = false,
            Error = errorMessage,
            Details = details,
            Suggestion = suggestion,
            ErrorCode = errorCode
        };

        return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsCamelCase);
    }

    /// <summary>
    /// Create a standardized "response too large" error result
    /// </summary>
    public string CreateOversizedErrorResponse(
        ResponseSizeCheck check,
        string context,
        string? suggestion = null,
        object? additionalMetrics = null)
    {
        var response = new OversizedResponse
        {
            Success = false,
            Error = "Response too large",
            Message = $"{context} Response size of {check.EstimatedTokens:N0} tokens " +
                     $"({check.CharacterCount:N0} characters) exceeds the safe limit of " +
                     $"{_options.SafeTokenLimit:N0} tokens.",
            Details = new OversizedDetails
            {
                CharacterCount = check.CharacterCount,
                EstimatedTokens = check.EstimatedTokens,
                SafeTokenLimit = _options.SafeTokenLimit,
                HardTokenLimit = _options.HardTokenLimit,
                PercentOfLimit = check.PercentOfLimit
            },
            Suggestion = suggestion,
            Metrics = additionalMetrics
        };

        return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsCamelCase);
    }

    /// <summary>
    /// Estimate tokens from character count without serialization
    /// </summary>
    public int EstimateTokens(int characterCount)
    {
        return characterCount / _options.CharsPerToken;
    }

    /// <summary>
    /// Check if a character count would exceed limits
    /// </summary>
    public bool WouldExceedLimit(int characterCount)
    {
        return EstimateTokens(characterCount) > _options.SafeTokenLimit;
    }

    /// <summary>
    /// Get the current configuration options
    /// </summary>
    public OutputGuardOptions GetOptions()
    {
        return _options;
    }
}
