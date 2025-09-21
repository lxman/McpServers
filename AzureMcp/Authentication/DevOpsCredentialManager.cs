using CredentialManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzureMcp.Authentication;

public class DevOpsCredentialManager
{
    private readonly ILogger<DevOpsCredentialManager> _logger;
    private readonly VssConnection _connection;

    public DevOpsCredentialManager(
        string organizationUrl, 
        IConfiguration configuration,
        ILogger<DevOpsCredentialManager> logger,
        string? credentialTarget = null)
    {
        _logger = logger;
        string pat = GetPersonalAccessToken(credentialTarget, configuration);
        
        var credentials = new VssBasicCredential(string.Empty, pat);
        _connection = new VssConnection(new Uri(organizationUrl), credentials);
        
        _logger.LogInformation("Azure DevOps connection established for {Organization}", organizationUrl);
    }

    private string GetPersonalAccessToken(string? credentialTarget, IConfiguration configuration)
    {
        // Try credential manager first
        string target = credentialTarget ?? "AzureDevOps";
        string? pat = TryGetFromCredentialManager(target);
        if (!string.IsNullOrEmpty(pat))
        {
            _logger.LogDebug("Retrieved PAT from Windows Credential Manager");
            return pat;
        }
        
        // Try configuration
        pat = configuration["AzureDevOps:PAT"];
        if (!string.IsNullOrEmpty(pat))
        {
            _logger.LogDebug("Retrieved PAT from configuration");
            return pat;
        }
        
        // Try environment variable
        pat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        if (!string.IsNullOrEmpty(pat))
        {
            _logger.LogDebug("Retrieved PAT from environment variable");
            return pat;
        }
        
        throw new InvalidOperationException(
            "No Azure DevOps PAT found. Please store it in Windows Credential Manager, " +
            "configuration, or AZURE_DEVOPS_PAT environment variable.");
    }
    
    private string? TryGetFromCredentialManager(string target)
    {
        try
        {
            using var cred = new Credential { Target = target };
            return cred.Load() ? cred.Password : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve credential from Windows Credential Manager");
            return null;
        }
    }
    
    public T GetClient<T>() where T : VssHttpClientBase => _connection.GetClient<T>();
    
    public VssConnection GetConnection() => _connection;
}
