namespace AzureMcp.Common.Exceptions;

/// <summary>
/// Base exception for Azure-related errors
/// </summary>
public class AzureException : Exception
{
    public AzureException() : base()
    {
    }

    public AzureException(string message) : base(message)
    {
    }

    public AzureException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    /// <summary>
    /// Azure service that caused the exception
    /// </summary>
    public string? ServiceName { get; set; }
    
    /// <summary>
    /// Azure error code if available
    /// </summary>
    public string? ErrorCode { get; set; }
}
