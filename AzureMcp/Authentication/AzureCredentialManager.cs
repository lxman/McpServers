using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Authentication;

/// <summary>
/// Manages Azure credentials using DefaultAzureCredential for Azure services other than DevOps
/// </summary>
public class AzureCredentialManager : ICredentialManager
{
    private readonly ILogger<AzureCredentialManager> _logger;
    private readonly TokenCredential _credential;

    public AzureCredentialManager(
        IConfiguration configuration,
        ILogger<AzureCredentialManager> logger)
    {
        _logger = logger;
        
        var options = new DefaultAzureCredentialOptions();
        
        // Allow explicit tenant configuration
        var tenantId = configuration["Azure:TenantId"];
        if (!string.IsNullOrEmpty(tenantId))
            options.TenantId = tenantId;
            
        // Support different Azure clouds
        var authorityHost = configuration["Azure:AuthorityHost"];
        if (!string.IsNullOrEmpty(authorityHost))
            options.AuthorityHost = new Uri(authorityHost);
            
        _credential = new DefaultAzureCredential(options);
        
        _logger.LogInformation("Azure credential manager initialized");
    }
    
    public TokenCredential GetCredential() => _credential;
    
    public async Task<bool> ValidateCredentialAsync()
    {
        try
        {
            // Test the credential by requesting a token for Azure Resource Manager
            var context = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await _credential.GetTokenAsync(context, CancellationToken.None);
            
            _logger.LogDebug("Azure credential validation successful");
            return !string.IsNullOrEmpty(token.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure credential validation failed");
            return false;
        }
    }
}
