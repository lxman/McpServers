using System.Text.Json;
using ModelContextProtocol.Server;

namespace McpCodeEditor.Tools.Common;

/// <summary>
/// Base class for MCP tool categories containing common patterns and utilities.
/// SLICE_5: Extracted common patterns to reduce duplication across tool classes.
/// </summary>
[McpServerToolType]
public abstract class BaseToolClass
{
    #region JSON Serialization Helpers

    /// <summary>
    /// Standard JsonSerializerOptions used consistently across all tools
    /// </summary>
    protected static readonly JsonSerializerOptions StandardJsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Serialize a successful result with consistent JSON formatting
    /// </summary>
    /// <typeparam name="T">Type of the result object</typeparam>
    /// <param name="result">Result object to serialize</param>
    /// <returns>JSON string representation</returns>
    protected static string SerializeResult<T>(T result)
    {
        return JsonSerializer.Serialize(result, StandardJsonOptions);
    }

    /// <summary>
    /// Serialize an error response with consistent formatting
    /// </summary>
    /// <param name="errorMessage">Error message to include</param>
    /// <param name="exception">Optional exception for additional context</param>
    /// <returns>JSON string representation of error</returns>
    protected static string SerializeError(string errorMessage, Exception? exception = null)
    {
        var errorResponse = new
        {
            success = false,
            error = errorMessage,
            details = exception?.StackTrace
        };

        return JsonSerializer.Serialize(errorResponse, StandardJsonOptions);
    }

    /// <summary>
    /// Serialize an error response from an exception
    /// </summary>
    /// <param name="exception">Exception to serialize</param>
    /// <returns>JSON string representation of error</returns>
    protected static string SerializeError(Exception exception)
    {
        return SerializeError(exception.Message, exception);
    }

    #endregion

    #region Common Validation Helpers

    /// <summary>
    /// Validate that a required string parameter is not null or empty
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="parameterName">Name of the parameter for error messages</param>
    /// <exception cref="ArgumentException">Thrown if value is null or empty</exception>
    protected static void ValidateRequiredParameter(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and cannot be null or empty.", parameterName);
        }
    }

    /// <summary>
    /// Validate that a file path parameter is not null or empty
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <exception cref="ArgumentException">Thrown if file path is invalid</exception>
    protected static void ValidateFilePath(string? filePath)
    {
        ValidateRequiredParameter(filePath, nameof(filePath));
    }

    /// <summary>
    /// Validate that a line number is positive
    /// </summary>
    /// <param name="lineNumber">Line number to validate</param>
    /// <param name="parameterName">Name of the parameter for error messages</param>
    /// <exception cref="ArgumentException">Thrown if line number is not positive</exception>
    protected static void ValidateLineNumber(int lineNumber, string parameterName = "lineNumber")
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be positive (1-based line numbers).", parameterName);
        }
    }

    #endregion

    #region Common Error Handling Patterns

    /// <summary>
    /// Execute an async operation with standard error handling and JSON serialization
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    /// <param name="operation">Async operation to execute</param>
    /// <returns>JSON string representation of result or error</returns>
    protected static async Task<string> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            var result = await operation();
            return SerializeResult(result);
        }
        catch (ArgumentException ex)
        {
            return SerializeError($"Invalid parameter: {ex.Message}", ex);
        }
        catch (FileNotFoundException ex)
        {
            return SerializeError($"File not found: {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            return SerializeError($"Directory not found: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return SerializeError($"Access denied: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            return SerializeError($"IO error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return SerializeError($"Unexpected error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Execute a sync operation with standard error handling and JSON serialization
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <returns>JSON string representation of result or error</returns>
    protected static string ExecuteWithErrorHandling<T>(Func<T> operation)
    {
        try
        {
            var result = operation();
            return SerializeResult(result);
        }
        catch (ArgumentException ex)
        {
            return SerializeError($"Invalid parameter: {ex.Message}", ex);
        }
        catch (FileNotFoundException ex)
        {
            return SerializeError($"File not found: {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            return SerializeError($"Directory not found: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return SerializeError($"Access denied: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            return SerializeError($"IO error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return SerializeError($"Unexpected error: {ex.Message}", ex);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Create a successful response object with standard structure
    /// </summary>
    /// <param name="data">Data to include in the response</param>
    /// <param name="message">Optional success message</param>
    /// <returns>Structured success response</returns>
    protected static object CreateSuccessResponse(object data, string? message = null)
    {
        return new
        {
            success = true,
            message,
            data
        };
    }

    /// <summary>
    /// Create a successful response with just a message
    /// </summary>
    /// <param name="message">Success message</param>
    /// <returns>Structured success response</returns>
    protected static object CreateSuccessResponse(string message)
    {
        return new
        {
            success = true,
            message
        };
    }

    #endregion
}
