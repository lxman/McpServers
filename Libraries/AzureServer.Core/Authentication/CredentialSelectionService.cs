using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Identity;

namespace AzureServer.Core.Authentication;

/// <summary>
/// Manages credential selection and session storage
/// </summary>
public class CredentialSelectionService(
    ILogger<CredentialSelectionService> logger,
    CredentialDiscoveryService discoveryService)
{
    // Session storage for selected credential
    private CredentialInfo? _selectedCredential;
    private TokenCredential? _selectedTokenCredential;
    private List<CredentialInfo>? _availableCredentials;

    /// <summary>
    /// Get the current credential, discovering and selecting if necessary
    /// </summary>
    public async Task<(TokenCredential? Credential, CredentialSelectionResult Result)> GetCredentialAsync()
    {
        // If we already have a selected credential for this session, return it
        if (_selectedTokenCredential is not null && _selectedCredential is not null)
        {
            return (_selectedTokenCredential, new CredentialSelectionResult
            {
                Status = SelectionStatus.UsingExisting,
                SelectedCredential = _selectedCredential
            });
        }

        // Discover available credentials
        _availableCredentials = await discoveryService.DiscoverCredentialsAsync();

        if (_availableCredentials.Count == 0)
        {
            logger.LogWarning("No valid Azure credentials found");
            return (null, new CredentialSelectionResult
            {
                Status = SelectionStatus.NoCredentialsFound,
                ErrorMessage = "No valid Azure credentials found. Please login using Azure CLI, Visual Studio, or configure environment variables."
            });
        }

        if (_availableCredentials.Count == 1)
        {
            // Only one credential available, use it automatically
            CredentialInfo credential = _availableCredentials[0];
            _selectedCredential = credential;
            _selectedTokenCredential = CreateTokenCredential(credential.Id);
            
            logger.LogInformation("Using {Source} credential automatically (only option available)", credential.Source);
            
            return (_selectedTokenCredential, new CredentialSelectionResult
            {
                Status = SelectionStatus.AutoSelected,
                SelectedCredential = credential
            });
        }

        // Multiple credentials available - need user selection
        return (null, new CredentialSelectionResult
        {
            Status = SelectionStatus.SelectionRequired,
            AvailableCredentials = _availableCredentials
        });
    }

    /// <summary>
    /// Select a credential by ID
    /// </summary>
    public (TokenCredential? Credential, CredentialSelectionResult Result) SelectCredential(string credentialId)
    {
        if (_availableCredentials is null || _availableCredentials.Count == 0)
        {
            return (null, new CredentialSelectionResult
            {
                Status = SelectionStatus.Error,
                ErrorMessage = "No credentials available. Call GetCredentialAsync first."
            });
        }

        CredentialInfo? credential = _availableCredentials.FirstOrDefault(c => c.Id == credentialId);
        if (credential is null)
        {
            return (null, new CredentialSelectionResult
            {
                Status = SelectionStatus.Error,
                ErrorMessage = $"Credential '{credentialId}' not found. Available: {string.Join(", ", _availableCredentials.Select(c => c.Id))}"
            });
        }

        _selectedCredential = credential;
        _selectedTokenCredential = CreateTokenCredential(credentialId);
        
        logger.LogInformation("Selected {Source} credential for this session", credential.Source);

        return (_selectedTokenCredential, new CredentialSelectionResult
        {
            Status = SelectionStatus.Selected,
            SelectedCredential = credential
        });
    }

    /// <summary>
    /// Get available credentials (for display to user)
    /// </summary>
    public List<CredentialInfo>? GetAvailableCredentials() => _availableCredentials;

    /// <summary>
    /// Clear the current selection (force re-selection)
    /// </summary>
    public void ClearSelection()
    {
        _selectedCredential = null;
        _selectedTokenCredential = null;
        _availableCredentials = null;
        logger.LogInformation("Cleared credential selection");
    }

    /// <summary>
    /// Create a TokenCredential based on credential ID
    /// </summary>
    private TokenCredential CreateTokenCredential(string credentialId)
    {
        return credentialId switch
        {
            "azure-cli" => new AzureCliCredential(),
            "visual-studio" => new VisualStudioCredential(),
            "environment" => new EnvironmentCredential(),
            "azure-powershell" => new AzurePowerShellCredential(),
            "shared-token-cache" => new SharedTokenCacheCredential(),
            _ => throw new ArgumentException($"Unknown credential ID: {credentialId}")
        };
    }

    /// <summary>
    /// Get the currently selected credential info
    /// </summary>
    public CredentialInfo? GetSelectedCredential() => _selectedCredential;
}

/// <summary>
/// Result of credential selection process
/// </summary>
public class CredentialSelectionResult
{
    public SelectionStatus Status { get; set; }
    public CredentialInfo? SelectedCredential { get; set; }
    public List<CredentialInfo>? AvailableCredentials { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of credential selection
/// </summary>
public enum SelectionStatus
{
    /// <summary>
    /// No credentials found
    /// </summary>
    NoCredentialsFound,
    
    /// <summary>
    /// Only one credential available, automatically selected
    /// </summary>
    AutoSelected,
    
    /// <summary>
    /// Multiple credentials available, user selection required
    /// </summary>
    SelectionRequired,
    
    /// <summary>
    /// User has selected a credential
    /// </summary>
    Selected,
    
    /// <summary>
    /// Using previously selected credential from session
    /// </summary>
    UsingExisting,
    
    /// <summary>
    /// Error occurred
    /// </summary>
    Error
}
