using Azure.Core;

namespace AzureServer.Core.Authentication;

/// <summary>
/// Common interface for credential management across Azure services
/// </summary>
public interface ICredentialManager
{
    /// <summary>
    /// Get a token credential for Azure services
    /// </summary>
    TokenCredential GetCredential();
    
    /// <summary>
    /// Test if the credential is valid
    /// </summary>
    Task<bool> ValidateCredentialAsync();
}
