using Microsoft.Extensions.Logging;
﻿using Azure.Core;
using Azure.Identity;

namespace AzureServer.Core.Authentication;

/// <summary>
/// Pure discovery-based Azure credential manager (no config files!)
/// </summary>
public class AzureCredentialManager : ICredentialManager
{
    private readonly ILogger<AzureCredentialManager> _logger;
    private readonly TokenCredential _credential;

    public AzureCredentialManager(ILogger<AzureCredentialManager> logger)
    {
        _logger = logger;
        
        // Pure DefaultAzureCredential with smart options
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true, // Don't pop up browser in MCP context
            ExcludeVisualStudioCodeCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = false,
            ExcludeAzurePowerShellCredential = false
        };
        
        _credential = new DefaultAzureCredential(options);
        
        _logger.LogDebug("Azure credential manager initialized with pure discovery");
    }
    
    public TokenCredential GetCredential() => _credential;
    
    public async Task<bool> ValidateCredentialAsync()
    {
        try
        {
            var context = new TokenRequestContext(["https://management.azure.com/.default"]);
            AccessToken token = await _credential.GetTokenAsync(context, CancellationToken.None);
            
            _logger.LogDebug("Azure credential validation successful");
            return !string.IsNullOrEmpty(token.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Azure credential validation failed");
            return false;
        }
    }
}