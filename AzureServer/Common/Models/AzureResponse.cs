namespace AzureServer.Common.Models;

/// <summary>
/// Standard response wrapper for all Azure Server operations
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class AzureResponse<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The actual data returned by the operation
    /// </summary>
    public T? Data { get; set; }
    
    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Error type for programmatic error handling
    /// </summary>
    public string? ErrorType { get; set; }
    
    /// <summary>
    /// Additional metadata about the response
    /// </summary>
    public ResponseMetadata? Metadata { get; set; }
    
    /// <summary>
    /// Create a successful response
    /// </summary>
    public static AzureResponse<T> Ok(T data, ResponseMetadata? metadata = null)
    {
        return new AzureResponse<T>
        {
            Success = true,
            Data = data,
            Metadata = metadata ?? new ResponseMetadata()
        };
    }
    
    /// <summary>
    /// Create an error response
    /// </summary>
    public static AzureResponse<T> Fail(string error, string? errorType = null)
    {
        return new AzureResponse<T>
        {
            Success = false,
            Error = error,
            ErrorType = errorType,
            Metadata = new ResponseMetadata()
        };
    }
}
