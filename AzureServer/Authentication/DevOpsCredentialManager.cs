using System.Diagnostics.CodeAnalysis;
using AzureServer.Authentication.models;
using Meziantou.Framework.Win32;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzureServer.Authentication;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class DevOpsCredentialManager
{
    // Public interface methods (compatible with existing DevOpsService)
    public T GetClient<T>() where T : VssHttpClientBase => _connection.GetClient<T>();
    public VssConnection GetConnection() => _connection;
    public string GetOrganizationUrl() => _organizationUrl;

    private readonly ILogger<DevOpsCredentialManager> _logger;
    private readonly VssConnection _connection;
    private readonly string _organizationUrl;

    /// <summary>
    /// Creates DevOps manager from discovered environment info (primary constructor)
    /// </summary>
    public DevOpsCredentialManager(
        DevOpsEnvironmentInfo environmentInfo,
        ILogger<DevOpsCredentialManager> logger)
    {
        _logger = logger;
        _organizationUrl = environmentInfo.OrganizationUrl;
        
        if (string.IsNullOrEmpty(environmentInfo.PersonalAccessToken))
        {
            throw new InvalidOperationException(
                $"No Personal Access Token available for {environmentInfo.OrganizationUrl}. " +
                $"Environment was discovered via: {environmentInfo.Source}");
        }

        var credentials = new VssBasicCredential(string.Empty, environmentInfo.PersonalAccessToken);
        _connection = new VssConnection(new Uri(environmentInfo.OrganizationUrl), credentials);
        
        _logger.LogInformation("Azure DevOps connection established for {Organization} (discovered via {Source})", 
            environmentInfo.OrganizationUrl, environmentInfo.Source);
    }

    /// <summary>
    /// Static factory method for seamless creation with discovery
    /// </summary>
    public static async Task<DevOpsCredentialManager?> CreateFromDiscoveryAsync(
        ILogger<DevOpsCredentialManager> logger)
    {
        var discoveryLogger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<AzureEnvironmentDiscovery>();
        var discovery = new AzureEnvironmentDiscovery(discoveryLogger);
        var result = await discovery.DiscoverAzureEnvironmentsAsync();
        
        var primaryEnvironment = result.DevOpsEnvironments.FirstOrDefault();
        if (primaryEnvironment is not null) return new DevOpsCredentialManager(primaryEnvironment, logger);
        logger.LogWarning("No Azure DevOps environments discovered");
        return null;

    }

    /// <summary>
    /// Manual creation with organization URL (for advanced scenarios)
    /// </summary>
    public static DevOpsCredentialManager CreateManual(
        string organizationUrl,
        ILogger<DevOpsCredentialManager> logger,
        string? credentialTarget = null)
    {
        var pat = DiscoverPersonalAccessToken(credentialTarget, organizationUrl, logger);
        
        var environmentInfo = new DevOpsEnvironmentInfo
        {
            OrganizationUrl = organizationUrl,
            PersonalAccessToken = pat,
            Source = "Manual Creation",
            HasCredentials = true,
            CredentialTarget = credentialTarget
        };

        return new DevOpsCredentialManager(environmentInfo, logger);
    }

    private static string DiscoverPersonalAccessToken(string? credentialTarget, string organizationUrl, ILogger logger)
    {
        // Try credential manager first
        var target = credentialTarget ?? "AzureDevOps";
        var pat = TryGetFromCredentialManager(target);
        if (!string.IsNullOrEmpty(pat))
        {
            logger.LogDebug("Retrieved PAT from Windows Credential Manager");
            return pat;
        }
        
        // Try environment variables
        pat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_EXT_PAT") ?? 
              Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ??
              Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        if (string.IsNullOrEmpty(pat))
            throw new InvalidOperationException(
                $"No Personal Access Token found for {organizationUrl}. Please:\n" +
                "1. Store PAT in Windows Credential Manager (target: 'AzureDevOps'), OR\n" +
                "2. Set AZURE_DEVOPS_PAT environment variable, OR\n" +
                "3. Configure Azure CLI: az devops configure --defaults organization=your-org-url");
        logger.LogDebug("Retrieved PAT from environment variable");
        return pat;

    }
    
    private static string? TryGetFromCredentialManager(string target)
    {
        try
        {
            var cred = CredentialManager.ReadCredential(target);
            return cred?.Password;
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            var client = _connection.GetClient<ProjectHttpClient>();
            await client.GetProjects();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DevOps connection validation failed for {Organization}", _organizationUrl);
            return false;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}