namespace AzureServer.Common.Exceptions;

/// <summary>
/// Exception thrown when credential-related operations fail
/// </summary>
public class CredentialException : AzureException
{
    public CredentialException() : base("Credential operation failed")
    {
    }

    public CredentialException(string message) : base(message)
    {
    }

    public CredentialException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    /// <summary>
    /// The credential source that failed (e.g., "Windows Credential Manager", "Environment Variable")
    /// </summary>
    public string? CredentialSource { get; set; }
    
    /// <summary>
    /// The credential target or key that was being accessed
    /// </summary>
    public string? CredentialTarget { get; set; }
    
    /// <summary>
    /// Creates a credential exception with source and target information
    /// </summary>
    public static CredentialException Create(string message, string source, string target, Exception? innerException = null)
    {
        return new CredentialException(message, innerException!)
        {
            CredentialSource = source,
            CredentialTarget = target
        };
    }
}
