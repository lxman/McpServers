namespace DocumentServer.Models.Common;

/// <summary>
/// Generic wrapper for service operation results
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ServiceResult<T>
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The data returned by the operation (if successful)
    /// </summary>
    public T? Data { get; set; }
    
    /// <summary>
    /// Error message (if operation failed)
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// List of warning messages (non-fatal issues)
    /// </summary>
    public List<string> Warnings { get; set; } = [];
    
    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static ServiceResult<T> CreateSuccess(T data)
    {
        return new ServiceResult<T>
        {
            Success = true,
            Data = data
        };
    }
    
    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static ServiceResult<T> CreateFailure(string error)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Error = error
        };
    }
    
    /// <summary>
    /// Creates a failed result from an exception
    /// </summary>
    public static ServiceResult<T> CreateFailure(Exception ex)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Error = ex.Message
        };
    }
}
