using Azure.Core;
using Azure.ResourceManager;
using AzureServer.Authentication;

namespace AzureServer.Services.Core;

/// <summary>
/// Factory service for managing ArmClient instances across the application.
/// This service is a singleton that ensures we reuse the same ArmClient instance
/// for all services, reducing overhead and maintaining consistency.
/// </summary>
public class ArmClientFactory(
    CredentialSelectionService credentialService,
    ILogger<ArmClientFactory> logger)
{
    private ArmClient? _armClient;
    private TokenCredential? _credential;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // Track the credential info used to create the current ArmClient
    private CredentialInfo? _currentCredential;

    /// <summary>
    /// Gets or creates an ArmClient instance using the current credentials.
    /// This method is thread-safe and will reuse the same client instance
    /// unless the credentials have changed.
    /// </summary>
    public async Task<ArmClient> GetArmClientAsync()
    {
        // Fast path - if we already have a client, return it
        if (_armClient is not null)
        {
            // Check if credentials have changed
            (_, var result) = await credentialService.GetCredentialAsync();
            if (result.SelectedCredential?.Id == _currentCredential?.Id)
            {
                return _armClient;
            }
            
            logger.LogInformation(
                "Credentials have changed from {OldCredential} to {NewCredential}, creating new ArmClient",
                _currentCredential?.Id ?? "none",
                result.SelectedCredential?.Id ?? "none");
        }

        // Slow path - need to create or recreate the client
        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern - another thread might have created it
            if (_armClient is not null)
            {
                (_, var recheckResult) = await credentialService.GetCredentialAsync();
                if (recheckResult.SelectedCredential?.Id == _currentCredential?.Id)
                {
                    return _armClient;
                }
            }

            // Get the credential and create the ArmClient
            (var credential, var credResult) = await credentialService.GetCredentialAsync();
            
            if (credResult.Status == SelectionStatus.NoCredentialsFound)
            {
                throw new InvalidOperationException(
                    "No Azure credentials found. Please authenticate using Azure CLI, Visual Studio, or environment variables.");
            }

            logger.LogInformation(
                "Creating new ArmClient using credential: {CredentialSource} ({CredentialId})",
                credResult.SelectedCredential?.Source ?? "Unknown",
                credResult.SelectedCredential?.Id ?? "Unknown");

            _credential = credential;
            _armClient = new ArmClient(credential);
            _currentCredential = credResult.SelectedCredential;
            
            return _armClient;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the current TokenCredential.
    /// This is useful for services that need to create their own specialized clients
    /// (like LogsQueryClient or MetricsQueryClient) that don't use ArmClient.
    /// </summary>
    public async Task<TokenCredential> GetCredentialAsync()
    {
        // Ensure we have initialized the credential
        if (_credential is null)
        {
            await GetArmClientAsync();
        }
        
        return _credential ?? throw new InvalidOperationException("Failed to obtain Azure credentials");
    }

    /// <summary>
    /// Gets both the ArmClient and the TokenCredential.
    /// This is useful for services that need both.
    /// </summary>
    public async Task<(ArmClient armClient, TokenCredential credential)> GetClientAndCredentialAsync()
    {
        var armClient = await GetArmClientAsync();
        var credential = _credential ?? throw new InvalidOperationException("Credential not available");
        return (armClient, credential);
    }

    /// <summary>
    /// Invalidates the current ArmClient instance, forcing recreation on next access.
    /// This can be useful when credentials are explicitly changed or refreshed.
    /// </summary>
    public void InvalidateClient()
    {
        logger.LogInformation("Invalidating current ArmClient instance");
        _armClient = null;
        _credential = null;
        _currentCredential = null;
    }

    /// <summary>
    /// Gets information about the current ArmClient instance
    /// </summary>
    public ArmClientInfo? GetCurrentClientInfo()
    {
        if (_armClient is null || _currentCredential is null)
            return null;

        return new ArmClientInfo
        {
            CredentialId = _currentCredential.Id,
            CredentialSource = _currentCredential.Source,
            IsActive = true
        };
    }
}

/// <summary>
/// Information about the current ArmClient instance
/// </summary>
public class ArmClientInfo
{
    public string CredentialId { get; init; } = string.Empty;
    public string CredentialSource { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
